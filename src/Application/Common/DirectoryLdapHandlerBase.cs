using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Common.Interfaces;
using Bitai.LDAPGateway.Application.Common.Models;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Application.Common;

/// <summary>
/// Base class for the directory handlers that participate in the read cache and in its invalidation.
/// </summary>
public abstract class DirectoryLdapHandlerBase : LdapHandlerBase
{
    private readonly DirectoryCache _cache;

    protected DirectoryLdapHandlerBase(IDomainEventPublisher domainEventPublisher, DirectoryCache cache)
        : base(domainEventPublisher)
    {
        _cache = cache;
    }

    /// <summary>Executes a cached directory query and publishes the operation-completed event.</summary>
    protected async Task<Result<T>> ExecuteCachedQueryAsync<T>(
        string operationName,
        LdapRequestContext context,
        string cacheKey,
        FusionCacheEntryOptions entryOptions,
        bool cacheNullAsNotFound,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken,
        Func<T, bool>? allowDistributedCache = null)
    {
        var cached = await _cache.GetOrSetAsync(
            cacheKey,
            context.ServerProfile,
            entryOptions,
            cacheNullAsNotFound,
            operation,
            cancellationToken,
            allowDistributedCache);

        await PublishCompletedAsync(
            operationName,
            context,
            cached.Result.IsSuccess,
            cached.Result,
            cancellationToken,
            cached.ServedFromCache ? "Operation completed (cache hit)" : null);

        return cached.Result;
    }

    /// <summary>
    /// Executes a directory mutation and invalidates the cached reads of its profile when it succeeds.
    /// </summary>
    protected async Task<Result<T>> ExecuteMutationAsync<T>(
        string operationName,
        LdapRequestContext context,
        Func<Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var result = await operation();

        if (result.IsSuccess)
        {
            await _cache.InvalidateProfileAsync(operationName, context.ServerProfile);
        }

        await PublishCompletedAsync(operationName, context, result.IsSuccess, result, cancellationToken);

        return result;
    }

    /// <summary>
    /// Executes a directory mutation without a payload and invalidates the cached reads of its profile when it succeeds.
    /// </summary>
    protected async Task<Result> ExecuteMutationAsync(
        string operationName,
        LdapRequestContext context,
        Func<Task<Result>> operation,
        CancellationToken cancellationToken)
    {
        var result = await operation();

        if (result.IsSuccess)
        {
            await _cache.InvalidateProfileAsync(operationName, context.ServerProfile);
        }

        await PublishCompletedAsync(operationName, context, result.IsSuccess, result, cancellationToken);

        return result;
    }
}
