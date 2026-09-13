using App.Backend.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Backend.Api.Controllers.V1;

/// <summary>
/// Controller for generating streaming mass-data OpenXML (.xlsx) exports.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ReportExportController : ControllerBase
{
    private readonly ReportExportService _exportService;

    public ReportExportController(ReportExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>
    /// Generates and streams an Excel workbook containing the complete plant inventory.
    /// </summary>
    [HttpGet("inventory")]
    public async Task<IActionResult> ExportInventory(CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportInventoryWorkbookAsync(cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "plant_inventory_export.xlsx");
    }

    /// <summary>
    /// Generates and streams an Excel workbook containing edge industrial controllers and telemetry logs.
    /// </summary>
    [HttpGet("telemetry")]
    public async Task<IActionResult> ExportTelemetry(CancellationToken cancellationToken)
    {
        var bytes = await _exportService.ExportControllersTelemetryWorkbookAsync(cancellationToken);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "controllers_telemetry_export.xlsx");
    }

    /// <summary>
    /// Emits Grafana Infinity / SimpleJson plugin compatible timeseries metrics.
    /// </summary>
    [HttpGet("grafana/metrics")]
    public async Task<IActionResult> GetGrafanaMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _exportService.GetGrafanaMetricsAsync(cancellationToken);
        return Ok(metrics);
    }

    /// <summary>
    /// Emits an OData v4 JSON feed of live telemetry snapshots for Excel PowerQuery connection.
    /// </summary>
    [HttpGet("odata/telemetry")]
    public async Task<IActionResult> GetODataTelemetry(CancellationToken cancellationToken)
    {
        var feed = await _exportService.GetODataTelemetryAsync(cancellationToken);
        return Ok(feed);
    }

    /// <summary>
    /// Emits an OData v4 JSON feed of plant machinery for Excel PowerQuery connection.
    /// </summary>
    [HttpGet("odata/machines")]
    public async Task<IActionResult> GetODataMachines(CancellationToken cancellationToken)
    {
        var feed = await _exportService.GetODataMachinesAsync(cancellationToken);
        return Ok(feed);
    }
}
