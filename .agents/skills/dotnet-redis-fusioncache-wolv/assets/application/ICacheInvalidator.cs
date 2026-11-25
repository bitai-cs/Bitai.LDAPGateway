// Application/Common/Caching/ICacheInvalidator.cs
namespace MyService.Application.Common.Caching;

public interface ICacheInvalidator
{
    /// Invalidate on this node AND every other node (L1 + L2).
    Task InvalidateAsync(IReadOnlyCollection<string> keys,
                         IReadOnlyCollection<string> tags,
                         CancellationToken ct = default);
}
