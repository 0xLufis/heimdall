namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using App.Shared.Protos.Telemetry;

/// <summary>
/// Latest known production / dynamics telemetry for one station (machine), merged from every
/// ingested PLC block that carried station telemetry. Stored in the distributed cache under
/// <see cref="StationTelemetryStore.Key"/>; every measurement is null until a PLC reports it.
/// </summary>
public class StationTelemetrySnapshot
{
    public Guid MachineId { get; set; }
    public string? OrganizationId { get; set; }

    // Dynamics
    public double? VibrationMmS { get; set; }
    public double? SpindleRpm { get; set; }
    public double? CycleTimeMs { get; set; }
    public double? PowerKw { get; set; }
    public double? TemperatureC { get; set; }
    public double? FrequencyHz { get; set; }

    // Production
    public double? Availability { get; set; }
    public double? Performance { get; set; }
    public double? Quality { get; set; }
    public double? Oee { get; set; }
    public double? PartsPerHour { get; set; }
    public double? TargetPartsPerHour { get; set; }
    public double? CycleTimeSeconds { get; set; }
    public double? TargetCycleTimeSeconds { get; set; }
    public long? TotalPartsProduced { get; set; }
    public long? ScrapParts { get; set; }

    /// <summary>RUNNING | IDLE | WARNING | FAULTED | MAINTENANCE as reported by the PLC.</summary>
    public string? OperationalStatus { get; set; }

    public DateTimeOffset? LastTelemetryAt { get; set; }

    /// <summary>First-pass yield from the reported counters, null without them.</summary>
    public double? Yield =>
        TotalPartsProduced is > 0 && ScrapParts is >= 0
            ? Math.Round((TotalPartsProduced.Value - Math.Min(ScrapParts.Value, TotalPartsProduced.Value)) * 100.0 / TotalPartsProduced.Value, 2)
            : null;

    /// <summary>Quality as reported, else first-pass yield.</summary>
    public double? EffectiveQuality => Quality ?? Yield;

    /// <summary>OEE as reported, else A x P x Q when all three are known.</summary>
    public double? EffectiveOee =>
        Oee ?? (Availability.HasValue && Performance.HasValue && EffectiveQuality.HasValue
            ? Math.Round(Availability.Value * Performance.Value * EffectiveQuality.Value / 10000.0, 2)
            : null);

    public double? EffectiveCycleTimeSeconds => CycleTimeSeconds ?? (CycleTimeMs.HasValue ? Math.Round(CycleTimeMs.Value / 1000.0, 3) : null);

    public bool HasDynamics =>
        VibrationMmS.HasValue || SpindleRpm.HasValue || CycleTimeMs.HasValue ||
        PowerKw.HasValue || TemperatureC.HasValue || FrequencyHz.HasValue;

    public bool HasProduction =>
        Availability.HasValue || Performance.HasValue || Quality.HasValue || Oee.HasValue ||
        PartsPerHour.HasValue || TargetPartsPerHour.HasValue || CycleTimeSeconds.HasValue ||
        TargetCycleTimeSeconds.HasValue || TotalPartsProduced.HasValue || ScrapParts.HasValue ||
        OperationalStatus != null;

    /// <summary>Copies every non-null field of <paramref name="sample"/> over this snapshot.</summary>
    public void MergeFrom(StationTelemetrySnapshot sample)
    {
        VibrationMmS = sample.VibrationMmS ?? VibrationMmS;
        SpindleRpm = sample.SpindleRpm ?? SpindleRpm;
        CycleTimeMs = sample.CycleTimeMs ?? CycleTimeMs;
        PowerKw = sample.PowerKw ?? PowerKw;
        TemperatureC = sample.TemperatureC ?? TemperatureC;
        FrequencyHz = sample.FrequencyHz ?? FrequencyHz;
        Availability = sample.Availability ?? Availability;
        Performance = sample.Performance ?? Performance;
        Quality = sample.Quality ?? Quality;
        Oee = sample.Oee ?? Oee;
        PartsPerHour = sample.PartsPerHour ?? PartsPerHour;
        TargetPartsPerHour = sample.TargetPartsPerHour ?? TargetPartsPerHour;
        CycleTimeSeconds = sample.CycleTimeSeconds ?? CycleTimeSeconds;
        TargetCycleTimeSeconds = sample.TargetCycleTimeSeconds ?? TargetCycleTimeSeconds;
        TotalPartsProduced = sample.TotalPartsProduced ?? TotalPartsProduced;
        ScrapParts = sample.ScrapParts ?? ScrapParts;
        OperationalStatus = sample.OperationalStatus ?? OperationalStatus;
        LastTelemetryAt = sample.LastTelemetryAt ?? LastTelemetryAt;
    }
}

/// <summary>Cache addressing for <see cref="StationTelemetrySnapshot"/>.</summary>
public static class StationTelemetryStore
{
    /// <summary>Snapshots expire when a station stops reporting for this long.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);

