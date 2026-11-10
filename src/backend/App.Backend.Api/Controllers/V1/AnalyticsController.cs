using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller providing predictive maintenance analytics, trend snapshots, OEE KPIs,
/// and Power BI configuration.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IPredictiveMaintenanceService _predictiveService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IPredictiveMaintenanceService predictiveService,
        IConfiguration configuration,
        ILogger<AnalyticsController> logger)
    {
        _predictiveService = predictiveService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets at-a-glance fleet-wide uptime summary, top-faulting machines, maintenance backlog,
    /// and critical stock depletion warnings.
    /// </summary>
    [HttpGet("fleet-summary")]
    public async Task<ActionResult<FleetSummaryDto>> GetFleetSummary(CancellationToken cancellationToken)
    {
        var summary = await _predictiveService.GetFleetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Gets rolling telemetry trends, statistical regression corridors, and recipe-based failure forecast.
    /// </summary>
    [HttpGet("trends")]
    public async Task<ActionResult<MachineTrendSeriesDto>> GetMachineTrends(
        [FromQuery] string machineId = "",
        [FromQuery] string metric = "cycle_time",
        [FromQuery] string range = "8h",
        [FromQuery] string? recipeId = null)
    {
        TimeSpan timeRange = range.ToLower() switch
        {
            "1h" => TimeSpan.FromHours(1),
            "24h" or "1d" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromHours(8)
        };

        var trends = await _predictiveService.GetMachineTrendsAsync(machineId, metric, timeRange, recipeId, HttpContext?.RequestAborted ?? default);
        return Ok(trends);
    }

    /// <summary>
    /// Gets production line-level OEE, Availability, Performance, and Quality KPIs.
    /// </summary>
    [HttpGet("kpis")]
    public async Task<ActionResult<List<LineKpiDto>>> GetLineKpis()
    {
        var kpis = await _predictiveService.GetLineKpisAsync(HttpContext?.RequestAborted ?? default);
        return Ok(kpis);
    }

    /// <summary>
    /// Gets station-level OEE, Yield, Parts/Hour, and SLA ticket overlays for floor plan CAD visualization.
    /// </summary>
    [HttpGet("station-kpis")]
    public async Task<ActionResult<List<StationKpiDto>>> GetStationKpis(
        [FromQuery] string? lineId = null,
        [FromQuery] string? stationId = null)
    {
        var stationKpis = await _predictiveService.GetStationTelemetryKpisAsync(lineId, stationId, HttpContext?.RequestAborted ?? default);
        return Ok(stationKpis);
    }
}
