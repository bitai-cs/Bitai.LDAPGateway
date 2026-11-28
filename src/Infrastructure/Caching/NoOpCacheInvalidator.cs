using Bitai.LDAPGateway.Application.Common.Caching;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Invalidator used when no cache level is active: there is nothing to invalidate.
/// </summary>
internal sealed class NoOpCacheInvalidator : ICacheInvalidator
{
    public Task InvalidateAsync(
        IReadOnlyCollection<string> keys,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
