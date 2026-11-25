using ZiggyCreatures.Caching.Fusion;

public sealed record GetProductsPageQuery(int Page, int Size);
public sealed record ProductListDto(IReadOnlyList<ProductDto> Items, int Total);   // immutable

public static class GetProductsPageHandler
{
    // A page always exists (possibly empty), so the repository returns a non-null DTO
    // and the null policy above does not apply.
    public static async Task<ProductListDto> Handle(
        GetProductsPageQuery q, IFusionCache cache, IProductReadRepository repo, CancellationToken ct)
        => await cache.GetOrSetAsync<ProductListDto>(
            CacheKeys.ProductList(q.Page, q.Size),
            async (_, token) => await repo.GetPageDtoAsync(q.Page, q.Size, token),
            tags: [CacheTags.Products],     // coarse tag: every product mutation already invalidates it
            token: ct);
}
