namespace App.Agent.Daemon.Lifecycle;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>Reads what the operating system recorded about the last shutdown, restart or crash.</summary>
public interface IOsShutdownEvidenceProvider
{
    /// <summary>
    /// Best effort: returns whatever could be read within <paramref name="timeout"/>, an empty list
    /// where the OS keeps no such record or access is denied. Never throws.
    /// </summary>
    IReadOnlyList<OsEvidence> Collect(DateTimeOffset sinceUtc, TimeSpan timeout);
}

/// <summary>
/// Windows: System event log - 1074 (User32: process/user initiated a shutdown or restart, with reason,
/// reason code, planned flag and comment), 6006 (event log stopped = orderly shutdown), 6008 (previous
/// shutdown was unexpected), 41 (Kernel-Power: rebooted without cleanly shutting down; bug check code
/// when the OS crashed), 1001 (BugCheck: rebooted from a bug check), 7034 (the Heimdall service
/// terminated unexpectedly).
/// Linux: journal of the previous boot (needs a persistent journal) - systemd-logind "System is
/// rebooting/powering down", "Power key pressed", sudo commands running reboot/shutdown/poweroff
/// (initiating user), kernel "reboot: Restarting system" / "Power down", kernel panics.
/// </summary>
public sealed class OsShutdownEvidenceProvider : IOsShutdownEvidenceProvider
{
    public const int MaxEvents = 50;

    public IReadOnlyList<OsEvidence> Collect(DateTimeOffset sinceUtc, TimeSpan timeout)
    {
        try
        {
            if (OperatingSystem.IsWindows()) return ReadWindowsEventLog(sinceUtc, timeout);
            if (OperatingSystem.IsLinux()) return ParseLinuxJournal(RunCommand("journalctl", "-b -1 -q --no-pager -o short-iso -n 400", timeout), sinceUtc);
        }
        catch
        {
            // Evidence is optional; the classification works without it.
        }
        return Array.Empty<OsEvidence>();
    }

    private static List<OsEvidence> ReadWindowsEventLog(DateTimeOffset sinceUtc, TimeSpan timeout)
    {
        var result = new List<OsEvidence>();
        if (!OperatingSystem.IsWindows()) return result;

        var watch = Stopwatch.StartNew();
        string since = sinceUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        string xpath = "*[System[(EventID=1074 or EventID=6006 or EventID=6008 or EventID=41 or EventID=1001 or EventID=7034)" +
                       $" and TimeCreated[@SystemTime>='{since}']]]";

        var query = new System.Diagnostics.Eventing.Reader.EventLogQuery("System", System.Diagnostics.Eventing.Reader.PathType.LogName, xpath)
        {
            ReverseDirection = true
        };
        using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader(query);
        for (int i = 0; i < MaxEvents && watch.Elapsed < timeout; i++)
        {
            using var record = reader.ReadEvent(timeout);
            if (record == null) break;

            var evidence = MapWindowsEvent(record.Id, record.ProviderName ?? string.Empty,
                record.Properties.Select(p => p.Value?.ToString()).ToList(),
                record.TimeCreated.HasValue ? new DateTimeOffset(record.TimeCreated.Value.ToUniversalTime(), TimeSpan.Zero) : DateTimeOffset.UtcNow);
            if (evidence != null) result.Add(evidence);
        }

        result.Reverse(); // oldest first
        return result;
    }

    /// <summary>Maps one System-log event to evidence (public for tests; the layouts are documented by Microsoft).</summary>
    public static OsEvidence? MapWindowsEvent(int eventId, string provider, IReadOnlyList<string?> properties, DateTimeOffset atUtc)
    {
        string? Prop(int index) => index < properties.Count && !string.IsNullOrWhiteSpace(properties[index]) ? properties[index]!.Trim() : null;
        var evidence = new OsEvidence { Source = "windows_eventlog", EventId = eventId, AtUtc = atUtc };

        switch (eventId)
        {
            case 1074 when provider.Contains("User32", StringComparison.OrdinalIgnoreCase):
                // 0 process, 1 computer, 2 reason text, 3 reason code, 4 shutdown type, 5 comment, 6 user
                evidence.Kind = OsEvidence.Kinds.ShutdownInitiated;
                evidence.Process = Prop(0);
                evidence.Reason = Prop(2);
                evidence.ReasonCode = Prop(3);
                evidence.ShutdownType = Prop(4);
                evidence.Comment = Prop(5);
                evidence.User = Prop(6);
                evidence.Planned = ParsePlanned(evidence.ReasonCode, evidence.Reason);
                return evidence;

            case 6006 when provider.Contains("EventLog", StringComparison.OrdinalIgnoreCase):
                evidence.Kind = OsEvidence.Kinds.CleanShutdown;
                return evidence;

            case 6008 when provider.Contains("EventLog", StringComparison.OrdinalIgnoreCase):
                evidence.Kind = OsEvidence.Kinds.UnexpectedShutdown;
                evidence.Reason = "The previous system shutdown was unexpected";
                return evidence;

            case 41 when provider.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase):
                // 0 BugcheckCode, 1-4 parameters, 5 SleepInProgress, 6 PowerButtonTimestamp
                evidence.Kind = OsEvidence.Kinds.KernelPower;
                if (long.TryParse(Prop(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) && code != 0)
                {
                    evidence.BugcheckCode = "0x" + code.ToString("x8", CultureInfo.InvariantCulture);
                }
                evidence.Reason = Prop(6) is { } button && button != "0"
                    ? "rebooted without a clean shutdown (power button held)"
                    : "rebooted without a clean shutdown (power loss, hard reset, hang or crash)";
                return evidence;

            case 1001 when provider.Contains("WER-SystemErrorReporting", StringComparison.OrdinalIgnoreCase) || provider.Contains("BugCheck", StringComparison.OrdinalIgnoreCase):
                evidence.Kind = OsEvidence.Kinds.Bugcheck;
                evidence.BugcheckCode = Prop(0)?.Split(' ', 2)[0];
                evidence.Reason = "the computer has rebooted from a bug check";
                return evidence;

            case 7034 when (Prop(0) ?? string.Empty).Contains("heimdall", StringComparison.OrdinalIgnoreCase):
                evidence.Kind = OsEvidence.Kinds.ServiceCrashed;
                evidence.Process = Prop(0);
                evidence.Reason = "the service terminated unexpectedly";
                return evidence;
        }

        return null;
    }

