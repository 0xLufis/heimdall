namespace App.Backend.Api.Dtos;

/// <summary>
/// Fleet maintenance summary (GET /api/v1/analytics/fleet-summary). Every figure is computed from
/// stored tickets, stock items and client PCs for the requested look-back window; a figure
/// without data is null and its sample size is reported next to it.
/// </summary>
public class FleetSummaryDto
{
    public DateTimeOffset GeneratedAt { get; set; }
    /// <summary>Look-back window (days) of the ticket based figures.</summary>
    public int WindowDays { get; set; }
    /// <summary>Number of (tenant-visible) machines.</summary>
    public int TotalMachines { get; set; }

    /// <summary>Client PCs (station controllers) visible to the caller.</summary>
    public int ControllerCount { get; set; }
    /// <summary>Controllers whose agent reported within <see cref="ControllerOnlineWindowSeconds"/>.</summary>
    public int ControllersOnline { get; set; }
    /// <summary>ControllersOnline / ControllerCount in percent; null without controllers. This is agent connectivity, not production availability.</summary>
    public double? ControllerOnlinePercentage { get; set; }
    public int ControllerOnlineWindowSeconds { get; set; }

    /// <summary>Tickets created inside the window (all statuses, with or without a machine).</summary>
    public int IncidentCount { get; set; }
    /// <summary>Tickets created inside the window that are resolved; the sample of <see cref="AverageMttrMinutes"/>.</summary>
    public int ResolvedIncidentCount { get; set; }
    /// <summary>Mean time from ticket creation to resolution (minutes) for tickets created inside the window; null without resolved tickets.</summary>
    public double? AverageMttrMinutes { get; set; }
    /// <summary>
    /// Mean gap (hours) between consecutive tickets on the same machine, averaged over the machines
    /// with at least two tickets in the window. It is a repeat-incident interval, not a runtime based
    /// MTBF (no machine runtime counters are ingested); null without such machines.
    /// </summary>
    public double? AverageMtbfHours { get; set; }
    /// <summary>Machines with at least two tickets in the window; the sample of <see cref="AverageMtbfHours"/>.</summary>
    public int MtbfMachineCount { get; set; }

    /// <summary>Hours after which an open Critical ticket counts as SLA breached (the backend's only SLA rule).</summary>
    public double CriticalSlaHours { get; set; }
    public List<TopFaultingMachineDto> TopFaultingMachines { get; set; } = new();
    public BacklogAgeDto MaintenanceBacklog { get; set; } = new();
    /// <summary>Stock items at or below their minimum threshold (worst first, capped).</summary>
    public List<StockDepletionItemDto> StockDepletion { get; set; } = new();
    /// <summary>Total number of stock items at or below their minimum threshold.</summary>
    public int StockDepletionTotal { get; set; }
}

public class TopFaultingMachineDto
{
    public string MachineId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Custom identifier, else machine name: what the machine directory can be searched by.</summary>
    public string Identifier { get; set; } = string.Empty;
    /// <summary>Line (machine group name, else the legacy line text); empty when unassigned.</summary>
    public string Line { get; set; } = string.Empty;
    /// <summary>Tickets created for the machine inside the window.</summary>
    public int IncidentCount { get; set; }
    /// <summary>Of those, the tickets that are still open.</summary>
    public int OpenTicketCount { get; set; }
    /// <summary>Sum of reported line-stop durations; null when no ticket reported one.</summary>
    public double? TotalDowntimeMinutes { get; set; }
    /// <summary>Most frequent alarm code on the machine's tickets; empty when none carried one.</summary>
    public string PrimaryAlarmCode { get; set; } = string.Empty;
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
    /// <summary>"Critical" when out of stock, else "Warning".</summary>
    public string Criticality { get; set; } = "Warning";
}

public class MachineTrendSeriesDto
{
    public string MachineId { get; set; } = string.Empty;
    /// <summary>History key the request resolved to (e.g. "vibration_mms"); empty when the metric has no history source.</summary>
    public string MetricKey { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    /// <summary>Target the station itself reports for the metric (cycle time, parts per hour); null when it reports none.</summary>
    public double? NominalValue { get; set; }
    /// <summary>No tolerance source exists on the backend; always null.</summary>
    public double? UpperTolerance { get; set; }
    /// <summary>No tolerance source exists on the backend; always null.</summary>
    public double? LowerTolerance { get; set; }
    public List<TelemetryPointDto> Points { get; set; } = new();
    public List<AnomalyDetailDto> DetectedAnomalies { get; set; } = new();
    /// <summary>Regression / moving average over <see cref="Points"/>; null without points.</summary>
    public RollingTrendsDto? StatisticalTrends { get; set; }
    public RecipeForecastDto? RecipeForecast { get; set; }
    public List<RecipeItemDto> AvailableRecipes { get; set; } = new();
    /// <summary>Samples the in-memory history keeps per station metric (older samples are dropped).</summary>
    public int BufferCapacity { get; set; }
    /// <summary>Samples currently buffered for this station metric, before the time-range filter.</summary>
    public int BufferedSampleCount { get; set; }
    public DateTime? BufferedFrom { get; set; }
    public DateTime? BufferedTo { get; set; }
    /// <summary>Station metrics that currently have buffered history for this machine.</summary>
    public List<TrendMetricOptionDto> AvailableMetrics { get; set; } = new();
}

public class TrendMetricOptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public int SampleCount { get; set; }
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
    // Last reported machine dynamics (same values as StationOscillationBroadcastDto); null until reported.
    public double? VibrationMmS { get; set; }
    public double? SpindleRpm { get; set; }
    public double? CycleTimeMs { get; set; }
    public double? PowerKw { get; set; }
    public double? TemperatureC { get; set; }
    public double? FrequencyHz { get; set; }
    /// <summary>RUNNING | IDLE | WARNING | FAULTED | MAINTENANCE | UNKNOWN (no telemetry).</summary>
    public string OperationalStatus { get; set; } = "UNKNOWN";
    public int ActiveTicketCount { get; set; }
    public int CriticalTicketCount { get; set; }
    public bool HasSlaBreach { get; set; }
    public DateTimeOffset? LastTelemetryAt { get; set; }
}

