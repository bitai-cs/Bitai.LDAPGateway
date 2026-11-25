using Microsoft.Extensions.Diagnostics.HealthChecks;

// Infrastructure/Caching/CacheReconcilerHealthCheck.cs
internal sealed class CacheReconcilerHealthCheck(CacheReconciler reconciler) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(!reconciler.HasSubscribed
            ? HealthCheckResult.Unhealthy("Generation subscription was never established.")
            : !reconciler.IsReady
                ? HealthCheckResult.Degraded("Redis is unavailable; reconciliation runs on reconnect.")
                : reconciler.IsReconciling
                    ? HealthCheckResult.Degraded("A local flush is pending; this node may briefly serve stale entries.")
                    : HealthCheckResult.Healthy());
}
