namespace App.Backend.Api.Services;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Interface for mass-data reporting and analytics exports.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface IReportExportService
{
    Task<byte[]> ExportInventoryWorkbookAsync(CancellationToken cancellationToken = default);
    Task<byte[]> ExportControllersTelemetryWorkbookAsync(CancellationToken cancellationToken = default);
    Task<List<object>> GetGrafanaMetricsAsync(CancellationToken cancellationToken = default);
    Task<object> GetODataTelemetryAsync(CancellationToken cancellationToken = default);
    Task<object> GetODataMachinesAsync(CancellationToken cancellationToken = default);
}
