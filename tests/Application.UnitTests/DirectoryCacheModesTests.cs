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
    private static SwitchableL2 NewL2() =>
        new(new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())));

    /// <summary>
    /// A distributed cache that can be "cut" mid-test: while disabled every read is a miss and writes are dropped.
    /// It lets a test tell an L1 hit from an L2 hit.
    /// </summary>
    private sealed class SwitchableL2 : IDistributedCache
    {
        private readonly IDistributedCache _inner;

        public SwitchableL2(IDistributedCache inner) => _inner = inner;

        public bool Enabled { get; set; } = true;

        public byte[]? Get(string key) => Enabled ? _inner.Get(key) : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
            => Enabled ? _inner.GetAsync(key, token) : Task.FromResult<byte[]?>(null);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            if (Enabled) _inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
            => Enabled ? _inner.SetAsync(key, value, options, token) : Task.CompletedTask;

        public void Refresh(string key) { if (Enabled) _inner.Refresh(key); }

        public Task RefreshAsync(string key, CancellationToken token = default)
            => Enabled ? _inner.RefreshAsync(key, token) : Task.CompletedTask;

        public void Remove(string key) { if (Enabled) _inner.Remove(key); }

        public Task RemoveAsync(string key, CancellationToken token = default)
            => Enabled ? _inner.RemoveAsync(key, token) : Task.CompletedTask;
    }

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
    public async Task MemoryOnly_DoesNotWriteToL2()
    {
        var l2 = NewL2();
        var nodeA = NewNode(l2, CacheMode.MemoryOnly);
        var hybridReader = NewNode(l2, CacheMode.Hybrid);
        var counter = new Counter();

        await Read(nodeA, counter);
        var onOtherNode = await Read(hybridReader, counter);

        Assert.False(onOtherNode.ServedFromCache);
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
    public async Task Hybrid_ServesFromL1_EvenWhenL2IsUnavailable()
    {
        var l2 = NewL2();
        var node = NewNode(l2, CacheMode.Hybrid);
        var counter = new Counter();

        await Read(node, counter);
        l2.Enabled = false;
        var second = await Read(node, counter);

        Assert.True(second.ServedFromCache);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task Hybrid_SharesThroughL2_WithAnotherNode()
    {
        var l2 = NewL2();
        var nodeA = NewNode(l2, CacheMode.Hybrid);
        var nodeB = NewNode(l2, CacheMode.Hybrid);
        var counter = new Counter();

        await Read(nodeA, counter);
        var onB = await Read(nodeB, counter);

        Assert.True(onB.ServedFromCache);
        Assert.Equal(1, counter.Calls);
    }

    [Fact]
    public async Task DistributedOnly_DoesNotUseL1_WhenL2IsUnavailable()
    {
        var l2 = NewL2();
        var node = NewNode(l2, CacheMode.DistributedOnly);
        var counter = new Counter();

        await Read(node, counter);
        l2.Enabled = false;
        var second = await Read(node, counter);

        Assert.False(second.ServedFromCache);
        Assert.Equal(2, counter.Calls);
    }

    [Fact]
    public void WithoutCacheLevels_IsRejected()
    {
        var fusion = new FusionCache(new FusionCacheOptions());

        Assert.Throws<ArgumentNullException>(() =>
            new DirectoryCache(fusion, Mock.Of<ICacheInvalidator>(), NullLogger<DirectoryCache>.Instance, null!));
    }
}
