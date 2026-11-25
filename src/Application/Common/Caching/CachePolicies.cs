using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Freshness policies for the cached LDAP reads. L1 lives in the process memory, L2 in Redis.
/// </summary>
public static class CachePolicies
{
    /// <summary>Status code used by the application to signal a missing directory entry.</summary>
    public const int NotFoundStatusCode = 404;

    /// <summary>L1 duration for a not-found result.</summary>
    public static readonly TimeSpan NotFoundL1Duration = TimeSpan.FromSeconds(10);

    /// <summary>L2 duration for a not-found result.</summary>
    public static readonly TimeSpan NotFoundL2Duration = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum number of entries that a single search result may contain to be written to Redis.
    /// Larger results are still cached in the node's L1 but not in the shared L2 (bounded memory).
    /// </summary>
    public const int MaxEntriesToCache = 200;

    /// <summary>Policy for search results (potentially many entries).</summary>
    public static FusionCacheEntryOptions Search() => Create(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));

    /// <summary>Policy for single-entry lookups.</summary>
    public static FusionCacheEntryOptions Entry() => Create(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));

    /// <summary>Policy for parent/group membership walks, which are more expensive and change less often.</summary>
    public static FusionCacheEntryOptions Parents() => Create(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));

    /// <summary>Applies the short not-found durations to the current factory execution.</summary>
    public static void ApplyNotFound(FusionCacheEntryOptions options)
    {
        options.Duration = NotFoundL1Duration;
        options.DistributedCacheDuration = NotFoundL2Duration;
    }

    private static FusionCacheEntryOptions Create(TimeSpan l1, TimeSpan l2) => new()
    {
        Duration = l1,
        DistributedCacheDuration = l2
    };
}
