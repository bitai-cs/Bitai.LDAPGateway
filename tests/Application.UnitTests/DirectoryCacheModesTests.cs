using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Common.Models;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bitai.LDAPGateway.Application.UnitTests;

/// <summary>Behavior of <see cref="DirectoryCache"/> for each active cache mode. The shared L2 is a MemoryDistributedCache.</summary>
public sealed class DirectoryCacheModesTests
{
    private static IDistributedCache NewL2() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    private static DirectoryCache NewNode(IDistributedCache l2, CacheMode mode)
    {
        var fusion = new FusionCache(new FusionCacheOptions
        {
            CacheKeyPrefix = "modes:",
            TagsDefaultEntryOptions = new FusionCacheEntryOptions { SkipMemoryCacheRead = true, SkipMemoryCacheWrite = true }
        });
        fusion.SetupDistributedCache(l2, new FusionCacheSystemTextJsonSerializer());

        return new DirectoryCache(fusion, Mock.Of<ICacheInvalidator>(), NullLogger<DirectoryCache>.Instance, new CacheLevels(mode));
    }

    private sealed class Counter
    {
        public int Calls;

        public Task<Result<string>> Run(CancellationToken _) => Task.FromResult(Result<string>.Success($"v{++Calls}"));
    }

    private static Task<CachedQueryResult<string>> Read(DirectoryCache cache, Counter counter, string key = "k") =>
        cache.GetOrSetAsync(key, "EDU", CachePolicies.Entry(), false, counter.Run, CancellationToken.None);

    [Fact]
    public async Task None_NeverCaches()
    {
        var cache = NewNode(NewL2(), CacheMode.None);
        var counter = new Counter();

        var first = await Read(cache, counter);
        var second = await Read(cache, counter);

        Assert.Equal(2, counter.Calls);
        Assert.False(first.ServedFromCache);
        Assert.False(second.ServedFromCache);
        Assert.Equal("v2", second.Result.Value);
    }

    [Fact]
    public async Task None_ReturnsTheFailureWithoutCachingIt()
    {
        var cache = NewNode(NewL2(), CacheMode.None);
        var calls = 0;
        Task<Result<string>> Fail(CancellationToken _)
        {
            calls++;
            return Task.FromResult(Result<string>.Failure(new Error("bad_gateway", "down", 502)));
        }

        var first = await cache.GetOrSetAsync("k", "EDU", CachePolicies.Entry(), false, Fail, CancellationToken.None);
        await cache.GetOrSetAsync("k", "EDU", CachePolicies.Entry(), false, Fail, CancellationToken.None);

        Assert.False(first.Result.IsSuccess);
        Assert.Equal(502, first.Result.Error!.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task MemoryOnly_CachesLocally_ButDoesNotShareWithOtherNodes()
    {
        var l2 = NewL2();
        var nodeA = NewNode(l2, CacheMode.MemoryOnly);
        var nodeB = NewNode(l2, CacheMode.MemoryOnly);
        var counter = new Counter();

        await Read(nodeA, counter);
        var again = await Read(nodeA, counter);
        Assert.True(again.ServedFromCache);
        Assert.Equal(1, counter.Calls);

        await Read(nodeB, counter);
        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task DistributedOnly_IsSharedAcrossNodes()
    {
        var l2 = NewL2();
        var nodeA = NewNode(l2, CacheMode.DistributedOnly);
        var nodeB = NewNode(l2, CacheMode.DistributedOnly);
        var counter = new Counter();

        await Read(nodeA, counter);
        var onB = await Read(nodeB, counter);

        Assert.True(onB.ServedFromCache);
        Assert.Equal("v1", onB.Result.Value);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task DistributedOnly_LargeResultGuard_SkipsEveryLevel()
    {
        var cache = NewNode(NewL2(), CacheMode.DistributedOnly);
        var counter = new Counter();

        for (var i = 0; i < 2; i++)
        {
            await cache.GetOrSetAsync("k", "EDU", CachePolicies.Entry(), false, counter.Run, CancellationToken.None,
                allowDistributedCache: _ => false);
        }

        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public async Task Hybrid_IsSharedAcrossNodes_AndCachesLocally()
    {
        var l2 = NewL2();
        var nodeA = NewNode(l2, CacheMode.Hybrid);
        var nodeB = NewNode(l2, CacheMode.Hybrid);
        var counter = new Counter();

        await Read(nodeA, counter);
        await Read(nodeA, counter);
        var onB = await Read(nodeB, counter);

        Assert.True(onB.ServedFromCache);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task WithoutCacheLevels_DefaultsToHybrid()
    {
        var fusion = new FusionCache(new FusionCacheOptions());
        var cache = new DirectoryCache(fusion, Mock.Of<ICacheInvalidator>(), NullLogger<DirectoryCache>.Instance);
        var counter = new Counter();

        await Read(cache, counter);
        await Read(cache, counter);

        Assert.Equal(1, counter.Calls);
    }
}
