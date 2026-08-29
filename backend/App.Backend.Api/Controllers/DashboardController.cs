using App.Infrastructure.Repositories;
using App.Backend.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for retrieving aggregated system telemetry and activity for the main dashboard.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IControllerRepository _controllerRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IControllerRepository controllerRepository,
        IAssetRepository assetRepository,
        IMaintenanceTicketRepository ticketRepository,
        ILogger<DashboardController> logger)
    {
        _controllerRepository = controllerRepository;
        _assetRepository = assetRepository;
        _ticketRepository = ticketRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a unified data package for the dashboard, including stats, recent clients, and security events.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<DashboardDto>> GetDashboardData()
    {
        int totalUsers = await _assetRepository.GetAuthUsersCountAsync();
        int totalClients = await _controllerRepository.GetCountAsync();
        int activeClients = await _controllerRepository.GetActiveCountAsync(TimeSpan.FromMinutes(5));
        int pendingAlerts = await _ticketRepository.GetPendingAlertsCountAsync(TimeSpan.FromDays(1));

        double avgUptime = totalClients > 0 ? (double)activeClients / totalClients * 100 : 0;

        var recentClientsRaw = await _controllerRepository.GetRecentClientsAsync(5);
        var recentClients = recentClientsRaw.Select(c => new RecentClientDto
        {
            Id = c.Id,
            Hostname = c.Hostname ?? c.Name,
            Os = "Unknown",
            LastSeen = c.LastOnline.HasValue ? GetRelativeTime(c.LastOnline.Value) : "Never"
        }).ToList();

        var securityEventsRaw = await _ticketRepository.GetRecentAgentEventsAsync(10);
        var securityEvents = securityEventsRaw.Select(e => new AgentEventDto
        {
            Title = e.Source,
            Description = e.Message,
            Time = GetRelativeTime(new DateTimeOffset(e.Timestamp, TimeSpan.Zero)),
            Severity = e.Level.ToLower() switch
            {
                "critical" => "high",
                "error" => "high",
                "warning" => "medium",
                _ => "low"
            }
        }).ToList();

        return new DashboardDto
        {
            Stats = new DashboardStatsDto
            {
                TotalUsers = totalUsers.ToString("N0"),
                ActiveClients = activeClients.ToString("N0"),
                PendingAlerts = pendingAlerts.ToString("N0"),
                AvgUptime = $"{avgUptime:F1}%"
            },
            RecentClients = recentClients,
            SecurityEvents = securityEvents
        };
    }

    private static string GetRelativeTime(DateTimeOffset dateTime)
    {
        var span = DateTimeOffset.UtcNow - dateTime;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} mins ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        return $"{(int)span.TotalDays} days ago";
    }
}