/// <summary>
/// Fleet analytics station list (GET /api/v1/analytics/fleet-stations): one row per tenant-visible
/// machine with the dimensions it can be grouped and filtered by.
/// </summary>
public class FleetStationsDto
{
    public DateTimeOffset GeneratedAt { get; set; }
    /// <summary>Look-back window (days) of the per-station ticket figures.</summary>
    public int WindowDays { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    /// <summary>
    /// The fixed stale threshold when <c>Analytics:TelemetryStaleSeconds</c> is set; otherwise the
    /// threshold of stations whose sample interval is not learned yet. Each station's own threshold
    /// is <see cref="FleetStationDto.StaleAfterSeconds"/>.
    /// </summary>
    public int TelemetryStaleAfterSeconds { get; set; }
    /// <summary>Per-station thresholds: a station is STALE after this many missed samples; null with a fixed threshold.</summary>
    public int? StaleAfterMissedSamples { get; set; }
    /// <summary>Lower / upper clamp of the per-station threshold (seconds); null with a fixed threshold.</summary>
    public double? StaleFloorSeconds { get; set; }
    public double? StaleCeilingSeconds { get; set; }
    /// <summary>A controller whose agent reported within this window counts as online.</summary>
    public int ControllerOnlineWindowSeconds { get; set; }
    /// <summary>Hours after which an open Critical ticket counts as SLA breached.</summary>
    public double CriticalSlaHours { get; set; }
    public List<FleetStationDto> Stations { get; set; } = new();
}

/// <summary>
/// One station of the fleet analytics list: the <see cref="StationKpiDto"/> overlay (latest
/// telemetry snapshot + open tickets) extended with grouping dimensions, ticket figures for the
/// requested window and controller connectivity. Values without a data source are null.
/// </summary>
public class FleetStationDto : StationKpiDto
{
    /// <summary>The machine's own name (unique registry name).</summary>
    public string MachineName { get; set; } = string.Empty;
    public string? CustomIdentifier { get; set; }
    public string? PinnedObjectHandle { get; set; }
    public string? OrganizationId { get; set; }
    /// <summary>Organization display name; null when the machine has no organization or its name is unknown.</summary>
    public string? OrganizationName { get; set; }
    public string? MachineType { get; set; }
    /// <summary>Lifecycle status of the machine record: active | inactive | maintenance | decommissioned | dismantled.</summary>
    public string LifecycleStatus { get; set; } = "active";
    public string? CellType { get; set; }
    public string? ParentStationId { get; set; }

    // Hierarchy above / below the line (the line itself is StationKpiDto.LineId / LineName).
    public string? SubLineId { get; set; }
    public string? SubLineName { get; set; }
    public string? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public string? BusinessUnitId { get; set; }
    public string? BusinessUnitName { get; set; }

    /// <summary>
    /// RUNNING | IDLE | WARNING | FAULTED | MAINTENANCE (state reported by the PLC, telemetry fresh),
    /// UNKNOWN (telemetry fresh, no state reported), STALE (last telemetry older than
    /// <see cref="StaleAfterSeconds"/>), OFFLINE (every bound controller's agent disconnected — MQTT
    /// last will or graceful stop — and no data arrived since), NO_DATA (never reported telemetry).
    /// </summary>
    public string LiveState { get; set; } = "NO_DATA";
    /// <summary>Age of the last telemetry at <see cref="FleetStationsDto.GeneratedAt"/>; null when never reported.</summary>
    public double? TelemetryAgeSeconds { get; set; }
    /// <summary>This station's stale threshold: missed samples × learned interval, clamped; or the fixed / fallback threshold.</summary>
    public double StaleAfterSeconds { get; set; }
    /// <summary>Learned sample interval (median gap between PLC blocks seen by this backend instance); null until learned.</summary>
    public double? SampleIntervalSeconds { get; set; }

    /// <summary>Tickets created for the station inside the window (all statuses).</summary>
    public int IncidentCount { get; set; }
    /// <summary>Of those, the resolved ones; the sample of <see cref="MttrMinutes"/>.</summary>
    public int ResolvedIncidentCount { get; set; }
    /// <summary>Mean creation-to-resolution time (minutes) of the window's resolved tickets; null without any.</summary>
    public double? MttrMinutes { get; set; }
    /// <summary>Sum of the line-stop durations reported on the window's tickets; null when none reported one.</summary>
    public double? DowntimeMinutes { get; set; }
    public DateTimeOffset? LastIncidentAt { get; set; }
    /// <summary>Creation time of the oldest still open ticket; null without open tickets.</summary>
    public DateTimeOffset? OldestOpenTicketAt { get; set; }

    /// <summary>Client PCs bound to the station.</summary>
    public int ControllerCount { get; set; }
    /// <summary>Bound client PCs whose agent reported within the online window.</summary>
    public int ControllersOnline { get; set; }
    public DateTimeOffset? ControllerLastSeenAt { get; set; }
}
