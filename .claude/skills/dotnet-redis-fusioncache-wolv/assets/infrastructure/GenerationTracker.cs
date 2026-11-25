// Infrastructure/Caching/GenerationTracker.cs
// Tracks which invalidation generations this node has seen. Tolerates reordering
// and duplicates; reports a gap only when the same hole persists across two checks.
internal sealed class GenerationTracker(int maxAhead)
{
    private readonly object _gate = new();
    private readonly SortedSet<long> _ahead = new();
    private long _contiguous;       // every generation <= _contiguous was seen or is covered by a flush
    private long _suspect = -1;     // _contiguous value at the previous unresolved check

    public void Reset(long generation)
    {
        lock (_gate) { _contiguous = generation; _ahead.Clear(); _suspect = -1; }
    }

    /// Returns true when too many out-of-order generations are being held (the caller should flush).
    public bool Observe(long generation)
    {
        if (generation <= 0) return false;
        lock (_gate)
        {
            if (generation <= _contiguous) return false;    // duplicate or already covered
            _ahead.Add(generation);
            while (_ahead.Remove(_contiguous + 1)) _contiguous++;
            return _ahead.Count > maxAhead;
        }
    }

    // redisGeneration: current value of the shared counter.
    public bool IsGapConfirmed(long redisGeneration)
    {
        lock (_gate)
        {
            if (redisGeneration < _contiguous) return true;     // Redis lost its state: counter went backwards
            if (redisGeneration == _contiguous) { _suspect = -1; return false; }
            if (_suspect == _contiguous) return true;           // same hole as the previous check
            _suspect = _contiguous;                             // first sighting: events may be in flight
            return false;
        }
    }
}
