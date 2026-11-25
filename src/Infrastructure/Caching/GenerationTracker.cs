namespace Bitai.LDAPGateway.Infrastructure.Caching;

/// <summary>
/// Tracks which invalidation generations this node has seen. Tolerates reordering and duplicates and
/// reports a gap only when the same hole persists across two checks.
/// </summary>
internal sealed class GenerationTracker
{
    private readonly object _gate = new();
    private readonly SortedSet<long> _ahead = new();
    private readonly int _maxAhead;
    private long _contiguous;
    private long _suspect = -1;

    public GenerationTracker(int maxAhead)
    {
        _maxAhead = maxAhead;
    }

    public void Reset(long generation)
    {
        lock (_gate)
        {
            _contiguous = generation;
            _ahead.Clear();
            _suspect = -1;
        }
    }

    /// <summary>Returns <see langword="true"/> when too many out-of-order generations are held (the caller should flush).</summary>
    public bool Observe(long generation)
    {
        if (generation <= 0)
        {
            return false;
        }

        lock (_gate)
        {
            if (generation <= _contiguous)
            {
                return false;
            }

            _ahead.Add(generation);
            while (_ahead.Remove(_contiguous + 1))
            {
                _contiguous++;
            }

            return _ahead.Count > _maxAhead;
        }
    }

    /// <summary>Compares the shared counter with the generations seen by this node.</summary>
    public bool IsGapConfirmed(long redisGeneration)
    {
        lock (_gate)
        {
            if (redisGeneration < _contiguous)
            {
                return true; // Redis lost its state: the counter went backwards.
            }

            if (redisGeneration == _contiguous)
            {
                _suspect = -1;
                return false;
            }

            if (_suspect == _contiguous)
            {
                return true; // Same hole as the previous check: events are not in flight anymore.
            }

            _suspect = _contiguous;
            return false;
        }
    }
}
