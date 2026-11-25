### 3f. Wolverine handler (Infrastructure) and routing

> **Code:** [`assets/wolverine/CacheInvalidationRequestedHandler.cs`](../assets/wolverine/CacheInvalidationRequestedHandler.cs)

> **Code:** [`assets/wolverine/WolverineSetup.cs`](../assets/wolverine/WolverineSetup.cs)

Wolverine API names shift between releases; if a method above does not compile, check the current Wolverine docs for local queues, durability, and error policies rather than dropping the outbox or retries. The same caution applies to `PublishDomainEventsFromEntityFrameworkCore<T>` in Step 2. These Wolverine snippets were compiled and run against WolverineFx 6.65.1 on 2026-11-23; the `usings` that `OnException(...).RetryWithCooldown(50.Milliseconds(), ...)` needs (`Wolverine.ErrorHandling`, `JasperFx.Core`) are already in the asset.

**What makes the post-commit guarantee real** (all of these MUST hold, otherwise treat the invalidation as best-effort):
- The Wolverine message store (`PersistMessagesWith...`) points at the **same database** as the business `DbContext`, and the EF Core integration (`UseEntityFrameworkCoreTransactions`) enlists the `DbContext` in the same transaction as the outbox rows. The outgoing `CacheInvalidationRequested` is stored with the data change and released only after commit.
- The handler runs through Wolverine's transactional middleware (`AutoApplyTransactions()` or `[Transactional]`), so `SaveChanges` and the outbox write commit together. A handler that opens its own `DbContext`/transaction outside this middleware does not get the guarantee.
- The `cache-invalidation` queue is durable (durable local queues / inbox), so a crash after commit redelivers the invalidation. The redelivery is not immediate: after a real crash the restarted host took 66-81 s to take over the dead node's envelope with Wolverine's default settings (measured on 6.65.1 and earlier on 6.64.2, single node), so staleness during that window is bounded only by `MemoryCacheDuration`. The handler is idempotent (Step 3c).

**Code outside a Wolverine handler** (for example a minimal-API endpoint). Calling `bus.PublishAsync(...)` *after* `SaveChangesAsync()` is **not atomic**: the process can die between the commit and the publish, and the invalidation is lost. Use the outbox-enrolled context instead, or move the work into a Wolverine command handler:

> **Code:** [`assets/wolverine/PriceEndpoint.cs`](../assets/wolverine/PriceEndpoint.cs)

If an outbox is genuinely not available, state the loss window explicitly: a crash between commit and invalidation loses it, and only `MemoryCacheDuration`/TTL recovers it (the generation is incremented *inside* the invalidator, so a crash before it leaves no trace for gap detection). Never call `ICacheInvalidator` before the commit.
