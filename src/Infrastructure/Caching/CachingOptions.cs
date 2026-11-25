using System.ComponentModel.DataAnnotations;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Configuration for the FusionCache L1/L2 cache, the Redis backplane and the reconciler.
/// </summary>
public sealed class CachingOptions : IValidatableObject
{
    public const string SectionName = "Caching";

    /// <summary>
    /// Enables the Redis L2 and the cross-node backplane. When disabled the cache lives in process memory only.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// StackExchange.Redis connection string. It must not be committed: supply it through configuration,
    /// environment variables or a secret store.
    /// </summary>
    public string RedisConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// FusionCache key prefix. Must identify the service, the environment and the cache version
    /// (for example <c>bitai:ldapgateway:prod:v1:</c>).
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// FusionCache backplane channel prefix. It is also the base of the generation key and channel.
    /// </summary>
    public string ChannelPrefix { get; set; } = string.Empty;

    /// <summary>Logical duration of an entry in Redis (L2).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Duration of an entry in the local memory cache (L1).</summary>
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan MemoryCacheDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How often each node compares the generations it has seen with the shared counter.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Out-of-order generations a node tracks before it gives up and flushes its L1.</summary>
    [Range(16, 100_000)]
    public int MaxTrackedGenerations { get; set; } = 4_096;

    [Range(1_000, 60_000)]
    public int ConnectTimeoutMs { get; set; } = 5_000;

    [Range(1_000, 60_000)]
    public int SyncTimeoutMs { get; set; } = 5_000;

    [Range(1_000, 60_000)]
    public int AsyncTimeoutMs { get; set; } = 5_000;

    [Range(0, 300)]
    public int KeepAliveSeconds { get; set; } = 60;

    /// <summary>Shared counter incremented by every invalidation; scoped per service and environment.</summary>
    public string GenerationKey => $"{ChannelPrefix}:cache:generation";

    /// <summary>Channel used to announce a new generation to every node.</summary>
    public string GenerationChannel => $"{ChannelPrefix}:cache:generation:events";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled)
        {
            if (string.IsNullOrWhiteSpace(RedisConnectionString))
            {
                yield return new ValidationResult(
                    "RedisConnectionString is required when caching is enabled.",
                    [nameof(RedisConnectionString)]);
            }

            if (string.IsNullOrWhiteSpace(KeyPrefix) || !KeyPrefix.EndsWith(':'))
            {
                yield return new ValidationResult(
                    "KeyPrefix is required when caching is enabled and must end with ':'.",
                    [nameof(KeyPrefix)]);
            }

            if (string.IsNullOrWhiteSpace(ChannelPrefix))
            {
                yield return new ValidationResult(
                    "ChannelPrefix is required when caching is enabled.",
                    [nameof(ChannelPrefix)]);
            }
        }

        if (MemoryCacheDuration > Duration)
        {
            yield return new ValidationResult(
                "MemoryCacheDuration (L1) must not exceed Duration (L2).",
                [nameof(MemoryCacheDuration)]);
        }
    }
}
