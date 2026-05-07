using System;
using System.Collections.Generic;

namespace App.Backend.Api.Dtos;

/// <summary>
/// Root Data Transfer Object for the main dashboard view, aggregating stats, clients, and events.
/// </summary>
public class DashboardDto
{
    /// <summary>High-level system statistics.</summary>
    public DashboardStatsDto Stats { get; set; } = new();
    /// <summary>List of the most recently active edge nodes.</summary>
    public List<RecentClientDto> RecentClients { get; set; } = new();
    /// <summary>Recent security or system events reported by agents.</summary>
    public List<AgentEventDto> SecurityEvents { get; set; } = new();
}

/// <summary>
/// Data Transfer Object for the high-level dashboard metrics (Stats Grid).
/// </summary>
public class DashboardStatsDto
{
    /// <summary>Total number of users registered in the system.</summary>
    public string TotalUsers { get; set; } = "0";
    /// <summary>Number of nodes that have sent a heartbeat in the last 5 minutes.</summary>
    public string ActiveClients { get; set; } = "0";
    /// <summary>Number of unresolved Warning or Error events from the last 24 hours.</summary>
    public string PendingAlerts { get; set; } = "0";
    /// <summary>Calculated average availability across all managed nodes.</summary>
    public string AvgUptime { get; set; } = "0%";
}

/// <summary>
/// Summary representation of a recently active Client PC for the dashboard preview.
/// </summary>
public class RecentClientDto
{
    /// <summary>Unique identifier for the PC.</summary>
    public Guid Id { get; set; }
    /// <summary>Network hostname of the device.</summary>
    public string Hostname { get; set; } = string.Empty;
    /// <summary>Operating System name and version.</summary>
    public string Os { get; set; } = "Unknown";
    /// <summary>Relative time since the last heartbeat (e.g., "2 mins ago").</summary>
    public string LastSeen { get; set; } = string.Empty;
}

/// <summary>
/// Simplified event record for the dashboard activity feed.
/// </summary>
public class AgentEventDto
{
    /// <summary>The source or category of the event.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>A descriptive message detailing what occurred.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Relative timestamp (e.g., "1 hour ago").</summary>
    public string Time { get; set; } = string.Empty;
    /// <summary>Severity level for UI highlighting (low, medium, high).</summary>
    public string Severity { get; set; } = "low";
}
