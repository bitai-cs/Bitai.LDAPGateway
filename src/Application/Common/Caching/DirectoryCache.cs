using Bitai.LDAPGateway.Application.Common.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Application.Common.Caching;

/// <summary>
/// Cache façade used by the directory query and command handlers.
/// </summary>
/// <remarks>
/// Reads go through FusionCache (L1 and/or Redis L2 + backplane, depending on <see cref="ICacheLevels"/>);
/// with no active level they go straight to the directory. Writes invalidate the coarse profile tag
/// after the LDAP operation succeeded. Infrastructure failures are never cached.
/// </remarks>
public sealed class DirectoryCache
{
    private readonly IFusionCache _cache;
    private readonly ICacheInvalidator _invalidator;
    private readonly ILogger<DirectoryCache> _logger;
    private readonly CacheMode _mode;

    public DirectoryCache(
        IFusionCache cache,
        ICacheInvalidator invalidator,
        ILogger<DirectoryCache> logger,
        ICacheLevels? levels = null)
    {
        _cache = cache;
        _invalidator = invalidator;
        _logger = logger;
        _mode = levels?.Mode ?? CacheMode.Hybrid;
    }

    /// <summary>
    /// Applies the active cache levels to a copy of the policy: the levels that are off are skipped per entry.
    /// </summary>
    private FusionCacheEntryOptions ApplyLevels(FusionCacheEntryOptions entryOptions)
    {
        if (_mode == CacheMode.Hybrid)
        {
            return entryOptions;
        }

        var options = entryOptions.Duplicate();
        if (_mode == CacheMode.DistributedOnly)
        {
            options.SkipMemoryCacheRead = true;
            options.SkipMemoryCacheWrite = true;
        }
        else if (_mode == CacheMode.MemoryOnly)
        {
            options.SkipDistributedCacheRead = true;
            options.SkipDistributedCacheWrite = true;
        }

        return options;
    }

    /// <summary>
    /// Returns the cached result for <paramref name="cacheKey"/> or runs <paramref name="operation"/> and caches it.
    /// </summary>
    /// <param name="cacheNullAsNotFound">
    /// When <see langword="true"/>, a successful operation with a <see langword="null"/> value is stored with the
    /// short not-found policy (used by the detail queries, where a missing entry is not an error).
    /// </param>
    /// <param name="allowDistributedCache">
    /// Optional guard evaluated after the operation completes. Returning <see langword="false"/> keeps the value
    /// in this node's L1 only (used for search results that are too large to share through Redis).
    /// </param>
    public async Task<CachedQueryResult<T>> GetOrSetAsync<T>(
        string cacheKey,
        string serverProfile,
        FusionCacheEntryOptions entryOptions,
        bool cacheNullAsNotFound,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken,
        Func<T, bool>? allowDistributedCache = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheKey);
        ArgumentNullException.ThrowIfNull(entryOptions);
        ArgumentNullException.ThrowIfNull(operation);

        if (_mode == CacheMode.None)
        {
            return new CachedQueryResult<T>(await operation(cancellationToken), ServedFromCache: false);
        }

        var servedFromCache = true;

        CachedResult<T> entry;
        try
        {
            entry = await _cache.GetOrSetAsync<CachedResult<T>>(
                cacheKey,
                async (context, token) =>
                {
                    servedFromCache = false;
                    var result = await operation(token);

                    if (result.IsSuccess)
                    {
                        if (cacheNullAsNotFound && result.Value is null)
                        {
                            CachePolicies.ApplyNotFound(context.Options);
                        }

                        if (allowDistributedCache is not null && result.Value is { } value && !allowDistributedCache(value))
                        {
                            context.Options.SkipDistributedCacheWrite = true;
                        }

                        return CachedResult<T>.Success(result.Value);
                    }

                    var error = result.Error ?? Error.Internal("LDAP operation failed without an error.");
                    if (error.StatusCode == CachePolicies.NotFoundStatusCode)
                    {
                        CachePolicies.ApplyNotFound(context.Options);
                        return CachedResult<T>.Failure(error);
                    }

                    // Infrastructure failures must not be cached: leave the factory without a value.
                    throw new CacheBypassException(error);
                },
                options: ApplyLevels(entryOptions),
                tags: [CacheTags.Profile(serverProfile)],
                token: cancellationToken);
        }
        catch (Exception ex) when (TryGetBypass(ex, out var bypass))
        {
            entry = CachedResult<T>.Failure(bypass.Error);
        }

        return new CachedQueryResult<T>(entry.ToResult(), servedFromCache);
    }

    /// <summary>
    /// Finds the bypass signal even when FusionCache wraps the factory exception in one of its own types.
    /// </summary>
    private static bool TryGetBypass(Exception exception, out CacheBypassException bypass)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is CacheBypassException found)
            {
                bypass = found;
                return true;
            }
        }

        bypass = null!;
        return false;
    }

    /// <summary>
    /// Invalidates every cached read of <paramref name="serverProfile"/> on all nodes.
    /// </summary>
    /// <remarks>
    /// Runs with <see cref="CancellationToken.None"/> on purpose: a client that aborts the request must not
    /// skip the invalidation after the directory was already modified. Failures are logged and swallowed
    /// because the command already succeeded; the TTL bounds the staleness window.
    /// </remarks>
    public async Task InvalidateProfileAsync(string operationName, string serverProfile)
    {
        try
        {
            await _invalidator.InvalidateAsync(
                [],
                [CacheTags.Profile(serverProfile)],
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cache invalidation failed after {OperationName} for profile {ServerProfile}; cached reads may be stale until the TTL expires.",
                operationName,
                serverProfile);
        }
    }
}
