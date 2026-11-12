namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using C = LifecycleContract.Classification;
using I = LifecycleContract.Initiator;
using R = LifecycleContract.StopReason;
using S = LifecycleContract.Signal;

/// <summary>Reads the identity of the running OS boot. Never throws.</summary>
public static class BootInfoProvider
{
    public const string KernelBootId = "kernel_boot_id";
    public const string VolatileRegistry = "volatile_registry";
    public const string BootTime = "boot_time";

    public static BootInfo Read()
    {
        long uptimeMs = Environment.TickCount64;
        var bootTime = DateTimeOffset.UtcNow - TimeSpan.FromMilliseconds(uptimeMs);

        try
        {
            if (OperatingSystem.IsLinux() && File.Exists("/proc/sys/kernel/random/boot_id"))
            {
                string id = File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim();
                if (id.Length >= 8) return new BootInfo(id, KernelBootId, bootTime, uptimeMs);
            }

            if (OperatingSystem.IsWindows())
            {
                string? id = ReadWindowsBootSession();
                if (id != null) return new BootInfo(id, VolatileRegistry, bootTime, uptimeMs);
            }
        }
        catch
        {
            // Fall through to the clock-derived identifier.
        }

        // Weakest form: derived from the wall clock, so a clock correction can shift it. The
        // classifier therefore compares such identifiers by boot time with a tolerance.
        return new BootInfo("bt-" + bootTime.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture), BootTime, bootTime, uptimeMs);
    }

    /// <summary>
    /// A volatile registry key exists only until the next OS restart, whatever the clock does: if it is
    /// there, an earlier agent session of this boot created it; if not, this is a new boot.
    /// Needs write access to HKLM (the agent service account has it); returns null otherwise.
    /// </summary>
    private static string? ReadWindowsBootSession()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Heimdall\BootSession", true, Microsoft.Win32.RegistryOptions.Volatile);
            if (key == null) return null;
            if (key.GetValue("BootId") is string existing && existing.Length >= 8) return existing;

            string created = Guid.NewGuid().ToString("N");
            key.SetValue("BootId", created);
            return created;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Turns "what told the agent to stop" into the stop reason of the contract.</summary>
public static class StopReasonResolver
{
    /// <param name="signal">The trigger (<see cref="LifecycleContract.Signal"/>), or null when the host stopped by itself.</param>
    /// <param name="isWindows">Platform the agent runs on.</param>
    /// <param name="systemState">Linux: output of <c>systemctl is-system-running</c> ("stopping" during shutdown), or null.</param>
    /// <param name="systemJobs">Linux: output of <c>systemctl list-jobs</c> (names reboot/poweroff/halt targets), or null.</param>
    /// <param name="faulted">True when the host is stopping because one of the agent's own services failed.</param>
    public static StopInfo Resolve(string? signal, bool isWindows, string? systemState = null, string? systemJobs = null, bool faulted = false)
    {
        var stop = new StopInfo { Signal = signal ?? S.Host };

        switch (signal)
        {
            case S.ServiceControlShutdown:
                stop.Reason = R.OsShutdown;
                stop.Initiator = I.Unknown;
                stop.Detail = "the service control manager announced a system shutdown";
                break;

            case S.ServiceControlStop:
                stop.Reason = R.ServiceStop;
                stop.Initiator = I.Operator;
                stop.Detail = "the service was stopped through the service control manager";
                break;

            case S.SigTerm when isWindows:
                // .NET maps CTRL_SHUTDOWN_EVENT to SIGTERM: a console-mode agent is told the system goes down.
                stop.Reason = R.OsShutdown;
                stop.Initiator = I.Unknown;
                stop.Detail = "CTRL_SHUTDOWN_EVENT";
                break;

            case S.SigTerm:
                bool stopping = string.Equals(systemState?.Trim(), "stopping", StringComparison.OrdinalIgnoreCase);
                if (stopping)
                {
                    bool reboot = systemJobs != null && (systemJobs.Contains("reboot.target", StringComparison.Ordinal) || systemJobs.Contains("kexec.target", StringComparison.Ordinal));
                    stop.Reason = reboot ? R.OsRestart : R.OsShutdown;
                    stop.Initiator = I.Unknown;
                    stop.Detail = "SIGTERM while systemd reports the system as stopping";
                }
                else
                {
                    stop.Reason = R.ServiceStop;
                    stop.Initiator = I.Operator;
                    stop.Detail = "SIGTERM while the system keeps running (service stop or restart)";
                }
                break;

            case S.SigInt:
            case S.SigQuit:
                stop.Reason = R.ServiceStop;
                stop.Initiator = I.User;
                stop.Detail = "interrupted from the console";
                break;

            case S.SigHup:
                stop.Reason = R.ServiceStop;
                stop.Initiator = I.Operator;
                stop.Detail = isWindows ? "console closed (CTRL_CLOSE_EVENT)" : "terminal hang-up";
                break;

            default:
                stop.Reason = faulted ? R.AgentFault : R.Unknown;
                stop.Initiator = faulted ? I.None : I.Unknown;
                stop.Detail = faulted
                    ? "the host is stopping because an agent service failed"
                    : "the host was asked to stop without a signal or service control request";
                break;
        }

        return stop;
    }
}

