using System.ComponentModel.DataAnnotations;
using Bitai.LDAPGateway.Application.Common.Caching;
using Bitai.LDAPGateway.Infrastructure.Caching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Bitai.LDAPGateway.Infrastructure.UnitTests;

public sealed class CachingLevelsTests
{
    private static CachingOptions RedisOptions(bool memory, bool distributed) => new()
    {
        MemoryCacheEnabled = memory,
        DistributedCacheEnabled = distributed,
        RedisConnectionString = "localhost:6379",
        KeyPrefix = "test:",
        ChannelPrefix = "test"
    };

    private static List<ValidationResult> Validate(CachingOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    private static ServiceCollection Register(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCaching(configuration);
        return services;
    }

    private static Dictionary<string, string?> Config(bool memory, bool distributed) => new()
    {
        ["Caching:MemoryCacheEnabled"] = memory.ToString(),
        ["Caching:DistributedCacheEnabled"] = distributed.ToString(),
        ["Caching:RedisConnectionString"] = "localhost:6379",
        ["Caching:KeyPrefix"] = "test:",
        ["Caching:ChannelPrefix"] = "test"
    };

    [Theory]
    [InlineData(true, true, CacheMode.Hybrid)]
    [InlineData(true, false, CacheMode.MemoryOnly)]
    [InlineData(false, true, CacheMode.DistributedOnly)]
    [InlineData(false, false, CacheMode.None)]
    public void Mode_IsResolvedFromTheTwoFlags(bool memory, bool distributed, CacheMode expected)
        => Assert.Equal(expected, RedisOptions(memory, distributed).Mode);

    [Fact]
    public void Defaults_EnableBothLevels()
        => Assert.Equal(CacheMode.Hybrid, new CachingOptions().Mode);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Validation_DoesNotRequireRedis_WhenL2IsOff(bool memory, bool distributed)
    {
        var options = new CachingOptions { MemoryCacheEnabled = memory, DistributedCacheEnabled = distributed };

        Assert.Empty(Validate(options));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validation_RequiresRedisSettings_WhenL2IsOn(bool memory)
    {
        var options = new CachingOptions { MemoryCacheEnabled = memory, DistributedCacheEnabled = true };

        var members = Validate(options).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(CachingOptions.RedisConnectionString), members);
        Assert.Contains(nameof(CachingOptions.KeyPrefix), members);
        Assert.Contains(nameof(CachingOptions.ChannelPrefix), members);
    }

    [Fact]
    public void Validation_L1MustNotExceedL2_OnlyWhenBothLevelsAreOn()
    {
        var both = RedisOptions(true, true);
        both.MemoryCacheDuration = TimeSpan.FromMinutes(10);
        both.Duration = TimeSpan.FromMinutes(1);
        Assert.Contains(Validate(both), r => r.MemberNames.Contains(nameof(CachingOptions.MemoryCacheDuration)));

        var l2Only = RedisOptions(false, true);
        l2Only.MemoryCacheDuration = TimeSpan.FromMinutes(10);
        l2Only.Duration = TimeSpan.FromMinutes(1);
        Assert.Empty(Validate(l2Only));
    }

    [Theory]
    [InlineData(false, false, "NoOpCacheInvalidator", false, false)]
    [InlineData(true, false, "LocalCacheInvalidator", false, false)]
    [InlineData(false, true, "FusionCacheInvalidator", true, false)]
    [InlineData(true, true, "FusionCacheInvalidator", true, true)]
    public void Registration_MatchesTheMode(bool memory, bool distributed, string invalidator, bool redis, bool reconciler)
    {
        var services = Register(Config(memory, distributed));

        var invalidatorDescriptor = Assert.Single(services, d => d.ServiceType == typeof(ICacheInvalidator));
        var invalidatorType = invalidatorDescriptor.ImplementationType ?? invalidatorDescriptor.ImplementationInstance?.GetType();
        Assert.Equal(invalidator, invalidatorType?.Name);

        Assert.Equal(redis, services.Any(d => d.ServiceType == typeof(IConnectionMultiplexer)));
        Assert.Equal(reconciler, services.Any(d => d.ServiceType.Name == "CacheReconciler"));
    }

    [Theory]
    [InlineData(false, false, CacheMode.None)]
    [InlineData(true, false, CacheMode.MemoryOnly)]
    [InlineData(false, true, CacheMode.DistributedOnly)]
    [InlineData(true, true, CacheMode.Hybrid)]
    public void Registration_ExposesTheCacheLevelsToTheApplication(bool memory, bool distributed, CacheMode expected)
    {
        var services = Register(Config(memory, distributed));

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ICacheLevels));
        Assert.Equal(expected, ((ICacheLevels)descriptor.ImplementationInstance!).Mode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Registration_WithoutRedis_BuildsAndResolvesTheInvalidator(bool memory, bool distributed)
    {
        using var provider = Register(Config(memory, distributed)).BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ICacheInvalidator>());
        Assert.NotNull(provider.GetRequiredService<ZiggyCreatures.Caching.Fusion.IFusionCache>());
    }
}
