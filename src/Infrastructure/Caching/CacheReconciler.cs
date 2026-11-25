using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion;

namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Watches the invalidation generation and flushes this node's L1 when it knows or suspects it missed an invalidation.
/// </summary>
/// <remarks>
/// Redis Pub/Sub is at-most-once. A reconnect, a malformed event, a confirmed generation gap or an excess of
/// out-of-order generations all converge on one repair: a debounced, local-only <c>ClearAsync</c>.
/// Other nodes are never touched; they repair themselves through their own signals.
/// </remarks>
internal sealed class CacheReconciler : IHostedService, IDisposable
{
    private static readonly TimeSpan MinFlushInterval = TimeSpan.FromSeconds(1);

    private static readonly FusionCacheEntryOptions LocalOnly = new()
    {
        SkipBackplaneNotifications = true,
        SkipDistributedCacheRead = true,
        SkipDistributedCacheWrite = true
    };

    private readonly IFusionCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly CachingOptions _options;
    private readonly ILogger<CacheReconciler> _logger;

    private readonly Meter _meter = new("Bitai.LDAPGateway.Caching");
    private readonly CancellationTokenSource _stopping = new();
    private readonly GenerationTracker _tracker;

    private Counter<long>? _flushes;
    private Counter<long>? _malformed;
    private Task? _loop;
    private bool _subscribed;
    private int _dirty;
    private int _flushing;

    public CacheReconciler(
        IFusionCache cache,
        IConnectionMultiplexer redis,
        IOptions<CachingOptions> options,
        ILogger<CacheReconciler> logger)
    {
        _cache = cache;
        _redis = redis;
        _options = options.Value;
        _logger = logger;
        _tracker = new GenerationTracker(_options.MaxTrackedGenerations);
    }

    /// <summary>Latched after the first successful subscription; transient disconnects do not reset it.</summary>
    public bool HasSubscribed { get; private set; }

    public bool IsReady => HasSubscribed && _redis.IsConnected;

    /// <summary>Pending or in progress: <see cref="_dirty"/> is zero during the flush itself.</summary>
    public bool IsReconciling => Volatile.Read(ref _dirty) == 1 || Volatile.Read(ref _flushing) == 1;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _flushes = _meter.CreateCounter<long>("cache_reconcile_flushes");
        _malformed = _meter.CreateCounter<long>("cache_generation_malformed");
        _meter.CreateObservableGauge("cache_reconciler_ready", () => IsReady ? 1 : 0);

        // Pub/Sub does not replay messages missed while disconnected: any restore marks this node for a flush.
        _redis.ConnectionRestored += OnConnectionRestored;

        try
        {
            await TryConnectAsync();
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            _logger.LogWarning(ex, "Initial generation subscription failed; retrying in background.");
        }

        _loop = Task.Run(() => LoopAsync(_stopping.Token), CancellationToken.None);
    }

    private async Task TryConnectAsync()
    {
        var subscriber = _redis.GetSubscriber();
        var channel = RedisChannel.Literal(_options.GenerationChannel);
        await subscriber.SubscribeAsync(channel, OnGeneration);

        try
        {
            // Subscribe first, then read the counter: a cold node's L1 is empty, and any later event is observed.
            _tracker.Reset(await ReadGenerationAsync());
        }
        catch
        {
            try
            {
                await subscriber.UnsubscribeAsync(channel, OnGeneration);
            }
            catch
            {
                // Best effort; the loop retries the whole sequence.
            }

            throw;
        }

        _subscribed = true;
        HasSubscribed = true;
    }

    private async Task<long> ReadGenerationAsync()
    {
        var value = await _redis.GetDatabase().StringGetAsync(_options.GenerationKey);
        return value.HasValue ? (long)value : 0;
    }

    /// <summary>Runs on a Redis callback thread: no I/O, no blocking, no exceptions.</summary>
    private void OnGeneration(RedisChannel channel, RedisValue value)
    {
        if (!value.TryParse(out long generation))
        {
            _malformed?.Add(1);
            MarkDirty();
            return;
        }

        if (_tracker.Observe(generation))
        {
            MarkDirty();
        }
    }

    private void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);

    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e) => MarkDirty();

    private async Task LoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var lastCheck = DateTimeOffset.UtcNow;
        var lastFlush = DateTimeOffset.MinValue;

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    if (!_subscribed)
                    {
                        await TryConnectAsync();
                        continue;
                    }

                    var now = DateTimeOffset.UtcNow;

                    if (IsReconciling && now - lastFlush >= MinFlushInterval)
                    {
                        Volatile.Write(ref _flushing, 1);
                        Interlocked.Exchange(ref _dirty, 0);
                        try
                        {
                            await FlushAsync(cancellationToken);
                            lastFlush = now;
                        }
                        catch
                        {
                            MarkDirty();
                            throw;
                        }
                        finally
                        {
                            Volatile.Write(ref _flushing, 0);
                        }

                        continue;
                    }

                    if (now - lastCheck >= _options.ReconcileInterval)
                    {
                        lastCheck = now;
                        if (_tracker.IsGapConfirmed(await ReadGenerationAsync()))
                        {
                            _logger.LogWarning("Invalidation generation gap detected; scheduling local flush");
                            MarkDirty();
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cache reconciliation step failed; will retry");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _cache.ClearAsync(allowFailSafe: false, LocalOnly, cancellationToken);
        _tracker.Reset(await ReadGenerationAsync());
        _flushes?.Add(1);
        _logger.LogWarning("Local cache flushed by reconciliation.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping.Cancel();
        _redis.ConnectionRestored -= OnConnectionRestored;

        if (_loop is not null)
        {
            await _loop.WaitAsync(cancellationToken);
        }

        if (_subscribed)
        {
            await _redis.GetSubscriber().UnsubscribeAsync(
                RedisChannel.Literal(_options.GenerationChannel),
                OnGeneration);
        }

        _subscribed = false;
        HasSubscribed = false;
    }

    public void Dispose()
    {
        _stopping.Dispose();
        _meter.Dispose();
    }
}
