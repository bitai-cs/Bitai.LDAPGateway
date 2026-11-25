using System.ComponentModel.DataAnnotations;

public sealed class CachingOptions : IValidatableObject
{
    public const string Section = "Caching";

    [Required(AllowEmptyStrings = false)]
    public string RedisConnectionString { get; set; } = default!;

    // FusionCache CacheKeyPrefix (applied to L1 and L2 keys). Must identify service +
    // environment (+ cache version when the cached shape changes): deployments may share one Redis.
    [Required(AllowEmptyStrings = false)]
    public string KeyPrefix { get; set; } = default!;

    // FusionCache BackplaneChannelPrefix, also the base of the generation key/channel.
    // Must be distinct per service and environment, for the same reason.
    [Required(AllowEmptyStrings = false)]
    public string ChannelPrefix { get; set; } = default!;

    [Range(1000, 60000)] public int ConnectTimeoutMs { get; set; } = 5_000;
    [Range(1000, 60000)] public int SyncTimeoutMs { get; set; } = 5_000;
    [Range(1000, 60000)] public int AsyncTimeoutMs { get; set; } = 5_000;
    [Range(0, 300)] public int KeepAliveSeconds { get; set; } = 60;

    // Logical duration in L2 (Redis).
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);

    // L1 duration. Bounds staleness when a notification is missed (see the consistency model).
    [Range(typeof(TimeSpan), "00:00:01", "7.00:00:00")]
    public TimeSpan MemoryCacheDuration { get; set; } = TimeSpan.FromMinutes(2);

    // How often each node compares the generations it has seen with the shared counter.
    // A gap is confirmed on two consecutive checks, so detection takes up to 2x this value.
    [Range(typeof(TimeSpan), "00:00:01", "01:00:00")]
    public TimeSpan ReconcileInterval { get; set; } = TimeSpan.FromSeconds(30);

    // Out-of-order generations a node holds before it gives up and flushes. Bounds memory.
    [Range(16, 100_000)] public int MaxTrackedGenerations { get; set; } = 4_096;

    // Shared counter incremented by every invalidation. Scoped per service and environment.
    // It must not be evictable: no per-key pinning exists in Redis, so this is a property of
    // the Redis deployment's maxmemory-policy (see the Generation key note in Step 3e).
    public string GenerationKey => $"{ChannelPrefix}:cache:generation";
    public string GenerationChannel => $"{ChannelPrefix}:cache:generation:events";

    public IEnumerable<ValidationResult> Validate(ValidationContext c)
    {
        // Applies to the GLOBAL defaults only. Per-entry overrides (Step 2) are not validated here:
        // an override that sets Duration (L1) above DistributedCacheDuration (L2) is a deliberate
        // per-data-class decision that must be reviewed and tested where it is made.
        if (MemoryCacheDuration > Duration)
            yield return new ValidationResult(
                "MemoryCacheDuration (L1) must not exceed Duration (L2).", [nameof(MemoryCacheDuration)]);
    }
}
