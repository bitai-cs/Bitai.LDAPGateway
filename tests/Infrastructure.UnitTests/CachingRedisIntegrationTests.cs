using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Application.Common.Models;
using Bitai.LDAPGateway.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Infrastructure.UnitTests;

/// <summary>Runs only when BITAI_IT_REDIS holds a Redis connection string; otherwise the tests are skipped.</summary>
public sealed class RedisFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "BITAI_IT_REDIS";

    public RedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Set {EnvironmentVariable} to a Redis connection string to run this test.";
        }
    }
}

/// <summary>
/// Two application nodes (two service providers built with the real <c>AddCaching</c>) sharing one real Redis.
/// Every run uses a unique key prefix, so runs never see each other's entries.
/// </summary>
public sealed class CachingRedisIntegrationTests
{
    private sealed class Node : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly List<IHostedService> _hosted;

        private Node(ServiceProvider provider, List<IHostedService> hosted, DirectoryCache cache)
        {
            _provider = provider;
            _hosted = hosted;
            Cache = cache;
        }

        public DirectoryCache Cache { get; }

        public static async Task<Node> StartAsync(bool memory, string runId)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Caching:MemoryCacheEnabled"] = memory.ToString(),
                ["Caching:DistributedCacheEnabled"] = "true",
                ["Caching:RedisConnectionString"] = Environment.GetEnvironmentVariable(RedisFactAttribute.EnvironmentVariable),
                ["Caching:KeyPrefix"] = $"bitai:it:{runId}:",
                ["Caching:ChannelPrefix"] = $"bitai:it:{runId}"
            }).Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddCaching(configuration);
            var provider = services.BuildServiceProvider();

            // Only the cache reconciler matters here; FusionCache has no hosted services of its own to start.
            var hosted = provider.GetServices<IHostedService>().Where(s => s.GetType().Name == "CacheReconciler").ToList();
            foreach (var service in hosted)
            {
                await service.StartAsync(CancellationToken.None);
            }

            var cache = new DirectoryCache(
                provider.GetRequiredService<IFusionCache>(),
                provider.GetRequiredService<ICacheInvalidator>(),
                NullLogger<DirectoryCache>.Instance,
                provider.GetRequiredService<ICacheLevels>());

            return new Node(provider, hosted, cache);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var service in _hosted)
            {
                await service.StopAsync(CancellationToken.None);
            }

            await _provider.DisposeAsync();
        }
    }

    private sealed class Counter
    {
        public int Calls;

        public Task<Result<string>> Run(CancellationToken _) => Task.FromResult(Result<string>.Success($"v{++Calls}"));
    }

    private static Task<CachedQueryResult<string>> Read(Node node, Counter counter) =>
        node.Cache.GetOrSetAsync("k", "EDU", CachePolicies.Entry(), false, counter.Run, CancellationToken.None);

    private static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return await condition();
    }

    [RedisFact]
    public async Task DistributedOnly_TwoNodes_ShareEntries_AndInvalidationReachesTheOtherNode()
    {
        var runId = Guid.NewGuid().ToString("N");
        await using var nodeA = await Node.StartAsync(memory: false, runId);
        await using var nodeB = await Node.StartAsync(memory: false, runId);
        var counter = new Counter();

        await Read(nodeA, counter);
        var onB = await Read(nodeB, counter);
        Assert.True(onB.ServedFromCache);
        Assert.Equal(1, counter.Calls);

        await nodeA.Cache.InvalidateProfileAsync("test", "EDU");

        var afterInvalidation = await Read(nodeB, counter);
        Assert.False(afterInvalidation.ServedFromCache);
        Assert.Equal("v2", afterInvalidation.Result.Value);
    }

    [RedisFact]
    public async Task Hybrid_TwoNodes_ShareEntries_AndBackplaneInvalidatesTheOtherNodesL1()
    {
        var runId = Guid.NewGuid().ToString("N");
        await using var nodeA = await Node.StartAsync(memory: true, runId);
        await using var nodeB = await Node.StartAsync(memory: true, runId);
        var counter = new Counter();

        await Read(nodeA, counter);
        await Read(nodeB, counter);
        await Read(nodeB, counter);
        Assert.Equal(1, counter.Calls);

        await nodeA.Cache.InvalidateProfileAsync("test", "EDU");

        // The L1 of node B is dropped through the backplane (asynchronous), so poll for a bounded time.
        var reloaded = await WaitUntilAsync(async () => !(await Read(nodeB, counter)).ServedFromCache, TimeSpan.FromSeconds(5));

        Assert.True(reloaded, "Node B kept serving the invalidated entry from its L1.");
    }

    [RedisFact]
    public async Task MixedFleet_DistributedOnlyNodeMutation_InvalidatesTheHybridNodesL1()
    {
        var runId = Guid.NewGuid().ToString("N");
        await using var hybrid = await Node.StartAsync(memory: true, runId);
        await using var distributedOnly = await Node.StartAsync(memory: false, runId);
        var counter = new Counter();

        await Read(hybrid, counter);
        await Read(hybrid, counter);
        Assert.Equal(1, counter.Calls);

        await distributedOnly.Cache.InvalidateProfileAsync("test", "EDU");

        var reloaded = await WaitUntilAsync(async () => !(await Read(hybrid, counter)).ServedFromCache, TimeSpan.FromSeconds(5));

        Assert.True(reloaded, "The hybrid node kept serving an entry invalidated by a distributed-only node.");
    }

    [RedisFact]
    public Task DistributedOnly_ReadInFlightDuringMutation_DoesNotLeaveAStaleEntry()
        => AssertNoStaleFillAsync(readerMemory: false, mutatorMemory: false);

    [RedisFact]
    public Task Hybrid_ReadInFlightDuringMutation_DoesNotLeaveAStaleEntry()
        => AssertNoStaleFillAsync(readerMemory: true, mutatorMemory: true);

    [RedisFact]
    public Task MixedFleet_ReadInFlightDuringMutation_DoesNotLeaveAStaleEntry()
        => AssertNoStaleFillAsync(readerMemory: false, mutatorMemory: true);

    /// <summary>
    /// A read starts (cache miss), a mutation on another node invalidates the profile, then the read finishes with
    /// its pre-mutation value. That value must not be served to later reads.
    /// </summary>
    private static async Task AssertNoStaleFillAsync(bool readerMemory, bool mutatorMemory)
    {
        var runId = Guid.NewGuid().ToString("N");
        await using var reader = await Node.StartAsync(readerMemory, runId);
        await using var mutator = await Node.StartAsync(mutatorMemory, runId);

        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();

        var slowRead = reader.Cache.GetOrSetAsync<string>("k", "EDU", CachePolicies.Entry(), false, async _ =>
        {
            started.SetResult();
            await release.Task;
            return Result<string>.Success("old");
        }, CancellationToken.None);

        await started.Task;
        await mutator.Cache.InvalidateProfileAsync("test", "EDU");
        release.SetResult();
        await slowRead;

        var counter = new Counter();
        var visible = await WaitUntilAsync(async () => (await Read(mutator, counter)).Result.Value == "v1", TimeSpan.FromSeconds(5));
        Assert.True(visible, "A pre-mutation fill was served after the profile was invalidated.");

        var onReader = await Read(reader, counter);
        Assert.NotEqual("old", onReader.Result.Value);
    }

    [RedisFact]
    public async Task MixedFleet_HybridNodeMutation_IsSeenByTheDistributedOnlyNode()
    {
        var runId = Guid.NewGuid().ToString("N");
        await using var hybrid = await Node.StartAsync(memory: true, runId);
        await using var distributedOnly = await Node.StartAsync(memory: false, runId);
        var counter = new Counter();

        await Read(distributedOnly, counter);
        Assert.True((await Read(distributedOnly, counter)).ServedFromCache);

        await hybrid.Cache.InvalidateProfileAsync("test", "EDU");

        var afterInvalidation = await Read(distributedOnly, counter);
        Assert.False(afterInvalidation.ServedFromCache);
    }
}
