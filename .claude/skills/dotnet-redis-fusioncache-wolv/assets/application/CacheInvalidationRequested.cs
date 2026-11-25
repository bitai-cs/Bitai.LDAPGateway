// Application/Common/Caching/CacheInvalidationRequested.cs
namespace MyService.Application.Common.Caching;

public sealed record CacheInvalidationRequested(string[] Keys, string[] Tags)
{
    public static CacheInvalidationRequested ForTags(params string[] tags) => new([], tags);
    public static CacheInvalidationRequested ForKeys(params string[] keys) => new(keys, []);
}
