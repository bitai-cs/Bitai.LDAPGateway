// Application/Common/Caching/CacheKeys.cs  (single source of truth for names)
namespace MyService.Application.Common.Caching;

public static class CacheKeys
{
    public const int MaxLength = 1024;   // a convention of this skill, not a FusionCache limit

    // Fail where the key is built instead of caching under an unbounded key.
    private static string Key(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= MaxLength
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), $"Cache key exceeds {MaxLength} characters.");
    }

    public static string Product(Guid id) => Key($"product:{id:N}");
    public static string ProductList(int page, int size) => Key($"products:list:{page}:{size}");
}

public static class CacheTags
{
    public const string Products = "products";                 // all product entries
    public static string Product(Guid id) => $"product:{id:N}"; // one product; a list is affected only if it was also written with this tag
}
