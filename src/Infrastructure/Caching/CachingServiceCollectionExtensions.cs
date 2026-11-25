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
    /// Registers FusionCache with a Redis L2, the cross-node backplane, the invalidator and the reconciler.
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

        if (!options.Enabled)
        {
            return services.AddDisabledCaching(options);
        }

        // One multiplexer shared by L2, the backplane and the generation pub/sub.
        services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
        {
            var cachingOptions = serviceProvider.GetRequiredService<IOptions<CachingOptions>>().Value;
            if (string.IsNullOrWhiteSpace(cachingOptions.RedisConnectionString))
            {
                throw new InvalidOperationException(
                    "Caching:RedisConnectionString is required when Caching:Enabled is true. " +
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

        services
            .AddFusionCache()
            .WithOptions((serviceProvider, fusionOptions) =>
            {
                var cachingOptions = serviceProvider.GetRequiredService<IOptions<CachingOptions>>().Value;
                fusionOptions.CacheKeyPrefix = cachingOptions.KeyPrefix;
                fusionOptions.BackplaneChannelPrefix = cachingOptions.ChannelPrefix;
                fusionOptions.ReThrowOriginalExceptions = true;
                fusionOptions.DefaultEntryOptions = new FusionCacheEntryOptions
                {
                    Duration = cachingOptions.MemoryCacheDuration,
                    DistributedCacheDuration = cachingOptions.Duration
                };
            })
            .WithSerializer(new FusionCacheSystemTextJsonSerializer())
            .WithRegisteredDistributedCache()
            .WithBackplane(serviceProvider => new RedisBackplane(Microsoft.Extensions.Options.Options.Create(new RedisBackplaneOptions
            {
                ConnectionMultiplexerFactory = () => Task.FromResult(serviceProvider.GetRequiredService<IConnectionMultiplexer>())
            })));

        services.AddSingleton<ICacheInvalidator, FusionCacheInvalidator>();

        services.AddSingleton<CacheReconciler>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<CacheReconciler>());
        services.AddHealthChecks().AddCheck<CacheReconcilerHealthCheck>("cache-reconciler", tags: ["ready"]);

        return services;
    }

    private static IServiceCollection AddDisabledCaching(this IServiceCollection services, CachingOptions options)
    {
        var prefix = string.IsNullOrWhiteSpace(options.KeyPrefix)
            ? "bitai:ldapgateway:memory:"
            : options.KeyPrefix;

        services.AddFusionCache()
            .WithOptions(fusionOptions => fusionOptions.CacheKeyPrefix = prefix);

        services.AddSingleton<ICacheInvalidator, LocalCacheInvalidator>();

        return services;
    }
}
