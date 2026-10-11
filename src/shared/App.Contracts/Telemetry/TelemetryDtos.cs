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
