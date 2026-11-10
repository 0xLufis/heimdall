namespace App.Agent.Daemon.Reporting.Triggers;

using System;
using System.Collections.Generic;
using App.Agent.Daemon.Domain.Recipes;

public enum TriggerType
{
    Heartbeat,
    ThresholdBreach,
    StateChange,
    DeadbandDelta,
    OnDemand
}

public class DataSliceSelection
{
    public bool IncludeHardware { get; set; }
    public bool IncludeSoftware { get; set; }
    public bool IncludeDisk { get; set; }
    public bool IncludePlcTelemetry { get; set; }
    public bool IncludeEvents { get; set; }

    public static DataSliceSelection FullSnapshot => new()
    {
        IncludeHardware = true,
        IncludeSoftware = true,
        IncludeDisk = true,
        IncludePlcTelemetry = true,
        IncludeEvents = true
    };

    public static DataSliceSelection HeartbeatOnly => new()
    {
        IncludeHardware = false,
        IncludeSoftware = false,
        IncludeDisk = true,
        IncludePlcTelemetry = true,
        IncludeEvents = false
    };

    public void Merge(DataSliceSelection other)
    {
        IncludeHardware |= other.IncludeHardware;
        IncludeSoftware |= other.IncludeSoftware;
        IncludeDisk |= other.IncludeDisk;
        IncludePlcTelemetry |= other.IncludePlcTelemetry;
        IncludeEvents |= other.IncludeEvents;
    }
}

public class TriggerContext
{
    public SystemInfoData? CurrentSystemInfo { get; set; }
    public SystemInfoData? PreviousSystemInfo { get; set; }
    public ushort CurrentAdsState { get; set; }
    public ushort PreviousAdsState { get; set; }
    public IReadOnlyDictionary<string, object>? PlcVariables { get; set; }
    public IReadOnlyDictionary<string, object>? OpcNodes { get; set; }
    public DateTimeOffset LastReportedTime { get; set; }
    public bool ForceReport { get; set; }
    public string? ForceReason { get; set; }
}

public class TriggerEvaluation
{
    public bool Triggered { get; set; }
    public string Reason { get; set; } = string.Empty;
    public TriggerType Type { get; set; }
    public EgressPriority Priority { get; set; } = EgressPriority.P3_LowInventory;
    public DataSliceSelection SlicesToReport { get; set; } = new();

    public static TriggerEvaluation NotTriggered(TriggerType type) => new()
    {
        Triggered = false,
        Type = type
    };
}

public interface ITelemetryTrigger
{
    string Name { get; }
    TriggerType Type { get; }
    TriggerEvaluation Evaluate(TriggerContext context);
}