/// <summary>
/// Decides how the previous agent run ended from what survived it: the session marker, the crash
/// record, the identity of the current OS boot and whatever the OS logged. Pure, no I/O.
/// <list type="table">
/// <item><term>clean flag, OS shutdown reason</term><description><c>os_shutdown</c></description></item>
/// <item><term>clean flag, any other reason</term><description><c>service_stop</c> (or <c>agent_crash</c> for an internal fault)</description></item>
/// <item><term>no clean flag, same boot, crash record</term><description><c>agent_crash</c></description></item>
/// <item><term>no clean flag, same boot, no crash record</term><description><c>agent_killed</c> (or <c>service_stop</c> when a stop intent was recorded)</description></item>
/// <item><term>no clean flag, boot changed</term><description><c>unexpected_shutdown</c>, unless the OS logged an orderly shutdown: <c>os_shutdown_unreported</c></description></item>
/// </list>
/// </summary>
public static class PreviousRunClassifier
{
    /// <summary>Tolerance when two boots can only be compared by their clock-derived boot time.</summary>
    public static readonly TimeSpan BootTimeTolerance = TimeSpan.FromMinutes(2);

    public static PreviousRunAssessment Classify(
        SessionMarker? previous,
        bool markerDamaged,
        BootInfo currentBoot,
        CrashRecord? crash,
        IReadOnlyList<OsEvidence>? evidence,
        string currentAgentVersion,
        DateTimeOffset nowUtc)
    {
        evidence ??= Array.Empty<OsEvidence>();

        if (previous == null)
        {
            if (crash != null)
            {
                return new PreviousRunAssessment
                {
                    Classification = C.AgentCrash,
                    Initiator = I.None,
                    Reason = "a crash record exists but the session marker is " + (markerDamaged ? "unreadable" : "missing"),
                    Crash = crash,
                    EndedAtUtc = crash.AtUtc
                };
            }

            return markerDamaged
                ? new PreviousRunAssessment { Classification = C.Unknown, Initiator = I.Unknown, Reason = "the session marker of the previous run was unreadable" }
                : new PreviousRunAssessment { Classification = C.FirstStart, Initiator = I.None, Reason = "no earlier run is known on this machine" };
        }

        bool sameBoot = IsSameBoot(previous, currentBoot);
        var endReference = previous.Stop?.AtUtc ?? previous.LastAliveAtUtc;
        var result = new PreviousRunAssessment
        {
            SessionId = previous.SessionId,
            BootId = previous.BootId,
            AgentVersion = previous.AgentVersion,
            StartedAtUtc = previous.ProcessStartedAtUtc,
            LastSeenAliveAtUtc = previous.LastAliveAtUtc,
            CleanShutdown = previous.CleanShutdown,
            SameBoot = sameBoot,
            DowntimeSeconds = Math.Max(0, (long)(nowUtc - endReference).TotalSeconds),
            Crash = crash
        };

        // Only what the OS logged around or after the end of that run says anything about it.
        var relevant = evidence
            .Where(e => e.AtUtc >= previous.LastAliveAtUtc - TimeSpan.FromMinutes(30))
            .OrderBy(e => e.AtUtc)
            .ToList();
        result.Evidence = relevant;
        var initiated = relevant.LastOrDefault(e => e.Kind == OsEvidence.Kinds.ShutdownInitiated);
        bool versionChanged = !string.IsNullOrEmpty(previous.AgentVersion) && previous.AgentVersion != currentAgentVersion;

        if (previous.CleanShutdown)
        {
            var stop = previous.Stop ?? new StopInfo();
            result.EndedAtUtc = stop.AtUtc;
            result.Initiator = stop.Initiator;
            result.InitiatedBy = stop.InitiatedBy;
            result.Planned = stop.Planned;

            if (stop.Reason is R.OsShutdown or R.OsRestart)
            {
                result.Classification = C.OsShutdown;
                result.OsAction = stop.Reason == R.OsRestart ? "restart" : null;
                ApplyInitiation(result, initiated);
                result.OsAction ??= "shutdown";
                result.Reason = Describe(result, stop.Detail);
            }
            else if (stop.Reason == R.AgentFault)
            {
                result.Classification = C.AgentCrash;
                result.Initiator = I.None;
                result.Reason = stop.Detail ?? "the agent stopped itself after an internal failure";
            }
            else
            {
                result.Classification = C.ServiceStop;
                if (versionChanged && stop.Initiator is I.Operator or I.Unknown)
                {
                    result.Initiator = I.Update;
                }
                result.Reason = (stop.Detail ?? "the agent was stopped while the OS kept running")
                                + (versionChanged ? $"; agent version changed from {previous.AgentVersion} to {currentAgentVersion}" : string.Empty)
                                + (sameBoot ? string.Empty : "; the OS was restarted while the agent was stopped");
            }

            return result;
        }

        // ---- No goodbye was recorded ----------------------------------------------------------
        if (sameBoot)
        {
            if (crash != null)
            {
                result.Classification = C.AgentCrash;
                result.Initiator = I.None;
                result.EndedAtUtc = crash.AtUtc;
                result.Reason = $"unhandled {crash.ExceptionType}: {crash.Message}";
            }
            else if (previous.PendingIntent != null)
            {
                // The agent had decided to stop (supervised restart, update) and was gone before the goodbye.
                result.Classification = C.ServiceStop;
                result.Initiator = previous.PendingIntent.Initiator;
                result.EndedAtUtc = previous.PendingIntent.AtUtc;
                result.Reason = (previous.PendingIntent.Detail ?? "stop requested by the agent") + "; the process ended before the stop completed";
            }
            else
            {
                result.Classification = C.AgentKilled;
                result.Initiator = I.Unknown;
                result.Reason = "the process ended without a goodbye and without a crash record while the OS kept running " +
                                "(killed, out of memory, native crash or forced service stop)";
            }

            return result;
        }

        bool bugcheck = relevant.Any(e => e.Kind == OsEvidence.Kinds.Bugcheck ||
                                          (e.Kind == OsEvidence.Kinds.KernelPower && !string.IsNullOrEmpty(e.BugcheckCode)));
        bool unexpected = bugcheck || relevant.Any(e => e.Kind is OsEvidence.Kinds.UnexpectedShutdown or OsEvidence.Kinds.KernelPower);
        bool orderly = initiated != null || relevant.Any(e => e.Kind == OsEvidence.Kinds.CleanShutdown);

        if (!unexpected && orderly)
        {
            result.Classification = C.OsShutdownUnreported;
            result.Initiator = I.Unknown;
            ApplyInitiation(result, initiated);
            result.OsAction ??= "shutdown";
            result.EndedAtUtc = initiated?.AtUtc ?? relevant.Last(e => e.Kind == OsEvidence.Kinds.CleanShutdown).AtUtc;
            result.Reason = Describe(result, "the OS logged an orderly shutdown, but the agent was ended before it could report it");
            return result;
        }

        result.Classification = C.UnexpectedShutdown;
        result.Initiator = I.None;
        result.Cause = bugcheck ? "os_crash" : unexpected ? "power_loss_or_reset" : null;
        result.Reason = bugcheck
            ? "the OS crashed (bug check) while the agent was running"
            : unexpected
                ? "the OS logged an unexpected shutdown (power loss or hard reset) while the agent was running"
                : "no clean-shutdown flag and the OS has booted since: power loss, hard reset or OS crash";
        return result;
    }

