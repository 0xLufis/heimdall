namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Collections.Generic;

/// <summary>
/// Names of the lifecycle / power report contract "heimdall.lifecycle/1". The authoritative
/// description is the comment above <c>AgentEventMessage</c> in
/// <c>src/shared/App.Contracts/Protos/system_info.proto</c>; the backend parser
/// (<c>AgentLifecycleIngestor</c>) and <c>AgentLifecycleTests</c> pin both sides to it.
/// </summary>
public static class LifecycleContract
{
    public const string Source = "AgentLifecycle";
    public const string Schema = "heimdall.lifecycle/1";

    public static class Phase
    {
        public const string Started = "started";
        public const string Online = "online";
        public const string Stopping = "stopping";
        public const string ConnectionLost = "connection_lost";
    }

    public static class StopReason
    {
        public const string OsShutdown = "os_shutdown";
        public const string OsRestart = "os_restart";
        public const string ServiceStop = "service_stop";
        public const string ServiceRestart = "service_restart";
        public const string AgentFault = "agent_fault";
        public const string Unknown = "unknown";
    }

    public static class Classification
    {
        public const string FirstStart = "first_start";
        public const string OsShutdown = "os_shutdown";
        public const string ServiceStop = "service_stop";
        public const string AgentCrash = "agent_crash";
        public const string AgentKilled = "agent_killed";
        public const string UnexpectedShutdown = "unexpected_shutdown";
        public const string OsShutdownUnreported = "os_shutdown_unreported";
        public const string Unknown = "unknown";
    }

    public static class Initiator
    {
        public const string User = "user";
        public const string Operator = "operator";
        public const string Supervisor = "supervisor";
        public const string Update = "update";
        public const string System = "system";
        public const string None = "none";
        public const string Unknown = "unknown";
    }

    public static class Signal
    {
        public const string ServiceControlStop = "SERVICE_CONTROL_STOP";
        public const string ServiceControlShutdown = "SERVICE_CONTROL_SHUTDOWN";
        public const string SigTerm = "SIGTERM";
        public const string SigInt = "SIGINT";
        public const string SigHup = "SIGHUP";
        public const string SigQuit = "SIGQUIT";
        public const string Intent = "INTENT";
        public const string Host = "HOST";
    }
}

/// <summary>Identity of the running OS boot. Two sessions with the same <see cref="BootId"/> ran without an OS restart in between.</summary>
/// <param name="BootId">Opaque per-boot identifier.</param>
/// <param name="Source">kernel_boot_id | volatile_registry | boot_time (weakest: derived from the wall clock).</param>
/// <param name="BootTimeUtc">Approximate boot time (now minus uptime).</param>
/// <param name="UptimeMs">Milliseconds since boot (monotonic).</param>
public sealed record BootInfo(string BootId, string Source, DateTimeOffset BootTimeUtc, long UptimeMs);