    /// <summary>Keyed by machine GUID (globally unique), so tenants cannot collide.</summary>
    public static string Key(Guid machineId) => $"telemetry:station:{machineId:D}";
}

/// <summary>
/// Maps PLC telemetry (ADS symbol blocks carrying a structured value) onto station telemetry.
///
/// Field names are matched case-insensitively ignoring separators and a one-letter IEC type
/// prefix (<c>fVibrationRms</c>, <c>nPartsProduced</c>), at any depth of a struct, or against the
/// last segment of the symbol name for scalar blocks. Anything unrecognised is ignored; nothing is
/// derived from absent fields.
/// </summary>
public static class StationTelemetryExtractor
{
    private enum Field
    {
        VibrationMmS, SpindleRpm, CycleTimeMs, PowerKw, TemperatureC, FrequencyHz,
        Availability, Performance, Quality, Oee, PartsPerHour, TargetPartsPerHour,
        CycleTimeSeconds, TargetCycleTimeSeconds, TotalPartsProduced, ScrapParts, OperationalStatus
    }

    private static readonly Dictionary<string, Field> Aliases = BuildAliases();

    /// <summary>Lua / trend metric keys for each numeric field (stable, snake_case).</summary>
    public static readonly IReadOnlyDictionary<string, string> MetricKeys = new Dictionary<string, string>
    {
        [nameof(Field.VibrationMmS)] = "vibration_mms",
        [nameof(Field.SpindleRpm)] = "spindle_rpm",
        [nameof(Field.CycleTimeMs)] = "cycle_time_ms",
        [nameof(Field.PowerKw)] = "power_kw",
        [nameof(Field.TemperatureC)] = "temperature_c",
        [nameof(Field.FrequencyHz)] = "frequency_hz",
        [nameof(Field.Availability)] = "availability_pct",
        [nameof(Field.Performance)] = "performance_pct",
        [nameof(Field.Quality)] = "quality_pct",
        [nameof(Field.Oee)] = "oee_pct",
        [nameof(Field.PartsPerHour)] = "parts_per_hour",
        [nameof(Field.TargetPartsPerHour)] = "target_parts_per_hour",
        [nameof(Field.CycleTimeSeconds)] = "cycle_time_s",
        [nameof(Field.TargetCycleTimeSeconds)] = "target_cycle_time_s",
        [nameof(Field.TotalPartsProduced)] = "parts_produced",
        [nameof(Field.ScrapParts)] = "scrap_parts"
    };

    private static Dictionary<string, Field> BuildAliases()
    {
        var map = new Dictionary<string, Field>(StringComparer.Ordinal);
        void Add(Field f, params string[] names) { foreach (var n in names) map[n] = f; }
        Add(Field.VibrationMmS, "vibrationmms", "vibrationrms", "vibrationrmsmms", "vibration", "vibrms", "vibrationvelocity");
        Add(Field.SpindleRpm, "spindlerpm", "spindlespeed", "spindlespeedrpm", "rpm");
        Add(Field.CycleTimeMs, "cycletimems", "actualcycletimems", "actualcycletime");
        Add(Field.PowerKw, "powerkw", "activepowerkw", "power");
        Add(Field.TemperatureC, "temperaturec", "temperature", "motortemp", "motortempc", "temp");
        Add(Field.FrequencyHz, "frequencyhz", "frequency", "linefrequency");
        Add(Field.Availability, "availability", "availabilitypct", "availabilitypercent");
        Add(Field.Performance, "performance", "performancepct", "performancepercent");
        Add(Field.Quality, "quality", "qualitypct", "qualitypercent");
        Add(Field.Oee, "oee", "oeepct", "oeepercent", "oeepercentage");
        Add(Field.PartsPerHour, "partsperhour", "throughput", "throughputpartsperhour");
        Add(Field.TargetPartsPerHour, "targetpartsperhour", "plannedpartsperhour");
        Add(Field.CycleTimeSeconds, "cycletimeseconds", "cycletimes", "cycletimesec", "cycletime");
        Add(Field.TargetCycleTimeSeconds, "targetcycletimeseconds", "targetcycletime", "tacttime", "tacttimeseconds");
        Add(Field.TotalPartsProduced, "totalpartsproduced", "partsproduced", "partcount", "totalparts", "partcounter");
        Add(Field.ScrapParts, "scrapparts", "scrapcount", "rejectedparts", "rejects", "nokparts");
        Add(Field.OperationalStatus, "operationalstatus", "machinestate", "stationstate", "state", "status");
        return map;
    }

    /// <summary>
    /// Extracts station telemetry from a PLC block. Returns null when the block carries none.
    /// <paramref name="metrics"/> receives every numeric value found (Lua metric key, value).
    /// </summary>
    public static StationTelemetrySnapshot? Extract(AdsPlcMemoryBlock block, out List<(string Metric, double Value)> metrics)
    {
        metrics = new List<(string, double)>();
        if (block?.StructuredValue == null) return null;

        var sample = new StationTelemetrySnapshot();
        var found = false;
        var value = block.StructuredValue;

        if (value.ValueCase == TelemetryValue.ValueOneofCase.StructValue)
        {
            found = Walk(value.StructValue, sample, metrics, depth: 0);
        }
        else
        {
            var leaf = (block.SymbolName ?? string.Empty).Split('.').LastOrDefault() ?? string.Empty;
            found = Apply(leaf, value, sample, metrics);
        }

        if (!found) return null;
        sample.LastTelemetryAt = block.Timestamp != null ? block.Timestamp.ToDateTimeOffset() : DateTimeOffset.UtcNow;
        return sample;
    }

