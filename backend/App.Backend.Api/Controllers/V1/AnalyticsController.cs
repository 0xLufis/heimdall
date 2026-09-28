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
    /// Gets rolling telemetry trends and statistical Z-score anomalies for a machine metric.
    /// </summary>
    [HttpGet("trends")]
    public ActionResult<MachineTrendSeriesDto> GetMachineTrends(
        [FromQuery] string machineId = "m-op20",
        [FromQuery] string metric = "cycle_time",
        [FromQuery] string range = "8h")
    {
        TimeSpan timeRange = range.ToLower() switch
        {
            "1h" => TimeSpan.FromHours(1),
            "24h" or "1d" => TimeSpan.FromHours(24),
            "7d" => TimeSpan.FromDays(7),
            _ => TimeSpan.FromHours(8)
        };

        var trends = _predictiveService.GetMachineTrends(machineId, metric, timeRange);
        return Ok(trends);
    }

    /// <summary>
    /// Gets production line-level OEE, Availability, Performance, and Quality KPIs.
    /// </summary>
    [HttpGet("kpis")]
    public ActionResult<List<LineKpiDto>> GetLineKpis()
    {
        var kpis = _predictiveService.GetLineKpis();
        return Ok(kpis);
    }
}
