namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Backend.Api.Dtos;

/// <summary>
/// Interface for predictive maintenance analytics, anomaly detection, and fleet metrics.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface IPredictiveMaintenanceService
{
    List<AnomalyDetailDto> DetectAnomalies(string metricName, List<TelemetryPointDto> points);
    double CalculateHealthIndex(string machineId, int recentAnomalyCount, int openTicketCount);
    /// <summary>Fleet maintenance figures for the look-back window (default 30 days, clamped to 1..365 days).</summary>
    Task<FleetSummaryDto> GetFleetSummaryAsync(TimeSpan? window = null, CancellationToken cancellationToken = default);
    /// <summary>
    /// One row per tenant-visible machine with hierarchy, organization, type, latest telemetry,
    /// ticket figures for the window and controller connectivity. <paramref name="freshness"/>
    /// decides when a station is stale (per-station thresholds by default).
    /// </summary>
    Task<FleetStationsDto> GetFleetStationsAsync(TimeSpan? window = null, bool includeDecommissioned = false, StationFreshnessOptions? freshness = null, CancellationToken cancellationToken = default);
    Task<MachineTrendSeriesDto> GetMachineTrendsAsync(string machineId, string metricName, TimeSpan timeRange, string? recipeId = null, CancellationToken cancellationToken = default);
    Task<List<LineKpiDto>> GetLineKpisAsync(CancellationToken cancellationToken = default);
    Task<List<StationKpiDto>> GetStationTelemetryKpisAsync(string? lineId = null, string? stationId = null, CancellationToken cancellationToken = default);
}
