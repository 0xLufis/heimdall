using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace App.Backend.Api.Services;

/// <summary>
/// When a station counts as stale: after <see cref="MissedSamples"/> missed samples of its own
/// learned sample interval, clamped to [<see cref="Floor"/>, <see cref="Ceiling"/>]. Settings:
/// <c>Analytics:Freshness:MissedSamples</c> (3), <c>Analytics:Freshness:FloorSeconds</c> (3),
/// <c>Analytics:Freshness:CeilingSeconds</c> (300). <c>Analytics:TelemetryStaleSeconds</c>, when set,
/// is a fixed threshold for every station instead. A station whose interval is not learned yet
/// (no arrivals on this backend instance, e.g. right after a restart) uses <see cref="Fallback"/>:
/// the larger of 60 s and three PLC keep-alive intervals, because without arrivals only the
/// snapshot's timestamp is known and that moves only once per keep-alive for an unchanged block.
/// </summary>
public sealed record StationFreshnessOptions
{
    public int MissedSamples { get; init; } = 3;
    public TimeSpan Floor { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan Ceiling { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan Fallback { get; init; } = PredictiveMaintenanceService.DefaultTelemetryStaleAfter;
    /// <summary>Fixed threshold for every station (<c>Analytics:TelemetryStaleSeconds</c>); null for per-station thresholds.</summary>
    public TimeSpan? FixedThreshold { get; init; }

    public static StationFreshnessOptions Default { get; } = new();

    public static StationFreshnessOptions FromConfiguration(IConfiguration? configuration)
    {
        if (configuration == null) return Default;
        var missed = configuration.GetValue<int?>("Analytics:Freshness:MissedSamples") ?? Default.MissedSamples;
        var floor = configuration.GetValue<double?>("Analytics:Freshness:FloorSeconds") ?? Default.Floor.TotalSeconds;
        var ceiling = configuration.GetValue<double?>("Analytics:Freshness:CeilingSeconds") ?? Default.Ceiling.TotalSeconds;
        var keepAlive = configuration.GetValue<double?>("Telemetry:PlcMemory:KeepAliveSeconds") ?? 10;
        var fixedSeconds = configuration.GetValue<double?>("Analytics:TelemetryStaleSeconds");

        floor = floor > 0 ? floor : Default.Floor.TotalSeconds;
        ceiling = Math.Max(floor, ceiling > 0 ? ceiling : Default.Ceiling.TotalSeconds);
        return new StationFreshnessOptions
        {
            MissedSamples = Math.Max(1, missed),
            Floor = TimeSpan.FromSeconds(floor),
            Ceiling = TimeSpan.FromSeconds(ceiling),
            Fallback = TimeSpan.FromSeconds(Math.Max(Default.Fallback.TotalSeconds, keepAlive * 3)),
            FixedThreshold = fixedSeconds is > 0 ? TimeSpan.FromSeconds(fixedSeconds.Value) : null
        };
    }

    /// <summary>Stale threshold of a station with the given learned sample interval (null: not learned).</summary>
    public TimeSpan StaleAfter(TimeSpan? sampleInterval)
    {
        if (FixedThreshold is { } fixedThreshold && fixedThreshold > TimeSpan.Zero) return fixedThreshold;
        if (sampleInterval is not { } interval || interval <= TimeSpan.Zero) return Fallback;
        var threshold = interval * Math.Max(1, MissedSamples);
        if (threshold < Floor) return Floor;
        return threshold > Ceiling ? Ceiling : threshold;
    }
}

/// <summary>What this backend instance saw arrive for one station.</summary>
/// <param name="LastArrivalAt">Receive time of the station's latest PLC block, including blocks the change gate dropped as unchanged.</param>
/// <param name="SampleInterval">Learned sample interval (median of recent gaps); null until enough gaps were seen.</param>
/// <param name="GapCount">Gaps currently in the learning window.</param>
public readonly record struct StationArrivals(DateTimeOffset LastArrivalAt, TimeSpan? SampleInterval, int GapCount);

/// <summary>Per-station arrival times and learned sample intervals.</summary>
public interface IStationFreshnessTracker
{
    /// <summary>Records that a PLC block of <paramref name="stationId"/> arrived at <paramref name="at"/>. No I/O.</summary>
    void RecordArrival(Guid stationId, DateTimeOffset at);

    /// <summary>The station's arrivals; null when this instance has not seen it.</summary>
    StationArrivals? Get(Guid stationId);
}

/// <summary>
/// In-memory, thread-safe arrival map: the receive time of every PLC block per station (also the
/// blocks the change gate drops, which cause no cache write or broadcast) and the station's sample
/// interval, learned as the median of its last <see cref="GapWindow"/> sample gaps. Blocks arriving
/// within <see cref="SameSampleWindow"/> of a sample's first block are the same sample (several
/// blocks per cycle); gaps longer than <see cref="MaxLearnedGap"/> are outages, not intervals.
/// Memory is bounded: a fixed ring per station, and stations silent for <see cref="Retention"/> are dropped.
/// <para>
/// Limitation: the map lives in this process. With several backend instances each one only knows
/// the blocks it ingested itself; another instance (or this one after a restart) falls back to the
/// shared snapshot timestamp (<see cref="StationTelemetrySnapshot.LastTelemetryAt"/>, refreshed on
/// forwarded blocks / keep-alives) and the fallback threshold of <see cref="StationFreshnessOptions"/>.
/// </para>
/// </summary>
public sealed class StationFreshnessTracker : IStationFreshnessTracker
{
    /// <summary>The process-wide map used by ingestion and analytics unless another tracker is injected.</summary>
    public static StationFreshnessTracker Shared { get; } = new();

    public const int GapWindow = 15;
    public const int MinGapsForInterval = 3;
    public static readonly TimeSpan SameSampleWindow = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan MaxLearnedGap = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Retention = TimeSpan.FromHours(1);
    private const int PruneEvery = 4096;

    private sealed class Entry
    {
        public readonly double[] Gaps = new double[GapWindow];
        public int GapCount;
        public int Next;
        public DateTimeOffset SampleStartedAt;
        public DateTimeOffset LastArrivalAt;
        public bool Initialized;
    }

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private long _records;

    /// <summary>Stations currently tracked.</summary>
    public int Count => _entries.Count;

    public void RecordArrival(Guid stationId, DateTimeOffset at)
    {
        if (stationId == Guid.Empty) return;
        var entry = _entries.GetOrAdd(stationId, _ => new Entry());
        lock (entry)
        {
            if (!entry.Initialized)
            {
                entry.Initialized = true;
                entry.SampleStartedAt = at;
                entry.LastArrivalAt = at;
            }
            else if (at > entry.LastArrivalAt)
            {
                entry.LastArrivalAt = at;
                var gap = at - entry.SampleStartedAt;
                if (gap >= SameSampleWindow)
                {
                    if (gap <= MaxLearnedGap)
                    {
                        entry.Gaps[entry.Next] = gap.TotalSeconds;
                        entry.Next = (entry.Next + 1) % GapWindow;
                        if (entry.GapCount < GapWindow) entry.GapCount++;
                    }
                    entry.SampleStartedAt = at;
                }
            }
        }

        if (Interlocked.Increment(ref _records) % PruneEvery == 0) Prune(at);
    }

    public StationArrivals? Get(Guid stationId)
    {
        if (!_entries.TryGetValue(stationId, out var entry)) return null;
        lock (entry)
        {
            if (!entry.Initialized) return null;
            TimeSpan? interval = null;
            if (entry.GapCount >= MinGapsForInterval)
            {
                var gaps = entry.Gaps.AsSpan(0, entry.GapCount).ToArray();
                Array.Sort(gaps);
                var mid = gaps.Length / 2;
                var median = gaps.Length % 2 == 1 ? gaps[mid] : (gaps[mid - 1] + gaps[mid]) / 2;
                interval = TimeSpan.FromSeconds(median);
            }
            return new StationArrivals(entry.LastArrivalAt, interval, entry.GapCount);
        }
    }

    /// <summary>Drops stations without an arrival since <paramref name="now"/> minus <see cref="Retention"/>.</summary>
    public void Prune(DateTimeOffset now)
    {
        foreach (var (id, entry) in _entries)
        {
            bool expired;
            lock (entry) expired = entry.Initialized && now - entry.LastArrivalAt > Retention;
            if (expired) _entries.TryRemove(id, out _);
        }
    }
}

/// <summary>Agent connection of one controller bound to a station (from <c>system_metadata.AgentLifecycle</c>).</summary>
/// <param name="LifecycleState">online | stopped | offline; null when the agent never sent lifecycle reports.</param>
/// <param name="LifecycleChangedAt">When that state was reached (receipt of the MQTT last will for offline).</param>
public readonly record struct ControllerConnection(string? LifecycleState, DateTimeOffset? LifecycleChangedAt)
{
    /// <summary>The agent said goodbye (stopped) or its MQTT last will arrived (offline): it publishes nothing more.</summary>
    public bool IsDisconnected =>
        string.Equals(LifecycleState, "offline", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(LifecycleState, "stopped", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads <c>AgentLifecycle.State</c> / <c>LastEventAtUtc</c> from a client PC's system metadata.</summary>
    public static ControllerConnection FromSystemMetadata(JsonDocument? metadata)
    {
        if (metadata == null || metadata.RootElement.ValueKind != JsonValueKind.Object) return default;
        if (!metadata.RootElement.TryGetProperty(AgentLifecycleIngestor.MetadataKey, out var lifecycle) ||
            lifecycle.ValueKind != JsonValueKind.Object)
        {
            return default;
        }
        string? state = lifecycle.TryGetProperty("State", out var s) && s.ValueKind == JsonValueKind.String ? s.GetString() : null;
        string? at = lifecycle.TryGetProperty("LastEventAtUtc", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null;
        return FromValues(state, at);
    }

    public static ControllerConnection FromValues(string? state, string? lastEventAtUtc) =>
        new(string.IsNullOrWhiteSpace(state) ? null : state.Trim(),
            DateTimeOffset.TryParse(lastEventAtUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ? parsed : null);
}

/// <summary>Result of <see cref="StationFreshnessPolicy.Evaluate"/>.</summary>
/// <param name="LiveState">PLC state while fresh, UNKNOWN (fresh, no state), STALE, OFFLINE or NO_DATA.</param>
/// <param name="LastDataAt">Latest of the snapshot timestamp and the last arrival on this instance.</param>
/// <param name="StaleAfter">The station's effective stale threshold.</param>
/// <param name="SampleInterval">Learned sample interval; null while unknown.</param>
public readonly record struct StationFreshnessVerdict(string LiveState, DateTimeOffset? LastDataAt, TimeSpan StaleAfter, TimeSpan? SampleInterval);

/// <summary>Live state of a station from its snapshot, its arrivals and its controllers' agent connections.</summary>
public static class StationFreshnessPolicy
{
    /// <summary>
    /// NO_DATA: never reported. OFFLINE: every bound controller's agent is disconnected (last will /
    /// graceful stop) and no data arrived after the latest disconnect. STALE: the last data is older
    /// than the station's threshold. Otherwise the PLC-reported state (UNKNOWN when it sends none).
    /// A controller without lifecycle reports is never assumed disconnected.
    /// </summary>
    public static StationFreshnessVerdict Evaluate(
        StationTelemetrySnapshot? snapshot,
        StationArrivals? arrivals,
        IReadOnlyCollection<ControllerConnection> controllers,
        DateTimeOffset now,
        StationFreshnessOptions options)
    {
        var interval = arrivals?.SampleInterval;
        var staleAfter = options.StaleAfter(interval);
        var lastData = Latest(snapshot?.LastTelemetryAt, arrivals?.LastArrivalAt);
        if (lastData is not { } last) return new StationFreshnessVerdict("NO_DATA", null, staleAfter, interval);

        if (controllers.Count > 0 && controllers.All(c => c.IsDisconnected))
        {
            var disconnectedAt = controllers.Max(c => c.LifecycleChangedAt);
            if (disconnectedAt is { } lost && last <= lost)
            {
                return new StationFreshnessVerdict("OFFLINE", last, staleAfter, interval);
            }
        }

        var state = now - last > staleAfter ? "STALE" : snapshot?.OperationalStatus ?? "UNKNOWN";
        return new StationFreshnessVerdict(state, last, staleAfter, interval);
    }

    public static DateTimeOffset? Latest(DateTimeOffset? a, DateTimeOffset? b) =>
        a is { } x && b is { } y ? (x >= y ? x : y) : a ?? b;
}
