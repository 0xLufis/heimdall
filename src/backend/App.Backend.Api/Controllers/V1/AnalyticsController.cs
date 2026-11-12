using App.Backend.Api.Dtos;
using App.Backend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Fleet analytics computed from stored data: the grouped station list, maintenance summary,
/// telemetry trend series and line / station KPIs.
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

    /// <summary>Look-back window from the query string; null (service default) when absent or not positive.</summary>
    private static TimeSpan? Window(int? windowDays) =>
        windowDays is > 0 ? TimeSpan.FromDays(Math.Min(windowDays.Value, 3650)) : null;

    /// <summary>
    /// When a station counts as stale: per station after <c>Analytics:Freshness:MissedSamples</c>
    /// missed samples of its learned interval, clamped to <c>Analytics:Freshness:FloorSeconds</c> ..
    /// <c>CeilingSeconds</c>; <c>Analytics:TelemetryStaleSeconds</c> forces one fixed threshold.
    /// </summary>
    private StationFreshnessOptions Freshness() => StationFreshnessOptions.FromConfiguration(_configuration);

    /// <summary>
    /// Fleet maintenance figures: controller connectivity, incidents / MTTR / repeat-incident
    /// interval for the look-back window, top-faulting machines, backlog age and depleted stock.
    /// </summary>
    [HttpGet("fleet-summary")]
    public async Task<ActionResult<FleetSummaryDto>> GetFleetSummary([FromQuery] int? windowDays = null, CancellationToken cancellationToken = default)
    {
        var summary = await _predictiveService.GetFleetSummaryAsync(Window(windowDays), cancellationToken);
        return Ok(summary);
    }

    /// <summary>
    /// Fleet analytics station list: one row per visible machine with the dimensions it can be
    /// grouped by (line, sub-line, project, business unit, machine type, organization), its latest
    /// telemetry, ticket figures for the look-back window and controller connectivity.
    /// </summary>
    [HttpGet("fleet-stations")]
    public async Task<ActionResult<FleetStationsDto>> GetFleetStations(
        [FromQuery] int? windowDays = null,
        [FromQuery] bool includeDecommissioned = false,
        CancellationToken cancellationToken = default)
    {
        var stations = await _predictiveService.GetFleetStationsAsync(
            Window(windowDays), includeDecommissioned, Freshness(), cancellationToken);
        return Ok(stations);
    }

    /// <summary>
    /// Telemetry trend series of one station metric from the buffered history, with regression and
    /// moving-average statistics. <paramref name="range"/>: 5m, 15m, 30m, 1h, 8h (default), 24h, 7d or all.
    /// </summary>
    [HttpGet("trends")]
    public async Task<ActionResult<MachineTrendSeriesDto>> GetMachineTrends(
        [FromQuery] string machineId = "",
        [FromQuery] string metric = "cycle_time",
        [FromQuery] string range = "8h",
        [FromQuery] string? recipeId = null)
    {
        TimeSpan timeRange = (range ?? string.Empty).ToLowerInvariant() switch
        {
            "5m" => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "30m" => TimeSpan.FromMinutes(30),
            "1h" => TimeSpan.FromHours(1),
            "24h" or "1d" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            "all" => TimeSpan.FromDays(3650),
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
