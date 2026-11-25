---
name: dotnet-fusioncache-redis-sync
description: Use when implementing, reviewing, migrating to, or troubleshooting cross-instance cache invalidation in a .NET service that uses FusionCache (ZiggyCreatures.FusionCache) with Redis as the distributed cache (L2) and as the backplane, including migrating from Microsoft.Extensions.Caching.Hybrid (HybridCache), which has no backplane. Apply the WolverineFx outbox/handler patterns only when the target solution already uses WolverineFx with EF Core transaction integration. Do not activate for generic Redis questions, non-.NET applications, IDistributedCache-only solutions, or other cache frameworks, unless the user explicitly asks for an architectural comparison.
---

# FusionCache + Redis Backplane Invalidation (Clean Architecture + Wolverine)

## Scope and conventions

**Use this skill when** the service has several instances (pods/replicas/hosts) that each keep an in-process FusionCache L1 in front of a shared Redis L2, and a write on one instance must invalidate the L1 of the others. Also use it to migrate such a service from `HybridCache`.

**Do not use it when** there is a single instance, the solution only uses `IDistributedCache`/`IMemoryCache`, the language is not .NET, or the user wants a different cache framework.

**Two layers of guidance** (keep them separate when adapting):
- *General principles*: the consistency model, the recovery protocol, and the test matrix. They apply to any L1 + Redis backplane.
- *Concrete pattern*: Clean Architecture layout, `IFusionCache`, Wolverine handlers/outbox, EF Core, and the .NET 10 packages. Apply these only if the target solution matches (see Preflight).

**Rule language.** `MUST` = required for the guarantees in this document to hold. `SHOULD` = default unless there is a documented reason. `MAY` = optional. Statements are tagged when the distinction matters: **[Verified]** = checked against official documentation, an issue tracker, or a run described under *Code status* below; **[Design]** = a decision of this skill that a team may change; **[Assumption]** = depends on the target environment and must be confirmed.

**Code status.** The snippets in `assets/` were compiled and run against specific package versions (FusionCache 2.10.0, WolverineFx 6.65.1, .NET 10, Redis 8, SQL Server 2022 / PostgreSQL 17). Kubernetes probes, multiple real pods, a Wolverine cluster with several live nodes and load are **not** verified. Pin the versions of the target solution, compile, and run the `references/verification-matrix.md` tests before relying on any of it; Wolverine APIs in particular change between releases. The assets include the `using` directives but still rely on project-specific types (`AppDbContext`, `Product`, ...) and were not recompiled as separate files after the split.

## Preflight (do this before writing code)

Inspect the target solution and answer each point. If an answer does not match this skill's assumptions, say so and adapt (or propose the alternative) instead of applying the snippets blindly.

1. Target framework and the exact versions of `ZiggyCreatures.FusionCache*`, `StackExchange.Redis`, and `WolverineFx*` actually resolved. Is the solution **already on HybridCache** (then follow *Migrating from HybridCache*)?
2. Is Redis the `IDistributedCache` (L2) already? Is it shared with other services or environments (key prefix and channel isolation needed)?
3. Instance topology: how many instances, rolling-deploy strategy, autoscaling (cold start cost of a flush).
4. Is WolverineFx present, and is it configured with EF Core transactions and a durable message store in the **same database** as the business data? If not, the post-commit guarantee in Step 2 does not apply as written.
5. Staleness policy: how stale may each data class be, and what happens when an invalidation is lost? This decides the delivery option in the consistency model.
6. Is any cached data security-sensitive (authorization decisions, per-tenant data)? That changes the TTL and key rules.

## Choose a mode

| User intent | What to do |
|---|---|
| **Implement** | Run Preflight, choose the delivery option, then follow Steps 1-5. |
| **Migrate** | Run Preflight, then *Migrating from HybridCache*, then Steps 1-5 for what changes. |
| **Review** | Compare the existing code with the consistency model, the Step 4 rules, and the checklist. Report findings by severity; do not rewrite working code unprompted. |
| **Diagnose** (stale data, missed invalidations) | Start from the consistency model: which scenario (lost notification, lost generation, failed callback, race with in-flight factory) matches the symptoms; use the Step 5 test for it to reproduce. |
| **Compare** | Present the delivery options table and the trade-offs; let the user choose. |


## Model in brief

- FusionCache keeps an in-process L1 in front of Redis (L2) and uses a Redis Pub/Sub **backplane** so a write on one node drops the L1 of the others. Pub/Sub is **at-most-once**: a lost notification is repaired by a shared **generation counter** that each node's reconciler checks, flushing only its local L1 on a gap or reconnect.
- Invalidation must run **after the commit**: the command handler returns `CacheInvalidationRequested`, the Wolverine outbox holds it until commit, and a durable local queue retries it.
- Flow: command handler -> outbox -> `CacheInvalidationRequested` handler -> `ICacheInvalidator` -> `FusionCacheInvalidator` (INCR generation, remove keys/tags, publish generation).
- Application holds ports, messages, keys and `IFusionCache` reads; Infrastructure holds Redis, the invalidator, tracker, reconciler and the Wolverine handler; the host calls `AddCaching(...)`.
- Delivery options: **A** backplane + reconciliation + outbox (default), **B** same without the reconciler, **C** Redis Streams fan-out (not implemented).

Details: [`references/consistency-model.md`](references/consistency-model.md) (scenario table and options), [`references/architecture.md`](references/architecture.md) (diagram and layer placement).

## Steps

Each step names the reference to read and the code to copy. Read a reference only when you reach its step.

