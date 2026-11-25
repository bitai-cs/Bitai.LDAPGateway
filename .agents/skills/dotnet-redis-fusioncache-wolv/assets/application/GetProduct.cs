using ZiggyCreatures.Caching.Fusion;

public sealed record GetProductQuery(Guid Id);
public sealed record ProductDto(Guid Id, string Name, decimal Price); // immutable, serializable

public static class GetProductHandler
{
    // No per-handler TTL literals: DefaultEntryOptions comes from CachingOptions.
    // Pass explicit FusionCacheEntryOptions only when this data needs a different
    // freshness policy than the default.
    //
    // Null policy [Design]: a not-found result IS cached (FusionCache caches null by
    // default [Assumption: confirm for the pinned version]), but only for a short,
    // explicit time using adaptive caching (the factory adjusts ctx.Options), so misses
    // do not hammer the database and a later insert becomes visible quickly.
    public static async Task<ProductDto?> Handle(
        GetProductQuery query, IFusionCache cache, IProductReadRepository repo, CancellationToken ct)
        => await cache.GetOrSetAsync<ProductDto?>(
            CacheKeys.Product(query.Id),
            async (ctx, token) =>
            {
                var dto = await repo.FindDtoAsync(query.Id, token);
                if (dto is null)
                {
                    ctx.Options.Duration = CachePolicies.NotFoundDuration;                  // L1
                    ctx.Options.DistributedCacheDuration = CachePolicies.NotFoundDuration;  // L2
                }
                return dto;
            },
            tags: [CacheTags.Products, CacheTags.Product(query.Id)],
            token: ct);
}

// Application/Common/Caching/CachePolicies.cs  [Design] a named policy, not a per-handler literal
public static class CachePolicies
{
    public static readonly TimeSpan NotFoundDuration = TimeSpan.FromSeconds(30);
}
