using Wolverine.EntityFrameworkCore;

// Conceptual example: verify IDbContextOutbox<T> and SaveChangesAndFlushMessagesAsync against the Wolverine version in use.
app.MapPost("/products/{id:guid}/price", async (
    Guid id, PriceRequest request, IDbContextOutbox<AppDbContext> outbox, CancellationToken ct) =>
{
    var product = await outbox.DbContext.Products.FindAsync([id], ct);
    if (product is null) return Results.NotFound();

    product.ChangePrice(request.NewPrice);
    await outbox.PublishAsync(CacheInvalidationRequested.ForTags(CacheTags.Product(id), CacheTags.Products));
    await outbox.SaveChangesAndFlushMessagesAsync(ct);   // data + outbox rows commit together
    return Results.NoContent();
});
