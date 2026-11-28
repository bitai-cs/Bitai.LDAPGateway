namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Tells the application which cache levels are active. The infrastructure resolves it from configuration.
/// </summary>
public interface ICacheLevels
{
    /// <summary>The active cache mode.</summary>
    CacheMode Mode { get; }
}

/// <summary>Default <see cref="ICacheLevels"/> implementation.</summary>
public sealed record CacheLevels(CacheMode Mode) : ICacheLevels;
