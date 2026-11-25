using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Readiness check for the cache reconciler. It is tagged <c>ready</c> so the liveness probe never depends on Redis.
/// </summary>
internal sealed class CacheReconcilerHealthCheck : IHealthCheck
{
    private readonly CacheReconciler _reconciler;

    public CacheReconcilerHealthCheck(CacheReconciler reconciler)
    {
        _reconciler = reconciler;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(!_reconciler.HasSubscribed
            ? HealthCheckResult.Unhealthy("Generation subscription was never established.")
            : !_reconciler.IsReady
                ? HealthCheckResult.Degraded("Redis is unavailable; reconciliation runs on reconnect.")
                : _reconciler.IsReconciling
                    ? HealthCheckResult.Degraded("A local flush is pending; this node may briefly serve stale entries.")
                    : HealthCheckResult.Healthy());
}
