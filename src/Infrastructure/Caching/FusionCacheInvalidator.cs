using Bitai.LDAPGateway.Application.Common.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Invalidates a set of entries on this node and on every other node.
/// </summary>
/// <remarks>
/// The generation is incremented before the removals so that a crash or a partial failure after the increment
/// leaves a trace: the other nodes observe a generation they never received and flush their own L1.
/// </remarks>
internal sealed class FusionCacheInvalidator : ICacheInvalidator
{
    private readonly IFusionCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly CachingOptions _options;
    private readonly ILogger<FusionCacheInvalidator> _logger;

    public FusionCacheInvalidator(
        IFusionCache cache,
        IConnectionMultiplexer redis,
        IOptions<CachingOptions> options,
        ILogger<FusionCacheInvalidator> logger)
    {
        _cache = cache;
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvalidateAsync(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // StackExchange.Redis calls take no CancellationToken; their bound is AsyncTimeout.
        var generation = await _redis.GetDatabase().StringIncrementAsync(_options.GenerationKey);

        foreach (var key in keys.Distinct())
        {
            await _cache.RemoveAsync(key, token: cancellationToken);
        }

        if (tags.Count > 0)
        {
            await _cache.RemoveByTagAsync(tags.Distinct().ToArray(), token: cancellationToken);
        }

        var receivers = await _redis
            .GetSubscriber()
            .PublishAsync(RedisChannel.Literal(_options.GenerationChannel), generation);

        // Counts only: raw keys and tags can carry tenant identifiers.
        _logger.LogDebug(
            "Cache invalidation {Generation} applied (keys={KeyCount}, tags={TagCount}); generation seen by {Receivers} subscribers",
            generation,
            keys.Count,
            tags.Count,
            receivers);
    }
}