| Step | Read | Copy / adapt |
|---|---|---|
| 1. Packages and versions | [`packages-and-compat.md`](references/packages-and-compat.md) | `dotnet add package` commands in that file |
| 2. Application layer (ports, keys, read handlers, command handler) | [`application-layer.md`](references/application-layer.md) | `assets/application/*.cs` |
| 3a-3d. Options, registration, invalidator, generation tracker | [`infrastructure-layer.md`](references/infrastructure-layer.md) | `assets/infrastructure/{CachingOptions,AddCaching,FusionCacheInvalidator,GenerationTracker}.cs` |
| 3e. Reconciler and readiness (skip for option B) | [`reconciler.md`](references/reconciler.md) | `assets/infrastructure/{CacheReconciler,CacheReconcilerHealthCheck}.cs` |
| 3f. Wolverine handler, routing, outbox | [`wolverine-integration.md`](references/wolverine-integration.md) | `assets/wolverine/*.cs` |
| 4. Design rules and pitfalls | [`pitfalls.md`](references/pitfalls.md) | - |
| Migrating from HybridCache | [`migrate-from-hybridcache.md`](references/migrate-from-hybridcache.md) | - |
| 5. Verification (failure and recovery matrix, unit tests) | [`verification-matrix.md`](references/verification-matrix.md) | - |

## Quick checklist when implementing or reviewing

- [ ] Preflight done: real versions, Redis topology, Wolverine/EF Core presence and same-database outbox, per-data-class staleness policy, and the chosen delivery option (A or B) are recorded. If migrating, the plan in *Migrating from HybridCache* is followed (new `KeyPrefix` version, rollout window decided).
- [ ] Application has only ports/messages/keys plus `IFusionCache` reads; no `StackExchange.Redis`, no backplane or serializer package, no registration code.
- [ ] One shared `IConnectionMultiplexer` feeds L2, the backplane, and the generation pub/sub; `AbortOnConnectFail=false`, bounded connect/sync/async timeouts, and a `ClientName`.
- [ ] `KeyPrefix` and `ChannelPrefix` are configured per service and environment (no defaults) and validated at startup; the Redis `maxmemory-policy` cannot evict the generation key (and is monitored).
- [ ] Cached values are immutable DTOs (deeply immutable, serializable); keys/tags centralized in `CacheKeys` / `CacheTags`; list queries carry either every relevant element tag or a coarse tag that every relevant mutation invalidates; not-found (`null`) results have an explicit, short policy; no over-tagging.
- [ ] Durations come from `CachingOptions`; `MemoryCacheDuration <= Duration`; the staleness bound per data class is written down; per-entry overrides exist only where the data needs a different policy.
- [ ] Commands return `CacheInvalidationRequested` from a transactional handler (post-commit via outbox), the outbox shares the business transaction, and code outside handlers uses `IDbContextOutbox<T>` (never publish-after-save without stating the loss window).
- [ ] The invalidator increments the generation first, removes keys/tags through `IFusionCache`, then publishes the generation; `SkipBackplaneNotifications` is not enabled by default.
- [ ] (Option A) The reconciler is wired: reconnect, malformed event, overflow, and generation gap all lead to a debounced **local-only** `ClearAsync` (skip backplane, skip L2 read and write); the default `ClearAsync()` is never used for repair; its cold-start cost is accepted; no `async void`.
- [ ] The reconciler is a resolvable singleton; readiness (tagged `ready`) reflects its state; liveness does not depend on it; the meter is registered with OpenTelemetry and the metrics are alerted on.
- [ ] Redis ACL grants only the commands/keys/channels the service needs (including the FusionCache backplane channel); credentials are not in source.
- [ ] Two-host Testcontainers test passes **and** the failure and recovery matrix (Step 5) covers the blind spot, gap with no event, reconnect, flush locality, rolling deploy, in-flight factory, and outbox crash.

## References

- FusionCache, Backplane (notification behavior, packages, registration): https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Backplane.md
- FusionCache, Tagging (`RemoveByTag`, markers, incoherence without a backplane, `CacheKeyPrefix`): https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/Tagging.md
- FusionCache, Auto-Recovery (retry of failed sends, in-process queue): https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/AutoRecovery.md
- FusionCache, Microsoft HybridCache adapter (`AsHybridCache`): https://github.com/ZiggyCreatures/FusionCache/blob/main/docs/MicrosoftHybridCache.md
- Microsoft, HybridCache in ASP.NET Core (tags, local invalidation not reaching other servers' memory cache): https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0
- dotnet/extensions issue #7411 (open): local tag-invalidation timestamps are not refreshed across instances; reported on 10.0.0 and 10.11.0: https://github.com/dotnet/extensions/issues/7411
- Redis, Pub/Sub (at-most-once delivery) and Streams (stronger delivery semantics): https://redis.io/docs/latest/develop/pubsub/
- Wolverine, Redis transport (streams, listeners, consumer groups): https://wolverinefx.net/guide/messaging/transports/redis
- Wolverine, local queues (durable local queues, routing applies to cascading messages): https://wolverinefx.io/guide/messaging/transports/local.html
- Wolverine, EF Core transactional middleware and outbox: https://wolverinefx.net/guide/durability/efcore/transactional-middleware


Verification status: the FusionCache statements tagged [Verified] were read from the pages above on 2026-11-22. The pages state no explicit delivery guarantee for the backplane and do not say that a disconnected subscriber's missed notifications are replayed; that is why this skill does not rely on it. The Microsoft, Redis, and issue #7411 statements were re-checked on 2026-11-21. The Wolverine pages were **not** re-opened in the latest review; re-verify them when pinning versions. None of this replaces a build and the Step 5 tests in the target solution.
