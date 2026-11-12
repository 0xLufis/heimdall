namespace App.Agent.Daemon.Lifecycle;

using System;
using App.Agent.Daemon.Infrastructure.Persistence;

/// <summary>What the start of a session found about the run before it.</summary>
public sealed record PreviousRunContext(SessionMarker? Marker, bool MarkerDamaged, CrashRecord? Crash, string? MarkerDetail);

/// <summary>
/// The durable session marker (<c>state/session.json</c>). Written at start with the clean flag
/// cleared, refreshed by <see cref="Heartbeat"/>, and completed with the stop reason only on a
/// graceful stop, so the next start can tell a clean stop from a crash (same OS boot, no flag) and
/// from a power loss / OS crash (OS booted since, no flag).
/// </summary>
public sealed class SessionTracker
{
    /// <summary>A run shorter than this that ends abnormally counts towards the start-up back-off.</summary>
    public static readonly TimeSpan ShortRun = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly DurableJsonFile<SessionMarker> _file;
    private readonly object _lock = new();
    private SessionMarker? _current;

    public SessionTracker(string markerFile)
    {
        _file = new DurableJsonFile<SessionMarker>(markerFile,
            m => string.IsNullOrWhiteSpace(m.SessionId) || string.IsNullOrWhiteSpace(m.BootId) ? "session marker without session or boot id" : null);
    }

    public string FilePath => _file.Path;

    public SessionMarker? Current
    {
        get { lock (_lock) return _current; }
    }

    /// <summary>How long to wait before starting the collectors, because the previous runs kept ending abnormally right after start.</summary>
    public TimeSpan StartupBackoff { get; private set; } = TimeSpan.Zero;

    /// <summary>
    /// Reads what the previous run left behind and writes the marker of this run (clean flag cleared).
    /// Never throws; a marker that cannot be written only weakens the next classification.
    /// </summary>
    public PreviousRunContext Begin(AgentSession session, BootInfo boot, CrashRecord? crash)
    {
        var loaded = _file.Load();
        var previous = loaded.Value;

        // A crash record belongs to the run it was written by; one left over from an older run is stale.
        if (crash != null && previous != null && !string.IsNullOrEmpty(crash.SessionId) && crash.SessionId != previous.SessionId)
        {
            crash = null;
        }

        int abnormalStarts = 0;
        if (previous != null && !previous.CleanShutdown && PreviousRunClassifier.IsSameBoot(previous, boot) &&
            previous.LastAliveAtUtc - previous.ProcessStartedAtUtc < ShortRun)
        {
            abnormalStarts = previous.ConsecutiveAbnormalStarts + 1;
        }
        StartupBackoff = BackoffFor(abnormalStarts);

        var marker = new SessionMarker
        {
            SessionId = session.SessionId,
            BootId = boot.BootId,
            BootIdSource = boot.Source,
            OsBootedAtUtc = boot.BootTimeUtc,
            ProcessStartedAtUtc = session.ProcessStartedAtUtc,
            AgentVersion = session.AgentVersion,
            ProcessId = Environment.ProcessId,
            LastAliveAtUtc = DateTimeOffset.UtcNow,
            LastAliveUptimeMs = boot.UptimeMs,
            CleanShutdown = false,
            ConsecutiveAbnormalStarts = abnormalStarts
        };

        lock (_lock)
        {
            _current = marker;
            _file.TrySave(marker, out _);
        }

        return new PreviousRunContext(previous, loaded.PrimaryWasDamaged && previous == null, crash, loaded.Detail);
    }

    /// <summary>Records that the agent is still alive (and the OS uptime it saw). Never throws.</summary>
    public void Heartbeat(BootInfo boot)
    {
        lock (_lock)
        {
            if (_current == null || _current.CleanShutdown) return;
            _current.LastAliveAtUtc = DateTimeOffset.UtcNow;
            _current.LastAliveUptimeMs = boot.UptimeMs;
            _file.TrySave(_current, out _);
        }
    }

    /// <summary>Records a stop the agent itself decided on, before carrying it out.</summary>
    public void RecordIntent(StopInfo intent)
    {
        lock (_lock)
        {
            if (_current == null) return;
            _current.PendingIntent = intent;
            _file.TrySave(_current, out _);
        }
    }

    /// <summary>Sets the clean-shutdown flag with the stop reason. Called once, as early as possible in a graceful stop.</summary>
    public bool CompleteGracefully(StopInfo stop)
    {
        lock (_lock)
        {
            if (_current == null) return false;
            _current.CleanShutdown = true;
            _current.Stop = stop;
            _current.LastAliveAtUtc = DateTimeOffset.UtcNow;
            _current.LastAliveUptimeMs = Environment.TickCount64;
            // A graceful end breaks a crash loop.
            _current.ConsecutiveAbnormalStarts = stop.Reason == LifecycleContract.StopReason.AgentFault ? _current.ConsecutiveAbnormalStarts : 0;
            return _file.TrySave(_current, out _);
        }
    }

    public static TimeSpan BackoffFor(int consecutiveAbnormalStarts)
    {
        if (consecutiveAbnormalStarts < 2) return TimeSpan.Zero;
        double seconds = 5 * Math.Pow(2, Math.Min(consecutiveAbnormalStarts - 2, 10));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }
}
