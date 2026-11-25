using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

public static class CachingServiceCollectionExtensions
{
    // Infrastructure/Caching/DependencyInjection.cs
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<CachingOptions>().Bind(config.GetSection(CachingOptions.Section))
                .ValidateDataAnnotations().ValidateOnStart();

        // ONE multiplexer, shared by L2, the backplane, and the generation pub/sub.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
            var cfg = ConfigurationOptions.Parse(o.RedisConnectionString);
            cfg.AbortOnConnectFail = false;       // keep retrying instead of crashing at startup
            cfg.ConnectTimeout = o.ConnectTimeoutMs;
            cfg.SyncTimeout = o.SyncTimeoutMs;
            cfg.AsyncTimeout = o.AsyncTimeoutMs;
            cfg.KeepAlive = o.KeepAliveSeconds;
            cfg.ClientName = o.KeyPrefix.TrimEnd(':');   // visible in CLIENT LIST
            // Connect is synchronous and runs once, when this singleton is first resolved.
            return ConnectionMultiplexer.Connect(cfg);
        });

        // L2: IDistributedCache over the shared multiplexer. No InstanceName here:
        // FusionCache's CacheKeyPrefix isolates the keys.
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>(
            (o, mux) => o.ConnectionMultiplexerFactory = () => Task.FromResult(mux));

        services.AddFusionCache()
            .WithOptions((sp, o) =>
            {
                var c = sp.GetRequiredService<IOptions<CachingOptions>>().Value;
                o.CacheKeyPrefix = c.KeyPrefix;
                o.BackplaneChannelPrefix = c.ChannelPrefix;
                o.DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = c.MemoryCacheDuration,          // L1
                    DistributedCacheDuration = c.Duration      // L2
                };
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithRegisteredDistributedCache()
            // Build the backplane explicitly so it reuses the shared multiplexer. The
            // parameterless WithStackExchangeRedisBackplane() did not pick up a factory
            // configured through IOptions<RedisBackplaneOptions> [Verified: failed with
            // "no Configuration nor ConfigurationOptions have been specified"].
            .WithBackplane(sp => new RedisBackplane(Options.Create(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(sp.GetRequiredService<IConnectionMultiplexer>())
            })));

        services.AddSingleton<ICacheInvalidator, FusionCacheInvalidator>();

        // ---- Option A only: reconciler + its readiness check (these three statements) ----
        // AddHostedService<T> registers only IHostedService; the concrete type must be
        // resolvable so the readiness check can read its state. For option B, delete the
        // AddSingleton<CacheReconciler>, AddHostedService and AddHealthChecks lines below.
        services.AddSingleton<CacheReconciler>();
        services.AddHostedService(sp => sp.GetRequiredService<CacheReconciler>());
        services.AddHealthChecks().AddCheck<CacheReconcilerHealthCheck>("cache-reconciler", tags: ["ready"]);
        return services;
    }
}
