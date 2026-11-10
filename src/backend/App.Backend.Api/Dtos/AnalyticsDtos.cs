namespace App.Backend.Api.Dtos;

public class FleetSummaryDto
{
    /// <summary>Number of (tenant-visible) machines.</summary>
    public int TotalMachines { get; set; }
    /// <summary>No machine runtime counters are ingested yet; always null.</summary>
    public double? TotalMachineHours { get; set; }
    /// <summary>Share of controllers that reported within the last 5 minutes; null without controllers.</summary>
    public double? AvailabilityPercentage { get; set; }
    /// <summary>Observed mean time between incidents (hours); null with fewer than two incidents per machine.</summary>
    public double? AverageMtbfHours { get; set; }
    /// <summary>Mean ticket resolution time (minutes) in the incident window; null without resolved tickets.</summary>
    public double? AverageMttrMinutes { get; set; }
    /// <summary>Tickets carry no SLA due date on the backend; always null until they do.</summary>
    public double? SlaCompliancePercentage { get; set; }
    public List<TopFaultingMachineDto> TopFaultingMachines { get; set; } = new();
    public BacklogAgeDto MaintenanceBacklog { get; set; } = new();
    public List<StockDepletionItemDto> StockDepletion { get; set; } = new();
}

public class TopFaultingMachineDto
{
    public string MachineId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    /// <summary>Sum of reported line-stop durations; null when no ticket reported one.</summary>
    public double? TotalDowntimeMinutes { get; set; }
    public string PrimaryAlarmCode { get; set; } = string.Empty;
    public double HealthIndex { get; set; }
}

public class BacklogAgeDto
{
    public int Under24Hours { get; set; }
    public int OneToThreeDays { get; set; }
    public int OverThreeDays { get; set; }
    public int CriticalBreached { get; set; }
}

public class StockDepletionItemDto
{
    public string PartNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinStockThreshold { get; set; }
    public string Criticality { get; set; } = "High";
}

public class MachineTrendSeriesDto
{
    public string MachineId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public double NominalValue { get; set; }
    public double UpperTolerance { get; set; }
    public double LowerTolerance { get; set; }
    public List<TelemetryPointDto> Points { get; set; } = new();
    public List<AnomalyDetailDto> DetectedAnomalies { get; set; } = new();
    public RollingTrendsDto StatisticalTrends { get; set; } = new();
    public RecipeForecastDto? RecipeForecast { get; set; }
    public List<RecipeItemDto> AvailableRecipes { get; set; } = new();
}

public class RollingTrendsDto
{
    public double LinearRegressionSlope { get; set; }
    public double LinearRegressionIntercept { get; set; }
    public double R2Score { get; set; }
    public int MovingAverageWindow { get; set; } = 5;
    public List<double> MovingAverages { get; set; } = new();
    public List<double> UpperStdDevCorridor { get; set; } = new();
    public List<double> LowerStdDevCorridor { get; set; } = new();
    public string TrendDirection { get; set; } = "Stable"; // "Stable" | "DriftingUp" | "DriftingDown" | "Volatile"
}

public class RecipeForecastDto
{
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public double TargetBaseline { get; set; }
    public double UpperTolerance { get; set; }
    public double LowerTolerance { get; set; }
    public double DriftPercentage { get; set; }
    public int ForecastCyclesUntilFailure { get; set; }
    public string ToolWearStatus { get; set; } = "Nominal"; // "Nominal" | "Warning" | "CriticalImpendingFailure"
    public string RecommendedAction { get; set; } = string.Empty;
}

public class RecipeItemDto
{
    public string RecipeId { get; set; } = string.Empty;
    public string RecipeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class TelemetryPointDto
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public bool IsAnomaly { get; set; }
    public double ZScore { get; set; }
}

public class AnomalyDetailDto
{
    public DateTime Timestamp { get; set; }
    public string Metric { get; set; } = string.Empty;
    public double Value { get; set; }
    public double ExpectedValue { get; set; }
    public double ZScore { get; set; }
    public string Severity { get; set; } = "Warning";
    public string Description { get; set; } = string.Empty;
}

public class LineKpiDto
{
    /// <summary>Line machine-group id.</summary>
    public string LineId { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    /// <summary>Mean over the line's stations reporting telemetry; null when none do.</summary>
    public double? Oee { get; set; }
    public double? Availability { get; set; }
    public double? Performance { get; set; }
    public double? Quality { get; set; }
    public double? MtbfHours { get; set; }
    public double? MttrMinutes { get; set; }
    public int IncidentCount { get; set; }
}

/// <summary>
/// Station overlay for the plant map (GET /api/v1/analytics/station-kpis). Production values are
/// null until the station's PLC reports them; ticket fields come from stored maintenance tickets.
/// Mirrored by <c>StationTelemetryKpi</c> in <c>app/composables/useStationTelemetry.ts</c>.
/// </summary>
public class StationKpiDto
{
    /// <summary>Machine id.</summary>
    public string StationId { get; set; } = string.Empty;
    /// <summary>Floor-plan handle, else custom identifier, else name.</summary>
    public string Handle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? LineId { get; set; }
    public string? LineName { get; set; }
    public double? Oee { get; set; }
    public double? Availability { get; set; }
    public double? Performance { get; set; }
    public double? Quality { get; set; }
    public double? Yield { get; set; }
    public double? PartsPerHour { get; set; }
    public double? TargetPartsPerHour { get; set; }
    public double? CycleTimeSeconds { get; set; }
    public double? TargetCycleTimeSeconds { get; set; }
    public long? TotalPartsProduced { get; set; }
    public long? ScrapParts { get; set; }
    /// <summary>RUNNING | IDLE | WARNING | FAULTED | MAINTENANCE | UNKNOWN (no telemetry).</summary>
    public string OperationalStatus { get; set; } = "UNKNOWN";
    public int ActiveTicketCount { get; set; }
    public int CriticalTicketCount { get; set; }
    public bool HasSlaBreach { get; set; }
    public DateTimeOffset? LastTelemetryAt { get; set; }
}
