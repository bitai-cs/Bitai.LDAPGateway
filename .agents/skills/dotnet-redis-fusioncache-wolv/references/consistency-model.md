## Why this skill exists

`HybridCache` (`Microsoft.Extensions.Caching.Hybrid`) gives L1 + L2 + stampede protection but **does not synchronize L1 across instances** [Verified, see References]: `RemoveAsync` clears L2 and the *calling* instance's L1 only, and `RemoveByTagAsync` is a logical marker that other instances only notice when they consult L2, which an L1 hit never does. FusionCache is a drop-in alternative that adds the missing piece: a **backplane**. After `Set`/`Remove`/`Expire` (and `RemoveByTag`/`Clear`, which are marker based) the node publishes a small notification through Redis Pub/Sub, and the other nodes drop their L1 copy. FusionCache tagging also states that without a backplane the cache "can become incoherent" until L1 expires [Verified, see References].

What FusionCache does **not** give you, and this skill adds:
1. **Post-commit invalidation.** The backplane notification is sent when you call `RemoveAsync`; it knows nothing about your database transaction. Invalidating before the commit lets a reader re-populate the old row. The Wolverine outbox (Step 2, Step 3f) provides "after commit, and retried after a crash".
2. **Repair of missed notifications.** FusionCache's auto-recovery retries *failed sends* from the publishing node through an in-process, non-persisted queue [Verified]. The documentation does not say that notifications missed by a *disconnected subscriber* are replayed [Verified: not documented; treat as not guaranteed]. A node that missed one keeps the stale entry until L1 expires. This skill keeps a shared **generation counter** so a node can notice it missed something and flush its own L1 (Step 3e).
3. **Cross-instance factory races** are still not solved by invalidation (Step 4, rule 7).

Re-verify these semantics against the exact package version in use; the integration tests in Step 5 do this.

## Consistency model (read before choosing the design)

FusionCache's backplane is Redis Pub/Sub underneath, which is **at-most-once** [Verified]: a message goes to the subscribers connected at that moment, with no acknowledgement and no replay. `PublishAsync` returns a receiver count, which is **not** confirmation that any node applied the invalidation.

"Invalidated" in this skill means: after the invalidating write commits and the invalidation runs, every *connected, healthy* instance drops the affected entries within the delivery latency of Redis. For every other case the guarantee comes from reconciliation or from the TTL, not from Pub/Sub:

| Scenario | What protects against stale data | Residual window |
|---|---|---|
| Normal delivery | FusionCache backplane notification applied by each node | Redis latency |
| Publisher cannot send (transient Redis error) | FusionCache auto-recovery queue (in-process, lost if the process dies) and the Wolverine retry of the invalidation handler | Retry latency |
| Subscriber disconnected and reconnects | `ConnectionRestored` marks the node for a local flush | Until reconnect + flush (about 1 s after the event) |
| Notification AND generation event both lost (network drop, Redis failover) | Generation gap detection compares the Redis counter with the generations seen | `ReconcileInterval` x 2 (default about 60 s) |
| Publisher crashes after `INCR` and before the publishes | The counter advanced without a matching event: gap detection flushes every node. The Wolverine redelivery then repeats the invalidation. | `ReconcileInterval` x 2, then outbox latency |
| Partial invalidation failure after `INCR` (a removal or the generation publish throws, no crash) | Same as the crash row: the counter advanced without a matching event, gap detection flushes every node; the Wolverine retry repeats the invalidation (only for the exception types configured in Step 3f) | `ReconcileInterval` x 2, then retry latency |
| Publisher crashes between commit and invalidation | Durable outbox redelivers the invalidation | Outbox retry latency |
| Redis lost its state (restart without persistence) | Generation going backwards is treated as a gap | `ReconcileInterval` x 2 |
| **A node misses only the FusionCache notification but receives the generation event** | **Not detected** (the two travel as separate messages). The entry is served from L1 until its L1 expiry. | Up to `MemoryCacheDuration` [Assumption: verify for tag markers in Step 5] |
| Factory already running during the commit finishes after the invalidation | **Not covered by invalidation.** Needs key versioning/fencing, or accept the window (Step 4) | One factory duration, up to the entry's TTL |

**Delivery options** **[Design]** (choose consciously, document the choice in the target solution):

| Option | Delivery | Use when |
|---|---|---|
| **A. FusionCache backplane + generation reconciliation + outbox (this skill's default)** | Best-effort fast path; repair by local flush on gap/reconnect | Bounded staleness of about `2 x ReconcileInterval` is acceptable for lost messages and Redis is already the L2. |
| **B. FusionCache backplane + outbox, without the reconciler** | Best-effort; a missed notification is bounded only by `MemoryCacheDuration` | The data tolerates `MemoryCacheDuration` staleness (keep it short, for example 30-60 s). Smallest code: skip Step 3e. |
| **C. Durable fan-out with Redis Streams** | At-least-once; every instance reads the stream with **its own consumer group** and acknowledges | A lost invalidation is not acceptable; accept the cost of stream trimming and per-instance group management. Wolverine's Redis transport is stream-based; by default consumers within a group compete (one node handles a message), so fan-out requires a distinct group per instance. This replaces the FusionCache backplane for invalidation and is not implemented here. |

The rest of this skill implements option A (drop Step 3e for option B).
