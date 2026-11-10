namespace App.Contracts.Telemetry;

using System;

/// <summary>
/// Strongly typed SignalR telemetry broadcast payload.
/// Replaces anonymous object payloads to guarantee cross-service type safety.
/// </summary>
public class TelemetryBroadcastDto
{
    public string Hostname { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string? MachineIdentifier { get; set; }
    public DateTimeOffset? LastOnline { get; set; }
    public object? FreeDiskSpace { get; set; }
    public object? ResourceAverages { get; set; }
    public int ComponentsCount { get; set; }
}

/// <summary>
/// Strongly typed SignalR inventory update notification.
/// </summary>
public class InventoryUpdatedNotificationDto
{
    public string Source { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Live machine dynamics for one station, published on SignalR <c>StationOscillationReceived</c>
/// to the station's organization group whenever a PLC telemetry block for that station is ingested.
/// Every measurement is nullable: a field the PLC did not report is null, never a default.
/// </summary>
public class StationOscillationBroadcastDto
{
    /// <summary>Machine (station) id.</summary>
    public string StationId { get; set; } = string.Empty;
    /// <summary>Floor-plan handle (DXF handle, else custom identifier) the plant map keys stations by.</summary>
    public string Handle { get; set; } = string.Empty;
    public double? VibrationMmS { get; set; }
    public double? SpindleRpm { get; set; }
    public double? CycleTimeMs { get; set; }
    public double? PowerKw { get; set; }
    public double? TemperatureC { get; set; }
    public double? FrequencyHz { get; set; }
    public string? OperationalStatus { get; set; }
    /// <summary>Sample time as Unix epoch milliseconds.</summary>
    public long Timestamp { get; set; }
}

/// <summary>
/// Station production KPI update published on SignalR <c>StationTelemetryReceived</c>.
/// Carries only telemetry-derived values; ticket/SLA counts come from the REST endpoint.
/// Null means "not reported"; clients keep their previous value for null fields.
/// </summary>
public class StationTelemetryBroadcastDto
{
    public string StationId { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double? Oee { get; set; }
    public double? Availability { get; set; }
    public double? Performance { get; set; }
    public double? Quality { get; set; }
    public double? Yield { get; set; }
    public double? PartsPerHour { get; set; }
    public double? TargetPartsPerHour { get; set; }
    public double? CycleTimeSeconds { get; set; }
    public double? TargetCycleTimeSeconds { get; set; }
    public long? TotalPartsProduced { get; set; }
    public long? ScrapParts { get; set; }
    public string? OperationalStatus { get; set; }
    public DateTimeOffset? LastTelemetryAt { get; set; }
}
