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
    Task<FleetSummaryDto> GetFleetSummaryAsync(CancellationToken cancellationToken = default);
    MachineTrendSeriesDto GetMachineTrends(string machineId, string metricName, TimeSpan timeRange);
    List<LineKpiDto> GetLineKpis();
}
