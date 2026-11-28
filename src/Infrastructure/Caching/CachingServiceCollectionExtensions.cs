using Bitai.LDAPGateway.Application.Common.Caching;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

public static class CachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the cache levels selected by <see cref="CachingOptions.MemoryCacheEnabled"/> and
    /// <see cref="CachingOptions.DistributedCacheEnabled"/>: none, L1, Redis L2 or both with the backplane and reconciler.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CachingOptions>()
            .Bind(configuration.GetSection(CachingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHealthChecks();

        var options = configuration.GetSection(CachingOptions.SectionName).Get<CachingOptions>() ?? new CachingOptions();

        var mode = options.Mode;
        services.AddSingleton<ICacheLevels>(new CacheLevels(mode));

        return mode switch
        {
            CacheMode.None => services.AddLocalFusionCache(options, new NoOpCacheInvalidator()),
            CacheMode.MemoryOnly => services.AddLocalFusionCache(options, null),
            CacheMode.DistributedOnly => services.AddDistributedOnlyCaching(),
            _ => services.AddHybridCaching()
        };
    }

    /// <summary>Redis connection shared by L2, the backplane and the generation pub/sub, plus the L2 itself.</summary>
    private static void AddRedisL2(this IServiceCollection services)
    {
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var cachingOptions = serviceProvider.GetRequiredService<IOptions<CachingOptions>>().Value;
            if (string.IsNullOrWhiteSpace(cachingOptions.RedisConnectionString))
            {
                throw new InvalidOperationException(
                    "Caching:RedisConnectionString is required when Caching:DistributedCacheEnabled is true. " +
                    "Supply it through configuration, an environment variable or a secret store.");
            }

            var configurationOptions = ConfigurationOptions.Parse(cachingOptions.RedisConnectionString);
            configurationOptions.AbortOnConnectFail = false;
            configurationOptions.ConnectTimeout = cachingOptions.ConnectTimeoutMs;
            configurationOptions.SyncTimeout = cachingOptions.SyncTimeoutMs;
            configurationOptions.AsyncTimeout = cachingOptions.AsyncTimeoutMs;
            configurationOptions.KeepAlive = cachingOptions.KeepAliveSeconds;
            configurationOptions.ClientName = cachingOptions.KeyPrefix.TrimEnd(':');

            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        // L2 over the shared multiplexer. No InstanceName: FusionCache's CacheKeyPrefix isolates the keys.
        services.AddStackExchangeRedisCache(_ => { });
        services.AddOptions<RedisCacheOptions>().Configure<IConnectionMultiplexer>(
            (redisOptions, multiplexer) => redisOptions.ConnectionMultiplexerFactory = () => Task.FromResult(multiplexer));
    }

    private static void ConfigureFusionOptions(FusionCacheOptions fusionOptions, CachingOptions cachingOptions, bool distributedOnly)
    {
        fusionOptions.CacheKeyPrefix = cachingOptions.KeyPrefix;
        fusionOptions.BackplaneChannelPrefix = cachingOptions.ChannelPrefix;
        fusionOptions.ReThrowOriginalExceptions = true;
        fusionOptions.DefaultEntryOptions = new FusionCacheEntryOptions
        {
            Duration = cachingOptions.MemoryCacheDuration,
            DistributedCacheDuration = cachingOptions.Duration
        };

        if (distributedOnly)
        {
            // Without an L1 there is no backplane to tell the other nodes that a tag changed, so the tag
            // metadata must never be kept in memory or a node would keep serving entries invalidated elsewhere.
            fusionOptions.TagsDefaultEntryOptions = new FusionCacheEntryOptions
            {
                SkipMemoryCacheRead = true,
                SkipMemoryCacheWrite = true
            };
        }
    }

    /// <summary>L1 + Redis L2, the cross-node backplane, the invalidator and the reconciler.</summary>
    private static IServiceCollection AddHybridCaching(this IServiceCollection services)
    {
        services.AddRedisL2();

        services
            .AddFusionCache()
            .WithOptions((serviceProvider, fusionOptions) =>
                ConfigureFusionOptions(fusionOptions, serviceProvider.GetRequiredService<IOptions<CachingOptions>>().Value, distributedOnly: false))
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithRegisteredDistributedCache()
            .WithRedisBackplane();

        services.AddSingleton<ICacheInvalidator, FusionCacheInvalidator>();

        services.AddSingleton<CacheReconciler>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<CacheReconciler>());
        services.AddHealthChecks().AddCheck<CacheReconcilerHealthCheck>("cache-reconciler", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Redis L2 only: no L1 and no reconciler. It still joins the backplane and the generation protocol so that
    /// hybrid nodes sharing the same prefix drop their L1 entries when this node mutates the directory.
    /// </summary>
    private static IServiceCollection AddDistributedOnlyCaching(this IServiceCollection services)
    {
        services.AddRedisL2();

        services
            .AddFusionCache()
            .WithOptions((serviceProvider, fusionOptions) =>
                ConfigureFusionOptions(fusionOptions, serviceProvider.GetRequiredService<IOptions<CachingOptions>>().Value, distributedOnly: true))
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithRegisteredDistributedCache()
            .WithRedisBackplane();

        services.AddSingleton<ICacheInvalidator, FusionCacheInvalidator>();

        return services;
    }

    /// <summary>Redis backplane over the shared multiplexer.</summary>
    private static IFusionCacheBuilder WithRedisBackplane(this IFusionCacheBuilder builder)
        => builder.WithBackplane(serviceProvider => new RedisBackplane(Microsoft.Extensions.Options.Options.Create(new RedisBackplaneOptions
        {
            ConnectionMultiplexerFactory = () => Task.FromResult(serviceProvider.GetRequiredService<IConnectionMultiplexer>())
        })));

    /// <summary>
    /// In-process cache only (or no cache at all, in which case <paramref name="invalidator"/> is a no-op).
    /// IFusionCache stays registered in both cases so the application services keep resolving.
    /// </summary>
    private static IServiceCollection AddLocalFusionCache(
        this IServiceCollection services,
        CachingOptions options,
        ICacheInvalidator? invalidator)
    {
        var prefix = string.IsNullOrWhiteSpace(options.KeyPrefix)
            ? "bitai:ldapgateway:memory:"
            : options.KeyPrefix;

        services.AddFusionCache()
            .WithOptions(fusionOptions => fusionOptions.CacheKeyPrefix = prefix);

        if (invalidator is null)
        {
            services.AddSingleton<ICacheInvalidator, LocalCacheInvalidator>();
        }
        else
        {
            services.AddSingleton<ICacheInvalidator>(invalidator);
        }

        return services;
    }
}
