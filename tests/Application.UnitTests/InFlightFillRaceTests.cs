using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bitai.LDAPGateway.Application.UnitTests;

/// <summary>
/// A read that started before an invalidation and finishes after it must not leave its pre-mutation value cached.
/// </summary>
public sealed class InFlightFillRaceTests
{
    // Same tag configuration as production: tag metadata skips memory only in the L2-only mode.
    private static IFusionCache NewNode(IDistributedCache? l2, bool tagsSkipMemory = false)
    {
        var options = new FusionCacheOptions { CacheKeyPrefix = "race:" };
        if (tagsSkipMemory)
        {
            options.TagsDefaultEntryOptions = new FusionCacheEntryOptions { SkipMemoryCacheRead = true, SkipMemoryCacheWrite = true };
        }

        var cache = new FusionCache(options);
        if (l2 is not null)
        {
            cache.SetupDistributedCache(l2, new FusionCacheSystemTextJsonSerializer());
        }

        return cache;
    }

    private static async Task<string> RunRace(IFusionCache cache, FusionCacheEntryOptions options)
    {
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var slowRead = cache.GetOrSetAsync<string>("k", async (_, _) =>
        {
            started.SetResult();
            await release.Task;
            return "old";
        }, options: options, tags: ["t"]).AsTask();

        await started.Task;
        await cache.RemoveByTagAsync("t");   // the mutation succeeded and invalidated the profile
        release.SetResult();
        await slowRead;                      // the pre-mutation read finishes after the invalidation

        return await cache.GetOrSetAsync<string>("k", _ => Task.FromResult("new"), options: options, tags: ["t"]);
    }

    private static readonly FusionCacheEntryOptions Both = new()
    {
        Duration = TimeSpan.FromMinutes(1),
        DistributedCacheDuration = TimeSpan.FromMinutes(1)
    };

    private static readonly FusionCacheEntryOptions L2Only = new()
    {
        Duration = TimeSpan.FromMinutes(1),
        DistributedCacheDuration = TimeSpan.FromMinutes(1),
        SkipMemoryCacheRead = true,
        SkipMemoryCacheWrite = true
    };

    [Fact]
    public async Task L2Only_PreMutationFill_IsNotServedAfterInvalidation()
        => Assert.Equal("new", await RunRace(NewNode(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), tagsSkipMemory: true), L2Only));

    [Fact]
    public async Task Hybrid_PreMutationFill_IsNotServedAfterInvalidation()
        => Assert.Equal("new", await RunRace(NewNode(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()))), Both));

    [Fact]
    public async Task MemoryOnly_PreMutationFill_IsNotServedAfterInvalidation()
        => Assert.Equal("new", await RunRace(NewNode(null), Both));
}
