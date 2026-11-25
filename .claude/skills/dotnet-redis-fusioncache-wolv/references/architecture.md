## Architecture overview

```
Command handler (Wolverine, transactional)
   |  writes DB, returns/cascades CacheInvalidationRequested  (held by outbox until commit)
   v
CacheInvalidationRequested handler   (Infrastructure, Wolverine durable local queue)
   |  calls ICacheInvalidator
   v
FusionCacheInvalidator
   |  1. INCR generation
   |  2. IFusionCache.RemoveAsync / RemoveByTagAsync   -> clears local L1 + L2
   |       FusionCache backplane publishes the notification (Redis Pub/Sub)
   |  3. PUBLISH generation event (just the number)
   |
   |        +--------------------------+--------------------------+
   v        v                          v                          v
 Node A   FusionCache backplane      FusionCache backplane      ...
 (origin) drops the L1 entry         drops the L1 entry
          CacheReconciler            CacheReconciler
          observes the generation    observes the generation
          on reconnect / gap -> local-only ClearAsync (this node's L1)
```

The FusionCache notification and the generation event are **two separate Pub/Sub messages**. The first does the work; the second only lets a node prove it did not miss an invalidation event.

Why Pub/Sub and not Wolverine's Redis transport for the generation event: that transport is built on Redis Streams with consumer groups, so by default a message is handled by one node of a group. The reconciler needs every node to see every event.

### Layer placement (Clean Architecture)

| Layer | Contains |
|---|---|
| **Domain** | Nothing cache related. |
| **Application** | `ICacheInvalidator` port, `CacheKeys` / `CacheTags` static helpers, `CacheInvalidationRequested` message, query handlers that read through `IFusionCache`, command handlers that return invalidation messages. |
| **Infrastructure** | `FusionCacheInvalidator`, `CacheReconciler`, `GenerationTracker`, `CachingOptions`, Redis + FusionCache registration, the Wolverine handler for `CacheInvalidationRequested`. |
| **API / Host** | `AddCaching(...)` call, Wolverine setup (`UseWolverine`), configuration binding. |

Application references the `ZiggyCreatures.FusionCache` core package (it defines `IFusionCache`) plus its own ports. It never references `StackExchange.Redis`, the Redis backplane, or the serializer package, and never builds a `FusionCacheOptions`. Redis and registration stay in Infrastructure. [Design] If the team wants Application free of any FusionCache type, wrap reads in their own port; this skill uses `IFusionCache` directly.
