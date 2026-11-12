namespace App.Agent.Daemon.Reporting;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using App.Agent.Daemon.Infrastructure.Identity;
using App.Shared.Protos;
using Google.Protobuf;

/// <summary>Outcome of checking one report.</summary>
public sealed record SanityResult(bool Accepted, IReadOnlyList<string> Issues);

/// <summary>
/// Client-side sanity checks applied to every live report before it is published (and before it is
/// spooled), so obviously wrong values are corrected or dropped here instead of being stored by the
/// server. Every rejection or correction is counted per rule and visible in <c>/api/status</c>
/// (<c>diagnostics.sanity</c>) - nothing is dropped invisibly.
/// <list type="bullet">
/// <item>Rejects the whole report: empty hostname, empty or placeholder machine identifier, missing or
/// placeholder MAC address (the server keys devices by MAC, so such a report would merge devices).</item>
/// <item>Corrects: timestamps at epoch zero or more than 5 minutes in the future (set to now), timestamps
/// going backwards between live reports (clock stepped back; kept monotonic), percentages outside
/// 0..100 (clamped), negative or non-finite free space (set to 0).</item>
/// <item>Drops: non-finite CPU/RAM samples (live telemetry slice), drive entries with non-finite, negative or
/// absurd (&gt; 1 PB) values or empty names, physical drives with sizes &lt;= 0 or &gt; 1 PB.</item>
/// <item>Bounds the serialized report to <c>MaxReportPayloadBytes</c>: the largest inventory components are
/// left out until it fits; a report that cannot fit is rejected.</item>
/// </list>
/// The server applies the same timestamp rule to lifecycle events (AgentLifecycleIngestor) and
/// quarantines payloads it cannot parse (malformed_telemetry_quarantine).
/// </summary>
public sealed class TelemetrySanityFilter
{
    public const double MaxPlausibleGb = 1_000_000;          // 1 PB
    public const long MaxPlausibleBytes = 1_000_000_000_000_000; // 1 PB
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);
    public static readonly DateTimeOffset MinPlausibleTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.Ordinal);
    private readonly object _lock = new();
    private DateTimeOffset _lastAccepted = DateTimeOffset.MinValue;
    private IReadOnlyList<string> _lastIssues = Array.Empty<string>();

    /// <summary>Per-rule counts of rejections and corrections since start.</summary>
    public IReadOnlyDictionary<string, long> Counters => _counters;

    public long ReportsChecked => _counters.TryGetValue("reports_checked", out var n) ? n : 0;
    public long ReportsRejected => _counters.TryGetValue("reports_rejected", out var n) ? n : 0;

    public IReadOnlyList<string> LastIssues
    {
        get { lock (_lock) return _lastIssues; }
    }

    /// <summary>Checks and repairs a report in place. Returns whether it may be published.</summary>
    public SanityResult Check(SystemInfoData data, DateTimeOffset nowUtc, bool liveReport = true)
    {
        var issues = new List<string>();
        Count("reports_checked");
        bool reject = false;

        if (string.IsNullOrWhiteSpace(data.Hostname)) { Issue(issues, "empty_hostname"); reject = true; }
        if (!MachineIdentityResolver.IsPlausibleMachineId(data.MachineIdentifier)) { Issue(issues, "invalid_machine_identifier"); reject = true; }
        if (!MachineIdentityResolver.IsPlausibleMac(data.MacAddress)) { Issue(issues, "invalid_mac_address"); reject = true; }

        data.LastOnline = SaneTime(data.LastOnline, nowUtc, issues, "last_online");
        if (liveReport)
        {
            lock (_lock)
            {
                if (data.LastOnline < _lastAccepted)
                {
                    Issue(issues, "out_of_order_timestamp");
                    data.LastOnline = _lastAccepted;
                }
            }
        }

        if (data.LiveTelemetry != null)
        {
            var live = data.LiveTelemetry;
            if (!double.IsFinite(live.CpuUsagePercent) || !double.IsFinite(live.RamUsagePercent))
            {
                Issue(issues, "non_finite_metric");
                data.LiveTelemetry = null!; // the live slice is left out of this report
            }
            else
            {
                if (live.CpuUsagePercent is < 0 or > 100)
                {
                    Issue(issues, "percentage_out_of_range");
                    live.CpuUsagePercent = Math.Clamp(live.CpuUsagePercent, 0, 100);
                    live.CpuLoad = live.CpuUsagePercent.ToString("F1", CultureInfo.InvariantCulture) + "%";
                }
                if (live.RamUsagePercent is < 0 or > 100)
                {
                    Issue(issues, "percentage_out_of_range");
                    live.RamUsagePercent = Math.Clamp(live.RamUsagePercent, 0, 100);
                    live.RamUsage = live.RamUsagePercent.ToString("F1", CultureInfo.InvariantCulture) + "%";
                }
                live.Timestamp = SaneTime(live.Timestamp, nowUtc, issues, "live_telemetry");
            }
        }

        if (data.Disk != null)
        {
            var disk = data.Disk;
            disk.TotalFreeGB = SaneGb(disk.TotalFreeGB, issues);
            disk.OsDriveFreeGB = SaneGb(disk.OsDriveFreeGB, issues);

            if (disk.Drives != null)
            {
                foreach (var key in disk.Drives.Keys.ToList())
                {
                    double value = disk.Drives[key];
                    if (string.IsNullOrWhiteSpace(key) || !double.IsFinite(value) || value < 0 || value > MaxPlausibleGb)
                    {
                        Issue(issues, "invalid_drive_entry");
                        disk.Drives.Remove(key);
                    }
                }
            }

            if (disk.PhysicalDrives != null)
            {
                int removed = disk.PhysicalDrives.RemoveAll(d => d == null || d.SizeBytes <= 0 || d.SizeBytes > MaxPlausibleBytes);
                for (int i = 0; i < removed; i++) Issue(issues, "invalid_physical_drive");
            }
        }

        if (reject)
        {
            Count("reports_rejected");
        }
        else if (liveReport)
        {
            lock (_lock) _lastAccepted = data.LastOnline;
        }

        lock (_lock) _lastIssues = issues.Count > 0 ? issues : _lastIssues;
        return new SanityResult(!reject, issues);
    }

    /// <summary>
    /// Keeps a serialized report within <paramref name="maxBytes"/> by leaving out the largest
    /// components first. Returns false when even the bare report does not fit.
    /// </summary>
    public bool EnforceSize(SystemInfoRequest request, int maxBytes, out IReadOnlyList<string> droppedComponents)
    {
        var dropped = new List<string>();
        while (request.CalculateSize() > maxBytes && request.Components.Count > 0)
        {
            var largest = request.Components.OrderByDescending(c => c.CalculateSize()).First();
            request.Components.Remove(largest);
            dropped.Add(largest.Name);
            Count("oversized_component_dropped");
        }

        droppedComponents = dropped;
        if (request.CalculateSize() <= maxBytes) return true;

        Count("oversized_report");
        Count("reports_rejected");
        return false;
    }

    private DateTimeOffset SaneTime(DateTimeOffset value, DateTimeOffset nowUtc, List<string> issues, string field)
    {
        if (value < MinPlausibleTime) { Issue(issues, "timestamp_at_epoch"); return nowUtc; }
        if (value > nowUtc + MaxClockSkew) { Issue(issues, "timestamp_in_future"); return nowUtc; }
        return value;
    }

    private double SaneGb(double value, List<string> issues)
    {
        if (!double.IsFinite(value) || value < 0) { Issue(issues, "invalid_free_space"); return 0; }
        if (value > MaxPlausibleGb) { Issue(issues, "invalid_free_space"); return 0; }
        return value;
    }

    private void Issue(List<string> issues, string rule)
    {
        issues.Add(rule);
        Count(rule);
    }

    private void Count(string rule) => _counters.AddOrUpdate(rule, 1, (_, n) => n + 1);
}
