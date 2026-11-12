using Microsoft.Extensions.Logging;

namespace App.Backend.Api.Logging;

/// <summary>One formatted log call, captured on the logging thread and written later by the writer thread.</summary>
public readonly record struct LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    int EventId,
    string Message,
    string? Exception);

/// <summary>
/// Bounded in-memory spool between the logging call sites (request threads, MQTT ingestion) and the
/// file writer thread. <see cref="Enqueue"/> does no I/O and never waits for the disk: it holds a lock
/// for a few instructions only. When the spool is full the oldest Trace/Debug/Information entry is
/// dropped first; warnings and errors are dropped (oldest first) only when no lower-level entry is left.
/// Drops are counted, so the writer can report them once the spool has drained.
/// </summary>
public sealed class LogSpool
{
    private readonly object _gate = new();
    // Two queues so that low-level entries can be evicted in O(1); the sequence number restores the
    // original order when they are merged again on drain.
    private Queue<(long Seq, LogEntry Entry)> _low = new();
    private Queue<(long Seq, LogEntry Entry)> _high = new();
    // Swapped in on drain (double buffering) so that the merge runs outside the lock. Single drainer.
    private Queue<(long Seq, LogEntry Entry)> _lowSpare = new();
    private Queue<(long Seq, LogEntry Entry)> _highSpare = new();
    private long _sequence;
    private long _droppedSinceDrain;
    private long _totalDropped;

    public LogSpool(int capacity)
    {
        Capacity = Math.Max(16, capacity);
    }

    public int Capacity { get; }

    public int Count
    {
        get { lock (_gate) return _low.Count + _high.Count; }
    }

    /// <summary>Entries dropped since the spool was created.</summary>
    public long TotalDropped => Interlocked.Read(ref _totalDropped);

    /// <summary>Queues <paramref name="entry"/>, evicting by priority when full.</summary>
    /// <returns>The number of queued entries after the call.</returns>
    public int Enqueue(in LogEntry entry)
    {
        var isLow = entry.Level < LogLevel.Warning;
        lock (_gate)
        {
            if (_low.Count + _high.Count >= Capacity)
            {
                if (_low.Count > 0)
                {
                    _low.Dequeue(); // the oldest debug/info entry goes first
                }
                else if (isLow)
                {
                    CountDrop(); // only warnings and errors are queued: the new low-level entry yields
                    return _high.Count;
                }
                else
                {
                    _high.Dequeue(); // nothing but warnings and errors: the oldest of them goes
                }
                CountDrop();
            }

            (isLow ? _low : _high).Enqueue((++_sequence, entry));
            return _low.Count + _high.Count;
        }
    }

    /// <summary>
    /// Moves every queued entry into <paramref name="batch"/> in logging order. Must only be called by
    /// one thread at a time (the writer).
    /// </summary>
    /// <returns>The number of entries dropped since the previous drain.</returns>
    public long DrainTo(List<LogEntry> batch)
    {
        Queue<(long Seq, LogEntry Entry)> low, high;
        long dropped;
        lock (_gate)
        {
            low = _low;
            high = _high;
            _low = _lowSpare;
            _high = _highSpare;
            dropped = _droppedSinceDrain;
            _droppedSinceDrain = 0;
        }

        while (low.Count > 0 || high.Count > 0)
        {
            var takeLow = high.Count == 0 || (low.Count > 0 && low.Peek().Seq < high.Peek().Seq);
            batch.Add((takeLow ? low : high).Dequeue().Entry);
        }

        _lowSpare = low;
        _highSpare = high;
        return dropped;
    }

    private void CountDrop()
    {
        _droppedSinceDrain++;
        Interlocked.Increment(ref _totalDropped);
    }
}
