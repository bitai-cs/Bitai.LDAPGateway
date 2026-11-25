public sealed record UpdateProductPriceCommand(Guid Id, decimal NewPrice);

public static class UpdateProductPriceHandler
{
    // With AutoApplyTransactions (or [Transactional]) + EF Core outbox, the returned
    // message is only released AFTER the DB transaction commits.
    public static async Task<CacheInvalidationRequested> Handle(
        UpdateProductPriceCommand cmd, AppDbContext db, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([cmd.Id], ct)
                      ?? throw new ProductNotFoundException(cmd.Id);

        product.ChangePrice(cmd.NewPrice);
        // SaveChanges is performed by Wolverine's transactional middleware.

        return CacheInvalidationRequested.ForTags(CacheTags.Product(cmd.Id), CacheTags.Products);
    }
}
