using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Backend.Api.Dtos;
using System.Linq;

namespace App.Backend.Api.Controllers;

/// <summary>
/// Controller for retrieving aggregated system telemetry and activity for the main dashboard.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardController"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DashboardController(AppDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a unified data package for the dashboard, including stats, recent clients, and security events.
    /// </summary>
    /// <returns>A <see cref="DashboardDto"/> containing the current system state.</returns>
    /// <response code="200">Returns the aggregated dashboard data.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<DashboardDto>> GetDashboardData()
    {
        var now = DateTimeOffset.UtcNow;
        var activeThreshold = now.AddMinutes(-5);

        // Stats
        int totalUsers = 0;
        try {
            totalUsers = await _context.AuthUsers.CountAsync();
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to count auth users. Casing or permissions issue?");
        }

        var totalClients = await _context.ClientPcs.CountAsync();
        var activeClients = await _context.ClientPcs.CountAsync(c => c.LastOnline >= activeThreshold);
        
        int pendingAlerts = 0;
        try {
            pendingAlerts = await _context.AgentEvents.CountAsync(e => (e.Level == "Warning" || e.Level == "Error" || e.Level == "Critical") && e.Timestamp >= now.AddDays(-1).DateTime);
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to count agent events.");
        }
        
        var avgUptime = totalClients > 0 ? (double)activeClients / totalClients * 100 : 0;

        // Recent Clients
        var recentClientsRaw = await _context.ClientPcs
            .OrderByDescending(c => c.LastOnline)
            .Take(5)
            .Select(c => new 
            {
                c.Id,
                Hostname = c.Hostname ?? c.Name,
                LastOnline = c.LastOnline
            })
            .ToListAsync();

        var recentClients = recentClientsRaw.Select(c => new RecentClientDto
        {
            Id = c.Id,
            Hostname = c.Hostname,
            Os = "Unknown",
            LastSeen = c.LastOnline.HasValue ? GetRelativeTime(c.LastOnline.Value) : "Never"
        }).ToList();

        // Security Events / Activity
        var securityEventsRaw = await _context.AgentEvents
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .ToListAsync();

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

    /// <summary>
    /// Helper method to convert a timestamp into a human-readable relative time string.
    /// </summary>
    /// <param name="dateTime">The timestamp to convert.</param>
    /// <returns>A string like "2 mins ago" or "Just now".</returns>
    private static string GetRelativeTime(DateTimeOffset dateTime)
    {
        var span = DateTimeOffset.UtcNow - dateTime;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} mins ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hours ago";
        return $"{(int)span.TotalDays} days ago";
    }
}