/// <summary>Why and by whom a stop was requested.</summary>
public sealed class StopInfo
{
    public string Reason { get; set; } = LifecycleContract.StopReason.Unknown;
    public string Initiator { get; set; } = LifecycleContract.Initiator.Unknown;
    public string? InitiatedBy { get; set; }
    public bool? Planned { get; set; }
    public string? Signal { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// The durable session marker (<c>state/session.json</c>): written at start with
/// <see cref="CleanShutdown"/> = false, refreshed periodically, and completed with the stop reason
/// only on a graceful stop. The next start reads it to classify how this run ended.
/// </summary>
public sealed class SessionMarker
{
    public string SessionId { get; set; } = string.Empty;
    public string BootId { get; set; } = string.Empty;
    public string BootIdSource { get; set; } = string.Empty;
    public DateTimeOffset OsBootedAtUtc { get; set; }
    public DateTimeOffset ProcessStartedAtUtc { get; set; }
    public string AgentVersion { get; set; } = string.Empty;
    public int ProcessId { get; set; }

    /// <summary>Last time the agent was known to be running (wall clock) - the best estimate of when an abrupt end happened.</summary>
    public DateTimeOffset LastAliveAtUtc { get; set; }
    /// <summary>OS uptime at <see cref="LastAliveAtUtc"/>; an uptime lower than this on the next start proves a reboot.</summary>
    public long LastAliveUptimeMs { get; set; }

    /// <summary>Set only by a graceful stop.</summary>
    public bool CleanShutdown { get; set; }
    public StopInfo? Stop { get; set; }

    /// <summary>A stop the agent itself decided on (supervised restart, update), recorded before it is carried out.</summary>
    public StopInfo? PendingIntent { get; set; }

    /// <summary>How many runs in a row ended abnormally shortly after starting (drives the start-up back-off).</summary>
    public int ConsecutiveAbnormalStarts { get; set; }
}

/// <summary>
/// Compact record of an unhandled exception, written durably before the process dies
/// (<c>state/crash.json</c>) and reported with the next "started" lifecycle report.
/// </summary>
public sealed class CrashRecord
{
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset AtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ExceptionType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>SHA-256 (first 16 hex chars) over exception type and stack frames: equal for the same crash site.</summary>
    public string StackHash { get; set; } = string.Empty;
    public List<string> TopFrames { get; set; } = new();
    public string AgentVersion { get; set; } = string.Empty;
    /// <summary>What the agent was doing (most recent activity breadcrumbs, newest last).</summary>
    public string Activity { get; set; } = string.Empty;
    /// <summary>"unhandled_exception" | "background_service" | "startup".</summary>
    public string Origin { get; set; } = "unhandled_exception";
    public bool Terminating { get; set; } = true;
    public int ProcessId { get; set; }
}

/// <summary>One piece of evidence the operating system kept about a shutdown, restart or crash.</summary>
public sealed class OsEvidence
{
    /// <summary>windows_eventlog | windows_registry | linux_journal | linux_wtmp.</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>shutdown_initiated | unexpected_shutdown | kernel_power | bugcheck | clean_shutdown | service_crashed.</summary>
    public string Kind { get; set; } = string.Empty;
    public int? EventId { get; set; }
    public DateTimeOffset AtUtc { get; set; }
    public string? User { get; set; }
    public string? Process { get; set; }
    public string? Reason { get; set; }
    public string? ReasonCode { get; set; }
    /// <summary>"restart" | "shutdown" | "power off" as the OS names it.</summary>
    public string? ShutdownType { get; set; }
    public bool? Planned { get; set; }
    public string? Comment { get; set; }
    public string? BugcheckCode { get; set; }

    public static class Kinds
    {
        public const string ShutdownInitiated = "shutdown_initiated";
        public const string UnexpectedShutdown = "unexpected_shutdown";
        public const string KernelPower = "kernel_power";
        public const string Bugcheck = "bugcheck";
        public const string CleanShutdown = "clean_shutdown";
        public const string ServiceCrashed = "service_crashed";
    }
}

/// <summary>The classification of the previous run, as sent in <c>previous</c> of a "started" report.</summary>
public sealed class PreviousRunAssessment
{
    public string Classification { get; set; } = LifecycleContract.Classification.FirstStart;
    public string? Cause { get; set; }
    public string? OsAction { get; set; }
    public string Initiator { get; set; } = LifecycleContract.Initiator.Unknown;
    public string? InitiatedBy { get; set; }
    public bool? Planned { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? BootId { get; set; }
    public string? AgentVersion { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? LastSeenAliveAtUtc { get; set; }
    public DateTimeOffset? EndedAtUtc { get; set; }
    public bool CleanShutdown { get; set; }
    public bool SameBoot { get; set; }
    public long? DowntimeSeconds { get; set; }
    public CrashRecord? Crash { get; set; }
    public List<OsEvidence> Evidence { get; set; } = new();

    /// <summary>Severity of this outcome for the event level: Info, Warning, Error or Critical.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Level => Classification switch
    {
        LifecycleContract.Classification.UnexpectedShutdown => "Critical",
        LifecycleContract.Classification.AgentCrash or LifecycleContract.Classification.AgentKilled => "Error",
        LifecycleContract.Classification.OsShutdownUnreported or LifecycleContract.Classification.Unknown => "Warning",
        _ => "Info"
    };
}
