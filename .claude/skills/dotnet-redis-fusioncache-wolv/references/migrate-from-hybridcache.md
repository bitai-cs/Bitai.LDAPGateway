## Migrating from HybridCache

Do the Preflight first. The migration changes what a cache entry looks like in Redis and how nodes notify each other, so it is **not** a drop-in change for a fleet that must keep running.

| Aspect | HybridCache | FusionCache (this skill) |
|---|---|---|
| Registration | `AddHybridCache()` + `HybridCacheOptions` | `AddFusionCache()...` (Step 3b) |
| Read | `cache.GetOrCreateAsync(key, state, factory, options, tags, ct)` | `cache.GetOrSetAsync<T>(key, factory, tags: ..., token: ct)`; capture dependencies in the lambda instead of a state tuple |
| Remove one / many | `RemoveAsync(key)` / `RemoveAsync(keys)` | `RemoveAsync(key)` in a loop (Step 3c) |
| Remove by tag | `RemoveByTagAsync(tag)` | `RemoveByTagAsync(tag)` or the `IEnumerable<string>` overload; propagates through the backplane |
| Flush this node only | `RemoveByTagAsync("*")` | `ClearAsync(allowFailSafe: false, LocalOnly)` (Step 3e); the default `ClearAsync()` is cluster-wide |
| L1 / L2 TTL | `LocalCacheExpiration` / `Expiration` | `Duration` / `DistributedCacheDuration` |
| Key isolation | `InstanceName` on the Redis cache | `CacheKeyPrefix` (and `BackplaneChannelPrefix`) |
| Cross-node propagation | none | FusionCache backplane + this skill's generation reconciler |

Plan:
1. **Pick a new `KeyPrefix` version** (for example `v2:`). Do not point FusionCache at keys written by HybridCache: the stored value formats are different [Assumption: confirm in the target]. Old entries expire by TTL.
2. **Move the code in this order:** packages and registration (Step 1, 3b); `ICacheInvalidator` implementation (3c-3e); query handlers; remove the HybridCache registration, the custom invalidation channel, the message contract, the subscriber, and the old `CachingOptions` fields (`InstanceName`, `InvalidationChannel`, `MaxPendingMessages`, `DefaultExpiration`, `DefaultLocalExpiration`).
3. **Rolling deploy.** Old pods (HybridCache, old channel) and new pods (FusionCache, new prefix/channel) do not see each other's invalidations. Either deploy in one step (blue/green, or scale old to zero before new serves), or accept a window bounded by the old pods' `LocalCacheExpiration` and the new pods' `MemoryCacheDuration`, and keep both short during the migration.
4. **Bridge, only if needed.** FusionCache can also be registered as the `HybridCache` implementation (`AddFusionCache()...AsHybridCache()`), which lets code that still injects `HybridCache` get backplane behavior while you migrate. This skill's target state uses `IFusionCache` directly; confirm in a test that the adapter exposes what you use (for example `RemoveByTagAsync`) before relying on it [Assumption: not exercised in this revision].
5. Run the Step 5 matrix against the migrated build.
