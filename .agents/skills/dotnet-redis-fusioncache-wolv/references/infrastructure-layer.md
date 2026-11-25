## Step 3: Infrastructure

There is **no custom cross-node invalidation payload or subscriber protocol** any more: what a remote node must drop travels in FusionCache's own notification, and the generation event is just a number. That removes the schema versioning, batching, validation, and bounded-inbox code that a hand-written backplane needs. The application-level `CacheInvalidationRequested` outbox message (Step 2) remains; it never crosses nodes.

### 3a. Options

> **Code:** [`assets/infrastructure/CachingOptions.cs`](../assets/infrastructure/CachingOptions.cs)

Configuration must supply the environment-scoped names; there are no safe defaults for them.

```json
{
  "Caching": {
    "RedisConnectionString": "redis-prod:6380,ssl=true,abortConnect=false",
    "KeyPrefix": "orders:prod:v1:",
    "ChannelPrefix": "orders:prod",
    "Duration": "00:30:00",
    "MemoryCacheDuration": "00:02:00",
    "ReconcileInterval": "00:00:30"
  }
}
```

### 3b. Registration

> **Code:** [`assets/infrastructure/AddCaching.cs`](../assets/infrastructure/AddCaching.cs)

Notes on the registration:
- Entry durations: `Duration` is the logical duration and is what L1 uses; `DistributedCacheDuration` overrides it for L2. This is the equivalent of HybridCache's `LocalCacheExpiration` (L1) and `Expiration` (L2).
- FusionCache sends backplane notifications on `Set`, `Remove`, `Expire`, and when a `GetOrSet` factory runs [Verified, see References]; `SkipBackplaneNotifications` defaults to `false` [Verified]. Do not flip that default globally.
- Reuse of the shared multiplexer by the backplane was exercised in the scratch run (two hosts, notifications delivered).
- Redis down at startup: `AbortOnConnectFail=false` keeps the host alive; FusionCache logs backplane errors and retries (auto-recovery is on by default in 2.x [Assumption: check `EnableAutoRecovery` in the version used]).

In the host, expose readiness and liveness separately and register the meter. Liveness MUST NOT depend on the cache reconciler, otherwise a Redis outage restarts every pod:

```csharp
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")      // includes cache-reconciler
});
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });  // process is up

// Without this the cache metrics are never exported:
builder.Services.AddOpenTelemetry().WithMetrics(m => m.AddMeter("MyService.Caching"));
```

```yaml
# Kubernetes (container spec): Unhealthy -> 503 -> pod removed from Service endpoints.
# Degraded -> 200 by default, so the pod stays in rotation; change ResultStatusCodes if the business needs otherwise.
readinessProbe:
  httpGet: { path: /health/ready, port: 8080 }
  periodSeconds: 5
  failureThreshold: 3
livenessProbe:
  httpGet: { path: /health/live, port: 8080 }
  periodSeconds: 10
  failureThreshold: 3
```

A health endpoint alone does not retire a pod: it only reports state. The probe configuration above is what makes the cluster act on it; test that mapping (Step 5).

### 3c. Invalidator (applies locally, FusionCache broadcasts, then announces the generation)

> **Code:** [`assets/infrastructure/FusionCacheInvalidator.cs`](../assets/infrastructure/FusionCacheInvalidator.cs)

The operation is idempotent: if Wolverine retries the handler after a Redis failure, the removals repeat and a new generation is issued. A retried attempt leaves a skipped generation, which makes the other nodes confirm a gap and flush once; that is a conservative cost, not a correctness problem. This applies to **any partial failure after the `INCR`** (a failed key removal, a failed tag removal, a failed generation publish), not only to a process crash. Wolverine retries only the exception types configured in Step 3f; verify which exception types the pinned Redis/FusionCache versions actually surface (FusionCache may swallow backplane errors by design), and test a failure injected after each operation (Step 5). FusionCache has no per-batch limit to respect, so there is no batching code.

### 3d. Generation tracker

> **Code:** [`assets/infrastructure/GenerationTracker.cs`](../assets/infrastructure/GenerationTracker.cs)
