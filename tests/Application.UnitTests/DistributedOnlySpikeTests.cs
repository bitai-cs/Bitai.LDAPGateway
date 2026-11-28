using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bitai.LDAPGateway.Application.UnitTests;

/// <summary>
/// Spike: validates that FusionCache can run L2-only (L1 skipped per entry) and that tag invalidation
/// is effective across two "nodes" sharing the same L2. No Redis needed: MemoryDistributedCache is the shared L2.
/// </summary>
public sealed class DistributedOnlySpikeTests
{
    private static readonly FusionCacheEntryOptions L2Only = new()
    {
        Duration = TimeSpan.FromMinutes(1),
        DistributedCacheDuration = TimeSpan.FromMinutes(1),
        SkipMemoryCacheRead = true,
        SkipMemoryCacheWrite = true
    };

    private static IFusionCache NewNode(IDistributedCache l2)
    {
        var cache = new FusionCache(new FusionCacheOptions
        {
            CacheKeyPrefix = "spike:",
            TagsDefaultEntryOptions = new FusionCacheEntryOptions
            {
                SkipMemoryCacheRead = true,
                SkipMemoryCacheWrite = true
            }
        });
        cache.SetupDistributedCache(l2, new FusionCacheSystemTextJsonSerializer());
        return cache;
    }

    private static IDistributedCache NewL2() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task L2Only_SecondCall_IsServedFromL2()
    {
        var node = NewNode(NewL2());
        var calls = 0;

        for (var i = 0; i < 2; i++)
        {
            await node.GetOrSetAsync<string>("k", _ => { calls++; return Task.FromResult("v"); },
                options: L2Only, tags: ["t"]);
        }

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task L2Only_RemoveByTag_SameNode_ForcesReload()
    {
        var node = NewNode(NewL2());
        var calls = 0;
        Task<string> Factory(CancellationToken _) { calls++; return Task.FromResult($"v{calls}"); }

        await node.GetOrSetAsync<string>("k", (_, ct) => Factory(ct), options: L2Only, tags: ["t"]);
        await node.RemoveByTagAsync("t");
        var after = await node.GetOrSetAsync<string>("k", (_, ct) => Factory(ct), options: L2Only, tags: ["t"]);

        Assert.Equal(2, calls);
        Assert.Equal("v2", after);
    }

    [Fact]
    public async Task L2Only_RemoveByTag_OnNodeA_IsSeenByNodeB()
    {
        var l2 = NewL2();
        var nodeA = NewNode(l2);
        var nodeB = NewNode(l2);
        var calls = 0;
        Task<string> Factory(CancellationToken _) { calls++; return Task.FromResult($"v{calls}"); }

        await nodeA.GetOrSetAsync<string>("k", (_, ct) => Factory(ct), options: L2Only, tags: ["t"]);
        var hitOnB = await nodeB.GetOrSetAsync<string>("k", (_, ct) => Factory(ct), options: L2Only, tags: ["t"]);
        Assert.Equal("v1", hitOnB);
        Assert.Equal(1, calls);

        await nodeA.RemoveByTagAsync("t");

        var afterOnB = await nodeB.GetOrSetAsync<string>("k", (_, ct) => Factory(ct), options: L2Only, tags: ["t"]);
        Assert.Equal("v2", afterOnB);
        Assert.Equal(2, calls);
    }
}
