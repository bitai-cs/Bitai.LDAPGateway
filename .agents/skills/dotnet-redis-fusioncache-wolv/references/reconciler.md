### 3e. Reconciler (repairs missed invalidations)

The reconciler watches the generation events and flushes this node's **own L1** whenever it *knows or suspects* it missed something: after a reconnect, after a confirmed generation gap, after a malformed event, or when it holds too many out-of-order generations.

> **Code:** [`assets/infrastructure/CacheReconciler.cs`](../assets/infrastructure/CacheReconciler.cs)

> **Code:** [`assets/infrastructure/CacheReconcilerHealthCheck.cs`](../assets/infrastructure/CacheReconcilerHealthCheck.cs)

Notes:
- **Reconnect/gap/malformed/overflow all converge on one repair**: `MarkDirty()`, then a debounced local flush. `ClearAsync` with the skip flags is a *logical* clear on this node [Verified]: after it, entries created before the clear are treated as expired **even when they are still in L2** (the factory re-ran in the scratch run), so the node refills from the database, not from Redis. Cost: a cold start of this node's cache. Monitor `cache_reconcile_flushes`.
- **Other nodes are not evicted by your flush.** They repair themselves through their own signals (their reconnect or their gap check). That is why the gap check exists: it covers a loss with no local signal.
- **Do not use the default `ClearAsync()` for the flush.** Its default propagates cluster-wide through the backplane [Verified], so one node's repair would flush every node. The skip flags are what keep it local.
- **Generation key.** `GenerationKey` MUST be scoped per service and environment, and it MUST survive Redis memory pressure. Redis has no per-key "do not evict" flag, so this is a property of the **deployment's `maxmemory-policy`**: under `allkeys-lru`/`allkeys-lfu`/`allkeys-random` any key, TTL or not, can be evicted and gap detection is silently disabled. Either (a) use a `volatile-*` policy and keep the generation key without a TTL (it is then never an eviction candidate, but the other keys in that Redis must carry TTLs for the policy to free memory), or (b) keep coordination state in a dedicated Redis instance with a suitable policy. `noeviction` also protects the key but makes writes fail when memory is full. Alert on the effective `maxmemory-policy` (`CONFIG GET maxmemory-policy`) and on the generation key disappearing. [Assumption: confirm the policy of the target Redis.]
- **Known blind spot.** A node that receives the generation event but misses the FusionCache notification is not detected (consistency model). If that is not acceptable, shorten `MemoryCacheDuration` for that data or use delivery option C.
- **Back-pressure.** The Redis callback does constant-time work. The only thing that can grow is the set of out-of-order generations, capped by `MaxTrackedGenerations`; beyond it the node flushes instead of tracking.
- Subscribe in `StartAsync` so the subscription exists while the host starts. This does not by itself stop traffic: Kestrel may already be listening, and readiness is an operational gate (Step 5 checks the mapping).
- Readiness semantics: `Unhealthy` until the first subscription exists, `Degraded` if Redis becomes unreachable later or a flush is pending (the pod stays in rotation by default, because `Degraded` maps to HTTP 200).
- **Telemetry.** The meter is `MyService.Caching`: gauge `cache_reconciler_ready`, counters `cache_reconcile_flushes` and `cache_generation_malformed`. FusionCache has its own logs/OpenTelemetry integration for hits, misses, and backplane events [Assumption: confirm the package and meter names for the version in use].