    private static bool? ParsePlanned(string? reasonCode, string? reasonText)
    {
        if (reasonCode != null && reasonCode.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(reasonCode.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
        {
            return (code & 0x80000000) != 0; // SHTDN_REASON_FLAG_PLANNED
        }
        if (reasonText == null) return null;
        if (reasonText.Contains("(Unplanned)", StringComparison.OrdinalIgnoreCase)) return false;
        if (reasonText.Contains("(Planned)", StringComparison.OrdinalIgnoreCase)) return true;
        return null;
    }

    private static readonly Regex JournalLine = new(@"^(?<ts>\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:[.,]\d+)?(?:[+-]\d\d:?\d\d|Z))\s+\S+\s+(?<proc>[^:\[]+)(?:\[\d+\])?:\s(?<msg>.*)$", RegexOptions.Compiled);
    private static readonly Regex SudoCommand = new(@"^\s*(?<user>[^ :]+)\s*:.*COMMAND=(?<cmd>.*)$", RegexOptions.Compiled);

    /// <summary>Parses <c>journalctl -o short-iso</c> output of the previous boot (public for tests).</summary>
    public static List<OsEvidence> ParseLinuxJournal(string? output, DateTimeOffset sinceUtc)
    {
        var result = new List<OsEvidence>();
        if (string.IsNullOrWhiteSpace(output)) return result;

        foreach (var raw in output.Split('\n'))
        {
            var match = JournalLine.Match(raw.TrimEnd('\r'));
            if (!match.Success) continue;
            // short-iso writes the offset without a colon (+0100); normalize to +01:00 before parsing.
            string stamp = Regex.Replace(match.Groups["ts"].Value, @"([+-]\d\d)(\d\d)$", "$1:$2");
            if (!DateTimeOffset.TryParse(stamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)) continue;
            if (at < sinceUtc) continue;

            string process = match.Groups["proc"].Value.Trim();
            string message = match.Groups["msg"].Value.Trim();
            var evidence = new OsEvidence { Source = "linux_journal", AtUtc = at.ToUniversalTime(), Process = process };

            if (message.StartsWith("System is rebooting", StringComparison.Ordinal) ||
                message.StartsWith("System is powering down", StringComparison.Ordinal) ||
                message.StartsWith("System is halting", StringComparison.Ordinal))
            {
                evidence.Kind = OsEvidence.Kinds.ShutdownInitiated;
                evidence.ShutdownType = message.Contains("rebooting", StringComparison.Ordinal) ? "restart" : "power off";
                evidence.Reason = message.TrimEnd('.');
            }
            else if (message.StartsWith("Power key pressed", StringComparison.Ordinal))
            {
                evidence.Kind = OsEvidence.Kinds.ShutdownInitiated;
                evidence.User = "power button";
                evidence.Reason = message.TrimEnd('.');
            }
            else if (process == "sudo" && SudoCommand.Match(message) is { Success: true } sudo &&
                     Regex.IsMatch(sudo.Groups["cmd"].Value, @"\b(reboot|shutdown|poweroff|halt)\b"))
            {
                evidence.Kind = OsEvidence.Kinds.ShutdownInitiated;
                evidence.User = sudo.Groups["user"].Value;
                evidence.Reason = "sudo " + sudo.Groups["cmd"].Value.Trim();
                evidence.ShutdownType = sudo.Groups["cmd"].Value.Contains("reboot", StringComparison.Ordinal) ||
                                        sudo.Groups["cmd"].Value.Contains(" -r", StringComparison.Ordinal) ? "restart" : null;
                evidence.Planned = true;
            }
            else if (process == "kernel" && (message.StartsWith("reboot: Restarting system", StringComparison.Ordinal) ||
                                             message.StartsWith("reboot: Power down", StringComparison.Ordinal)))
            {
                evidence.Kind = OsEvidence.Kinds.CleanShutdown;
                evidence.Reason = message;
            }
            else if (process == "kernel" && message.Contains("Kernel panic", StringComparison.Ordinal))
            {
                evidence.Kind = OsEvidence.Kinds.Bugcheck;
                evidence.Reason = message.Length > 200 ? message[..200] : message;
            }
            else
            {
                continue;
            }

            result.Add(evidence);
            if (result.Count >= MaxEvents) break;
        }

        return result;
    }

    /// <summary>Runs a command with a hard timeout; returns stdout or null. Never throws.</summary>
    public static string? RunCommand(string fileName, string arguments, TimeSpan timeout)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEndAsync();
            _ = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)Math.Max(1, timeout.TotalMilliseconds)))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }
            return output.Wait(TimeSpan.FromMilliseconds(200)) ? output.Result : null;
        }
        catch
        {
            return null;
        }
    }
}