    private static bool Walk(StructValue node, StationTelemetrySnapshot sample, List<(string, double)> metrics, int depth)
    {
        if (depth > 4) return false;
        var found = false;
        foreach (var (name, child) in node.Fields)
        {
            if (child == null) continue;
            if (child.ValueCase == TelemetryValue.ValueOneofCase.StructValue)
            {
                found |= Walk(child.StructValue, sample, metrics, depth + 1);
            }
            else
            {
                found |= Apply(name, child, sample, metrics);
            }
        }
        return found;
    }

    private static bool Apply(string rawName, TelemetryValue value, StationTelemetrySnapshot sample, List<(string, double)> metrics)
    {
        if (!Aliases.TryGetValue(Normalize(rawName), out var field)) return false;

        if (field == Field.OperationalStatus)
        {
            var status = value.ValueCase switch
            {
                TelemetryValue.ValueOneofCase.StringValue => NormalizeStatus(value.StringValue),
                TelemetryValue.ValueOneofCase.StateValue => NormalizeStatus(value.StateValue.StateLabel),
                _ => null
            };
            if (status == null) return false;
            sample.OperationalStatus = status;
            return true;
        }

        var number = ToDouble(value);
        if (number == null || double.IsNaN(number.Value) || double.IsInfinity(number.Value)) return false;
        var v = number.Value;

        switch (field)
        {
            case Field.VibrationMmS: sample.VibrationMmS = v; break;
            case Field.SpindleRpm: sample.SpindleRpm = v; break;
            case Field.CycleTimeMs: sample.CycleTimeMs = v; break;
            case Field.PowerKw: sample.PowerKw = v; break;
            case Field.TemperatureC: sample.TemperatureC = v; break;
            case Field.FrequencyHz: sample.FrequencyHz = v; break;
            case Field.Availability: sample.Availability = v; break;
            case Field.Performance: sample.Performance = v; break;
            case Field.Quality: sample.Quality = v; break;
            case Field.Oee: sample.Oee = v; break;
            case Field.PartsPerHour: sample.PartsPerHour = v; break;
            case Field.TargetPartsPerHour: sample.TargetPartsPerHour = v; break;
            case Field.CycleTimeSeconds: sample.CycleTimeSeconds = v; break;
            case Field.TargetCycleTimeSeconds: sample.TargetCycleTimeSeconds = v; break;
            case Field.TotalPartsProduced: sample.TotalPartsProduced = (long)Math.Max(0, Math.Round(v)); break;
            case Field.ScrapParts: sample.ScrapParts = (long)Math.Max(0, Math.Round(v)); break;
        }
        metrics.Add((MetricKeys[field.ToString()], v));
        return true;
    }

    private static double? ToDouble(TelemetryValue value) => value.ValueCase switch
    {
        TelemetryValue.ValueOneofCase.DoubleValue => value.DoubleValue,
        TelemetryValue.ValueOneofCase.FloatValue => value.FloatValue,
        TelemetryValue.ValueOneofCase.Int32Value => value.Int32Value,
        TelemetryValue.ValueOneofCase.Int64Value => value.Int64Value,
        TelemetryValue.ValueOneofCase.Uint32Value => value.Uint32Value,
        TelemetryValue.ValueOneofCase.Uint64Value => value.Uint64Value,
        _ => null
    };

    /// <summary>Lower-cases, drops separators and a one-letter IEC 61131 type prefix (fX, nX, bX ...).</summary>
    public static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var start = 0;
        if (name.Length > 2 && "fnbirlsuwdeq".Contains(name[0]) && char.IsUpper(name[1])) start = 1;
        var sb = new StringBuilder(name.Length);
        for (var i = start; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Maps PLC state labels onto the HUD's status vocabulary; unknown labels are dropped.</summary>
    public static string? NormalizeStatus(string? label)
    {
        var s = (label ?? string.Empty).Trim().ToLowerInvariant();
        if (s.Length == 0) return null;
        if (s is "running" or "run" or "production" or "producing" or "auto" or "automatic") return "RUNNING";
        if (s is "idle" or "stopped" or "stop" or "standby" or "ready" or "waiting" or "starved" or "blocked") return "IDLE";
        if (s is "warning" or "warn" or "degraded") return "WARNING";
        if (s is "fault" or "faulted" or "error" or "alarm" or "emergencystop" or "estop") return "FAULTED";
        if (s is "maintenance" or "service" or "setup" or "changeover") return "MAINTENANCE";
        return null;
    }
}
