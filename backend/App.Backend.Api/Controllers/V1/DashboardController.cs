using App.Infrastructure.Repositories;
using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

[ApiController]
[Route("api/v1/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IControllerRepository _controllerRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;
    private readonly ICacheService _cache;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IControllerRepository controllerRepository,
        IAssetRepository assetRepository,
        IMaintenanceTicketRepository ticketRepository,
        ICacheService cache,
        ILogger<DashboardController> logger)
    {
        _controllerRepository = controllerRepository;
        _assetRepository = assetRepository;
        _ticketRepository = ticketRepository;
        _cache = cache;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboardData()
    {
        var data = await _cache.GetOrSetAsync("dashboard:metrics", async () =>
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
                    "warn" or "warning" => "medium",
                    _ => "low"
                }
            }).ToList();

            return new DashboardDto
            {
                Stats = new DashboardStatsDto
                {
                    TotalUsers = totalUsers.ToString(),
                    ActiveClients = activeClients.ToString(),
                    PendingAlerts = pendingAlerts.ToString(),
                    AvgUptime = $"{avgUptime:F0}%"
                },
                RecentClients = recentClients,
                SecurityEvents = securityEvents
            };
        }, TimeSpan.FromSeconds(30));

        return Ok(data);
    }

    private static string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime.ToUniversalTime();
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    private static string GetRelativeTime(DateTimeOffset dateTimeOffset)
    {
        var span = DateTimeOffset.UtcNow - dateTimeOffset.ToUniversalTime();
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
