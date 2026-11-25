using Bitai.LDAPGateway.Application.Common.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Invalidator used when the Redis L2 is disabled: it removes the entries from this node's L1 only.
/// </summary>
/// <remarks>
/// Read-your-writes still holds on the node that performed the mutation. Other nodes do not exist in this mode;
/// if several instances run with the cache disabled, they will only converge through the TTL.
/// </remarks>
internal sealed class LocalCacheInvalidator : ICacheInvalidator
{
    private readonly IFusionCache _cache;

    public LocalCacheInvalidator(IFusionCache cache)
    {
        _cache = cache;
    }

    public async Task InvalidateAsync(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        foreach (var key in keys.Distinct())
        {
            await _cache.RemoveAsync(key, token: cancellationToken);
        }

        if (tags.Count > 0)
        {
            await _cache.RemoveByTagAsync(tags.Distinct().ToArray(), token: cancellationToken);
        }
    }
}