    /// <summary>
    /// True when no OS restart happened between the previous session and now. A lower uptime than the
    /// previous session last saw proves a reboot; otherwise OS-provided boot identifiers decide, and
    /// clock-derived ones are compared with a tolerance.
    /// </summary>
    public static bool IsSameBoot(SessionMarker previous, BootInfo current)
    {
        if (current.UptimeMs < previous.LastAliveUptimeMs) return false;

        bool strongIds = current.Source != BootInfoProvider.BootTime && previous.BootIdSource == current.Source;
        if (strongIds) return string.Equals(previous.BootId, current.BootId, StringComparison.Ordinal);

        return (current.BootTimeUtc - previous.OsBootedAtUtc).Duration() <= BootTimeTolerance;
    }

    /// <summary>Who started a shutdown, as far as the account the OS logged tells.</summary>
    public static string InitiatorOf(string? user)
    {
        if (string.IsNullOrWhiteSpace(user)) return I.Unknown;
        string u = user.Trim();
        return u.EndsWith("\\SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               u.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase) ||
               u.StartsWith("NT SERVICE\\", StringComparison.OrdinalIgnoreCase) ||
               u.EndsWith("\\LOCAL SERVICE", StringComparison.OrdinalIgnoreCase) ||
               u.EndsWith("\\NETWORK SERVICE", StringComparison.OrdinalIgnoreCase)
            ? I.System
            : I.User;
    }

    private static void ApplyInitiation(PreviousRunAssessment result, OsEvidence? initiated)
    {
        if (initiated == null) return;

        result.InitiatedBy ??= initiated.User;
        result.Planned ??= initiated.Planned;
        if (result.Initiator is I.Unknown or I.Operator) result.Initiator = InitiatorOf(initiated.User);
        if (!string.IsNullOrEmpty(initiated.ShutdownType))
        {
            result.OsAction = initiated.ShutdownType.Contains("restart", StringComparison.OrdinalIgnoreCase) ||
                              initiated.ShutdownType.Contains("reboot", StringComparison.OrdinalIgnoreCase)
                ? "restart"
                : "shutdown";
        }
    }

    private static string Describe(PreviousRunAssessment result, string? detail)
    {
        var evidence = result.Evidence.LastOrDefault(e => e.Kind == OsEvidence.Kinds.ShutdownInitiated);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(evidence?.Reason)) parts.Add(evidence!.Reason!);
        if (!string.IsNullOrWhiteSpace(evidence?.Comment)) parts.Add("comment: " + evidence!.Comment);
        if (!string.IsNullOrWhiteSpace(evidence?.Process)) parts.Add("by " + evidence!.Process);
        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(detail)) parts.Add(detail!);
        return string.Join("; ", parts);
    }
}
