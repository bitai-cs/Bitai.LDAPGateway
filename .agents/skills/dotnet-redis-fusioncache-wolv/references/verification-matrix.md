## Step 5: Verification

The two-host test proves the happy path, which is necessary but **not sufficient** for the guarantees in this document. Run both sets.

**Baseline (cross-instance sync):**

Test harness gotcha: obtain the `IFusionCache` under test from each host's own service provider (or by constructor injection), never from a different host's provider, and give each node its own `ServiceProvider`/`IHost`. When Redis runs in Docker bound to `127.0.0.1`, use `127.0.0.1:6379` rather than `localhost` in the connection string on Windows [Assumption: carried over from the previous revision].
1. Start Redis with Testcontainers (`Testcontainers.Redis`).
2. Build **two** hosts (`WebApplicationFactory` x2 or two `IHost`s) that point at the same Redis, with the same `KeyPrefix` and `ChannelPrefix`.
3. Prime the key on host A and host B (both now hold L1 copies). Include a case where the node that invalidates **never held the key** [Verified to work in the scratch run].
4. Execute the update on host A through Wolverine (`IMessageBus.InvokeAsync(new UpdateProductPriceCommand(...))`).
5. Configure both hosts with `Caching:MemoryCacheDuration=00:10:00` (configuration only), then assert that host B returns the new value within a short timeout (poll up to ~2 s), so only the backplane path can explain the refresh.
6. Repeat for a tag invalidation and a key invalidation.
7. Negative test: start host B with `IgnoreIncomingBackplaneNotifications = true` (or without a backplane) and confirm it stays stale (proves the test is meaningful).

**Failure and recovery matrix** (a long `MemoryCacheDuration` plus a passing happy path does **not** prove the recovery guarantees; the tests below must force a loss and prove reconciliation):

| Scenario | Preparation | Expected result |
|---|---|---|
| Key invalidation | A and B hold L1; A invalidates. | B stops serving its local copy after the event. |
| Tag invalidation | A and B hold the same tagged entry. | B drops it on the event; the next read returns new data. |
| Remote never cached | The invalidating node never read the key; B holds it. | B is invalidated. |
| Lost notification, generation delivered | B with `IgnoreIncomingBackplaneNotifications`; A invalidates through `ICacheInvalidator`. | B keeps the stale value until `MemoryCacheDuration`. **Documents the blind spot**; the test fails if the team assumed otherwise. |
| Gap with no event | Increment the generation key directly (no publish), or kill the publisher after `INCR`. | B confirms the gap within `2 x ReconcileInterval` and flushes; the stale entry is no longer served. |
| Lost on disconnect | B loses its Redis connection during an invalidation. | After reconnect B flushes and stops serving stale data; measure the time. |
| Flush stays local | Trigger B's reconciler flush while A holds warm entries. | A's L1 and L2 are untouched; only B refills. |
| Malformed generation event | Publish a non-numeric payload on `GenerationChannel`. | Counted in `cache_generation_malformed`, B flushes once, no crash. |
| Out-of-order overflow | Publish more distinct future generations than `MaxTrackedGenerations`. | B flushes instead of growing memory, then returns to `Healthy`. |
| Redis state loss | Restart Redis without persistence while hosts hold L1. | Counter going backwards is a gap; nodes flush; no permanent staleness. |
| In-flight factory | B starts a factory before the commit and finishes after the invalidation. | Behavior matches the chosen option in Step 4 rule 7. |
| Repeated reconnects | Force several `ConnectionRestored` events in a burst. | One debounced flush, no storm of calls, no errors during shutdown. |
| Subscription setup fails midway | `SubscribeAsync` succeeds but `ReadGenerationAsync` throws (Redis drops between the two). | The subscription is undone and retried by the loop; `HasSubscribed` becomes true once it succeeds; readiness does not stay `Unhealthy` forever. |
| Flush in progress | Block `FlushAsync` (for example a gated `ClearAsync`) and sample the health check. | `IsReconciling` is true and readiness is `Degraded` during the flush, `Healthy` after it. |
| Partial failure after `INCR` | Inject a failure after the increment, after one key removal, after the tag removal, and before/after the generation publish. | Other nodes confirm a gap and flush; the Wolverine retry repeats the invalidation; no stale entry outlives `2 x ReconcileInterval` plus retry latency. |
| Not-found caching | Read a missing id, insert it, read again. | The miss is served from cache for at most `CachePolicies.NotFoundDuration`, then the new row is returned. |
| Shutdown with Redis down | Stop the host while Redis is unreachable. | `StopAsync` returns within the host shutdown timeout (bounded by `ct` and `AsyncTimeout`), no unobserved exceptions. |
| Redis down at startup | Start the host without Redis. | Host starts, readiness is `Unhealthy` then recovers once subscribed; the subscription is retried without a restart. |
| Redis lost at runtime | Stop Redis after L1 is warm. | Readiness `Degraded`; serving-stale policy matches the business requirement. |
| Outbox + rollback | Command fails before commit. | No invalidation is issued for an unconfirmed write. |
| Outbox + crash | Commit, then kill the process before dispatch. | The invalidation stays pending and runs after recovery. |
| Kubernetes readiness | Reconciler not subscribed / Redis down / healthy. | Probe results match the intended policy and the cluster removes/re-adds the pod accordingly. |
| Load with N instances | Frequent invalidations, many nodes, and a forced flush. | Measure L1 churn from miss notifications (Step 4 rule 8), L2 operations, latency, and factory pressure after flushes. |

Readiness mapping to assert: pre-subscription -> `Unhealthy` (503), Redis lost after startup or flush pending/in progress -> `Degraded` (200 by default; a deliberate default, teams that cannot tolerate stale reads should map `Degraded` to a non-success status through `HealthCheckOptions.ResultStatusCodes`), normal -> `Healthy` (200). `cache_reconciler_ready` must follow `IsReady` (1 only when subscribed and connected).

Unit tests: mock `ICacheInvalidator` and assert that command handlers return the expected `CacheInvalidationRequested` (keys/tags); unit-test `GenerationTracker` (reordering, duplicates, hole persisting across two checks, counter going backwards, overflow of `maxAhead`). Do not test FusionCache internals.
