namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Cache levels that are active for the directory reads.
/// </summary>
public enum CacheMode
{
    /// <summary>No cache: every read goes to the directory.</summary>
    None,

    /// <summary>Only the in-process memory cache (L1).</summary>
    MemoryOnly,

    /// <summary>Only the shared distributed cache (L2).</summary>
    DistributedOnly,

    /// <summary>L1 in front of the shared L2, kept coherent through the backplane.</summary>
    Hybrid
}
