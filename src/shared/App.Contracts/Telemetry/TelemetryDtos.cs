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
/// Sub-second station oscillation telemetry broadcast for plant map HUD and 3D digital twin.
/// </summary>
public class StationOscillationBroadcastDto
{
    public string StationId { get; set; } = string.Empty;
    public double VibrationMmS { get; set; }
    public double SpindleRpm { get; set; }
    public double CycleTimeMs { get; set; }
    public double PowerKw { get; set; }
    public double TemperatureC { get; set; }
    public double FrequencyHz { get; set; } = 50.0;
    public string OperationalStatus { get; set; } = "RUNNING";
    public long Timestamp { get; set; }
}

/// <summary>
/// Strongly typed SignalR station KPI telemetry broadcast.
/// </summary>
public class StationTelemetryBroadcastDto
{
    public string StationId { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string LineId { get; set; } = string.Empty;
    public double Oee { get; set; }
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
    public double Yield { get; set; }
    public int PartsPerHour { get; set; }
    public double CycleTimeSeconds { get; set; }
    public int TotalPartsProduced { get; set; }
    public int ScrapParts { get; set; }
    public string OperationalStatus { get; set; } = "RUNNING";
    public int ActiveTicketCount { get; set; }
    public int CriticalTicketCount { get; set; }
    public bool HasSlaBreach { get; set; }
}
