using Bitai.LDAPGateway.Application.Common.Caching;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Invalidator used when only the Redis L2 is active (no L1, no backplane, no reconciler).
/// </summary>
/// <remarks>
/// Redis is the single source of truth, so removing the entries and the tags from it is enough for every node.
/// The tag metadata is never kept in memory (see <c>TagsDefaultEntryOptions</c> in the registration).
/// </remarks>
internal sealed class DistributedCacheInvalidator : ICacheInvalidator
{
    private readonly IFusionCache _cache;

    public DistributedCacheInvalidator(IFusionCache cache)
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
