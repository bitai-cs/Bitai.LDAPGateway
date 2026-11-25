using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;
using System.Diagnostics.Metrics;

// Infrastructure/Caching/CacheReconciler.cs
internal sealed class CacheReconciler(
    IFusionCache cache,
    IConnectionMultiplexer redis,
    IOptions<CachingOptions> options,
    ILogger<CacheReconciler> logger) : IHostedService, IDisposable
{
    private static readonly TimeSpan MinFlushInterval = TimeSpan.FromSeconds(1);   // debounce bursts

    // Drops only THIS node's L1: no backplane notification, no L2 read or write.
    // [Verified] in the scratch run: other nodes and L2 were untouched.
    private static readonly FusionCacheEntryOptions LocalOnly = new()
    {
        SkipBackplaneNotifications = true,
        SkipDistributedCacheRead = true,
        SkipDistributedCacheWrite = true
    };

    private readonly Meter _meter = new("MyService.Caching");
    private readonly CancellationTokenSource _stopping = new();
    private readonly GenerationTracker _tracker = new(options.Value.MaxTrackedGenerations);
    private Counter<long>? _flushes, _malformed;
    private Task? _loop;
    private bool _subscribed;
    private int _dirty;     // 1 = this node must flush its local view
    private int _flushing;   // 1 = a local flush is running right now (_dirty is cleared before it starts)

    // Latched after the first successful subscription; transient disconnects do not
    // reset it (the loop handles those).
    public bool HasSubscribed { get; private set; }
    public bool IsReady => HasSubscribed && redis.IsConnected;
    // Pending OR in progress: _dirty alone is 0 during the flush itself.
    public bool IsReconciling => Volatile.Read(ref _dirty) == 1 || Volatile.Read(ref _flushing) == 1;

    public async Task StartAsync(CancellationToken ct)
    {
        _flushes = _meter.CreateCounter<long>("cache_reconcile_flushes");
        _malformed = _meter.CreateCounter<long>("cache_generation_malformed");
        _meter.CreateObservableGauge("cache_reconciler_ready", () => IsReady ? 1 : 0);

        // Pub/Sub does not replay messages missed while disconnected: any restore
        // event marks this node for a flush (done by the loop, never here).
        redis.ConnectionRestored += OnConnectionRestored;

        // Redis may be down at startup (AbortOnConnectFail=false). Try once; the loop
        // keeps retrying every second until the subscription exists.
        try { await TryConnectAsync(); }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogWarning(ex, "Initial generation subscription failed; retrying in background.");
        }

        _loop = Task.Run(() => LoopAsync(_stopping.Token), CancellationToken.None);
    }

    private async Task TryConnectAsync()
    {
        // Handler overload: the callback below does O(1) work and never blocks.
        var subscriber = redis.GetSubscriber();
        var channel = RedisChannel.Literal(options.Value.GenerationChannel);
        await subscriber.SubscribeAsync(channel, OnGeneration);

        try
        {
            // Subscribe first, then read the counter: any event published after the
            // subscription is observed, and the L1 of a cold node is empty anyway.
            _tracker.Reset(await ReadGenerationAsync());
        }
        catch
        {
            // Undo the subscription so the loop retries the whole sequence. Setting
            // _subscribed before the read succeeds would leave HasSubscribed false forever
            // (the loop only retries while _subscribed is false) and readiness Unhealthy.
            try { await subscriber.UnsubscribeAsync(channel, OnGeneration); } catch { /* best effort */ }
            throw;
        }

        _subscribed = true;
        HasSubscribed = true;
    }

    private async Task<long> ReadGenerationAsync()
    {
        var value = await redis.GetDatabase().StringGetAsync(options.Value.GenerationKey);
        return value.HasValue ? (long)value : 0;
    }

    // Runs on a Redis callback thread: no I/O, no blocking, no exceptions.
    private void OnGeneration(RedisChannel channel, RedisValue value)
    {
        if (!value.TryParse(out long generation)) { _malformed?.Add(1); MarkDirty(); return; }
        if (_tracker.Observe(generation)) MarkDirty();     // too many holes: stop tracking, flush
    }

    private void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);

    // Plain synchronous handler on purpose: no `async void`, nothing to crash the process.
    private void OnConnectionRestored(object? sender, ConnectionFailedEventArgs e) => MarkDirty();

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var lastCheck = DateTimeOffset.UtcNow;
        var lastFlush = DateTimeOffset.MinValue;

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    if (!_subscribed) { await TryConnectAsync(); continue; }

                    var now = DateTimeOffset.UtcNow;

                    if (IsReconciling && now - lastFlush >= MinFlushInterval)
                    {
                        Volatile.Write(ref _flushing, 1);      // set BEFORE clearing _dirty: IsReconciling never reads false mid-way
                        Interlocked.Exchange(ref _dirty, 0);   // clear first: a concurrent MarkDirty is not lost
                        try { await FlushAsync(ct); lastFlush = now; }
                        catch { MarkDirty(); throw; }          // retry on the next tick
                        finally { Volatile.Write(ref _flushing, 0); }
                        continue;
                    }

                    if (now - lastCheck >= options.Value.ReconcileInterval)
                    {
                        lastCheck = now;
                        if (_tracker.IsGapConfirmed(await ReadGenerationAsync()))
                        {
                            logger.LogWarning("Invalidation generation gap detected; scheduling local flush");
                            MarkDirty();
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // Redis unreachable etc.: keep the loop alive; readiness reports Degraded.
                    logger.LogError(ex, "Cache reconciliation step failed; will retry");
                }
            }
        }
        catch (OperationCanceledException) { /* stopping */ }
    }

    private async Task FlushAsync(CancellationToken ct)
    {
        await cache.ClearAsync(allowFailSafe: false, LocalOnly, ct);
        _tracker.Reset(await ReadGenerationAsync());   // everything up to here is now covered
        _flushes?.Add(1);
        logger.LogWarning("Local cache flushed by reconciliation.");
    }

    // Shutdown bound: the loop observes `_stopping`, but the StackExchange.Redis calls inside it
    // take no token, so StopAsync can wait up to AsyncTimeout for an in-flight Redis call.
    // `ct` bounds the wait for the loop here.
    public async Task StopAsync(CancellationToken ct)
    {
        _stopping.Cancel();
        redis.ConnectionRestored -= OnConnectionRestored;
        if (_loop is not null) await _loop.WaitAsync(ct);
        if (_subscribed)
            await redis.GetSubscriber().UnsubscribeAsync(
                RedisChannel.Literal(options.Value.GenerationChannel), OnGeneration);
        _subscribed = false;
        HasSubscribed = false;
    }

    public void Dispose() { _stopping.Dispose(); _meter.Dispose(); }
}
