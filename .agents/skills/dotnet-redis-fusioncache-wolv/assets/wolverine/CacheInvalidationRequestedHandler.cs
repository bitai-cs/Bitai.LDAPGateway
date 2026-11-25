// Infrastructure/Caching/CacheInvalidationRequestedHandler.cs
public static class CacheInvalidationRequestedHandler
{
    public static Task Handle(CacheInvalidationRequested msg, ICacheInvalidator invalidator, CancellationToken ct)
        => invalidator.InvalidateAsync(msg.Keys, msg.Tags, ct);
}
