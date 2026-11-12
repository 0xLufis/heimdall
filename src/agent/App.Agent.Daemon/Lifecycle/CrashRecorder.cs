namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using App.Agent.Daemon.Infrastructure.Persistence;

/// <summary>Identity of this agent process, shared by the lifecycle report and the crash recorder.</summary>
public sealed class AgentSession
{
    public AgentSession(string? sessionId = null, string? agentVersion = null, DateTimeOffset? processStartedAtUtc = null)
    {
        SessionId = sessionId ?? Guid.NewGuid().ToString("N");
        AgentVersion = agentVersion ?? CurrentAgentVersion();
        ProcessStartedAtUtc = processStartedAtUtc ?? DateTimeOffset.UtcNow;
    }

    public string SessionId { get; }
    public string AgentVersion { get; }
    public DateTimeOffset ProcessStartedAtUtc { get; }

    /// <summary>Same rule as <c>/api/v1/agent/status</c>: HEIMDALL_AGENT_VERSION, else the assembly version.</summary>
    public static string CurrentAgentVersion() =>
        Environment.GetEnvironmentVariable("HEIMDALL_AGENT_VERSION")
        ?? typeof(AgentSession).Assembly.GetName().Version?.ToString(3)
        ?? "1.0.0";
}

/// <summary>
/// "What was the agent doing" breadcrumbs for crash records. Updated by the main loops at each
/// step; reading and writing are lock-free so the crash handler can always read it.
/// </summary>
public static class AgentActivity
{
    private static string _current = "starting";

    public static string Current => Volatile.Read(ref _current);

    public static void Set(string activity) => Volatile.Write(ref _current, activity ?? string.Empty);
}

/// <summary>
/// Persists a compact crash record (exception type, message, stack hash and top frames, time,
/// agent version, current activity) durably before the process dies, so the next start can report
/// "agent_crash" with the cause. Installed in <c>Program.cs</c> before the host is built, so
/// crashes during start-up are captured too.
/// </summary>
public sealed class CrashRecorder
{
    public const int MaxFrames = 8;
    public const int MaxMessageLength = 1000;

    private static CrashRecorder? _installed;
    private readonly DurableJsonFile<CrashRecord> _file;
    private readonly AgentSession _session;
    private long _unobservedTaskExceptions;

    public CrashRecorder(string crashRecordFile, AgentSession session)
    {
        _file = new DurableJsonFile<CrashRecord>(crashRecordFile, r => string.IsNullOrWhiteSpace(r.ExceptionType) ? "crash record without exception type" : null);
        _session = session;
    }

    public string FilePath => _file.Path;

    /// <summary>Unobserved task exceptions seen by this process (not fatal since .NET 4.5, counted for diagnostics).</summary>
    public long UnobservedTaskExceptions => Interlocked.Read(ref _unobservedTaskExceptions);

    /// <summary>Hooks the process-wide handlers. Idempotent per process.</summary>
    public static CrashRecorder Install(string crashRecordFile, AgentSession session)
    {
        var recorder = new CrashRecorder(crashRecordFile, session);
        if (Interlocked.CompareExchange(ref _installed, recorder, null) != null)
        {
            return _installed!;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) recorder.Record(ex, "unhandled_exception", e.IsTerminating);
        };
        TaskScheduler.UnobservedTaskException += (_, _) => Interlocked.Increment(ref recorder._unobservedTaskExceptions);
        return recorder;
    }

    /// <summary>Writes the crash record durably. Never throws (it runs inside a dying process).</summary>
    public bool Record(Exception exception, string origin, bool terminating = true)
    {
        try
        {
            var record = Build(exception, origin, terminating, _session, AgentActivity.Current);
            return _file.TrySave(record, out _);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>The crash record left by an earlier process, or null. A damaged record is treated as absent.</summary>
    public CrashRecord? ReadPrevious(out DurableLoad<CrashRecord> load)
    {
        load = _file.Load();
        return load.Value;
    }

    /// <summary>Removes the record once it has been taken into the lifecycle report.</summary>
    public void Clear() => _file.Delete();

    public static CrashRecord Build(Exception exception, string origin, bool terminating, AgentSession session, string activity)
    {
        var innermost = exception;
        while (innermost is AggregateException { InnerExceptions.Count: 1 } aggregate) innermost = aggregate.InnerExceptions[0];

        string[] frames = SafeFrames(innermost);
        string hashInput = innermost.GetType().FullName + "\n" + string.Join("\n", frames);

        string message = innermost.Message ?? string.Empty;
        if (message.Length > MaxMessageLength) message = message[..MaxMessageLength] + "...";

        return new CrashRecord
        {
            SessionId = session.SessionId,
            AtUtc = DateTimeOffset.UtcNow,
            ExceptionType = innermost.GetType().FullName ?? innermost.GetType().Name,
            Message = message,
            StackHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)))[..16].ToLowerInvariant(),
            TopFrames = frames.Take(MaxFrames).ToList(),
            AgentVersion = session.AgentVersion,
            Activity = activity,
            Origin = origin,
            Terminating = terminating,
            ProcessId = Environment.ProcessId
        };
    }

    /// <summary>Method names only (no file paths, no line numbers): stable across builds, no user data.</summary>
    private static string[] SafeFrames(Exception exception)
    {
        try
        {
            var trace = new StackTrace(exception, fNeedFileInfo: false);
            var frames = trace.GetFrames()
                .Select(f => f.GetMethod())
                .Where(m => m != null)
                .Select(m => $"{m!.DeclaringType?.FullName ?? "?"}.{m.Name}")
                .ToArray();
            if (frames.Length > 0) return frames;
        }
        catch
        {
            // Fall back to the textual trace.
        }

        return (exception.StackTrace ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.StartsWith("at ", StringComparison.Ordinal) ? line[3..] : line)
            .Select(line => line.Contains(" in ", StringComparison.Ordinal) ? line[..line.IndexOf(" in ", StringComparison.Ordinal)] : line)
            .ToArray();
    }
}
