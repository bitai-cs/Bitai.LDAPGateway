using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

// Infrastructure/Caching/FusionCacheInvalidator.cs
internal sealed class FusionCacheInvalidator(
    IFusionCache cache,
    IConnectionMultiplexer redis,
    IOptions<CachingOptions> options,
    ILogger<FusionCacheInvalidator> logger) : ICacheInvalidator
{
    public async Task InvalidateAsync(IReadOnlyCollection<string> keys,
                                      IReadOnlyCollection<string> tags,
                                      CancellationToken ct = default)
    {
        // 1) Increment BEFORE invalidating. If the process dies (or a call fails) after
        //    the INCR, subscribers see a generation they never received and reconcile.
        //    NOTE: StackExchange.Redis calls (INCR, PUBLISH) take no CancellationToken, so `ct`
        //    does not cancel them; their upper bound is AsyncTimeout. Check `ct` between steps.
        ct.ThrowIfCancellationRequested();
        var generation = await redis.GetDatabase().StringIncrementAsync(options.Value.GenerationKey);

        // 2) Clears L1 + L2 here; FusionCache's backplane tells the other nodes.
        foreach (var key in keys.Distinct()) await cache.RemoveAsync(key, token: ct);
        if (tags.Count > 0) await cache.RemoveByTagAsync(tags.Distinct().ToArray(), token: ct);

        // 3) Announce the generation AFTER the removals were published, so a node that
        //    sees generation N knows invalidation N has already been sent.
        var receivers = await redis.GetSubscriber()
            .PublishAsync(RedisChannel.Literal(options.Value.GenerationChannel), generation);

        // Counts only: raw keys/tags can carry tenant identifiers. `receivers` is the number
        // of connected subscribers at publish time, NOT a confirmation that they applied anything.
        logger.LogDebug(
            "Cache invalidation {Generation} applied (keys={KeyCount}, tags={TagCount}); generation seen by {Receivers} subscribers",
            generation, keys.Count, tags.Count, receivers);
    }
}
