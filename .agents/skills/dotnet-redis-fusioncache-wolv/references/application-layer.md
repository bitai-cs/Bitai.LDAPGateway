## Step 2: Application layer

> **Code:** [`assets/application/ICacheInvalidator.cs`](../assets/application/ICacheInvalidator.cs)

> **Code:** [`assets/application/CacheKeys.cs`](../assets/application/CacheKeys.cs)

Rules for keys/tags:
- Keys must be deterministic and versionable. The environment/service/version prefix is **not** part of the key: it is `CachingOptions.KeyPrefix`, applied by FusionCache (`CacheKeyPrefix`). Add a version segment to that prefix (`v1:`) when the cached shape changes.
- Prefer **tags** for "everything related to X" (lists, aggregates, projections). Use explicit keys only for single entries. Do not over-tag: FusionCache tagging is client-assisted (a marker per tag, checked on reads) and there is no "get entries by tag" operation [Verified, see References].
- Include tenant/user/culture in the key if the data varies by it.

> **Code:** [`assets/application/CacheInvalidationRequested.cs`](../assets/application/CacheInvalidationRequested.cs)

### Read side: query handler (Wolverine) using IFusionCache

> **Code:** [`assets/application/GetProduct.cs`](../assets/application/GetProduct.cs)

Guidelines:
- Cache **DTOs / read models**, never EF entities or tracked/mutable objects. Prefer `sealed record` types. A `sealed record` is only *shallowly* immutable: a record that exposes a `List<T>` lets one caller mutate an instance another caller may share from L1. Use `IReadOnlyList<T>`/immutable collections, and make sure the configured serializer can round-trip the type (System.Text.Json here). FusionCache can skip its defensive copy for immutable types (`SkipAutoCloneForImmutableObjects`), so deep immutability matters. Changing the shape during a rolling deploy needs a new cache prefix version (Step 4, rule 6).
- The factory must not capture a `DbContext` that is disposed later; use read-only/`AsNoTracking` queries through a repository whose lifetime covers the call.
- Do not cache `null` unintentionally: decide explicitly. The example above caches not-found for `CachePolicies.NotFoundDuration` only. Alternatives: a dedicated sentinel result, or never caching misses (then accept database load on repeated misses). Add a test that proves the chosen behavior (a missing row is cached briefly, and a row inserted afterwards is served after that duration).
- Never cache per-request secrets or authorization decisions with long TTLs.

### Read side: list query with a real tag strategy

A list entry contains many entities, so it must be invalidated when any of them changes. Tag it **either** with the tag of every element it contains **or** with a coarse tag that every mutation of those elements also invalidates. The example uses the second strategy.

> **Code:** [`assets/application/GetProductsPage.cs`](../assets/application/GetProductsPage.cs)

Strategy used here: lists carry only the coarse `CacheTags.Products` tag, and every product mutation returns `ForTags(CacheTags.Product(id), CacheTags.Products)`. That keeps list invalidation correct without per-element tags; the cost is that any product change invalidates all lists. `CacheTags.Product(id)` only invalidates a list if the list was written with that tag, so never rely on it for a list that was not.

### Write side: command handler returns the invalidation as a cascading message

> **Code:** [`assets/application/UpdateProductPriceCommand.cs`](../assets/application/UpdateProductPriceCommand.cs)

Domain-event variant: if the aggregates raise domain events and Wolverine publishes them (`opts.PublishDomainEventsFromEntityFrameworkCore<EntityBase>(e => e.DomainEvents)`), write an Application-layer handler per event (`ProductPriceChanged` -> returns `CacheInvalidationRequested`). This keeps command handlers free of cache knowledge.

**Never invalidate before the commit.** Invalidating first lets a concurrent reader re-populate the cache with the old row. Returning the message from a transactional handler holds it in the outbox until the commit, and a durable local queue redelivers it if the process crashes; this holds only under the conditions listed in Step 3f. Delivery to the *other* nodes afterwards is still best-effort Pub/Sub (see the consistency model).
