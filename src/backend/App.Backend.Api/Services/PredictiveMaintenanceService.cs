using App.Backend.Api.Dtos;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;

namespace App.Backend.Api.Services;

/// <summary>
/// Predictive maintenance analytics computed exclusively from stored data:
/// <list type="bullet">
///   <item>Machines, stock and controllers from the (tenant-filtered) repositories.</item>
///   <item>Maintenance tickets for incident counts, MTTR, MTBF (observed inter-arrival of incidents) and backlog.</item>
///   <item>Station telemetry snapshots (distributed cache, written by <see cref="TelemetryIngestionService"/>)
///   for OEE / availability / performance / quality / throughput.</item>
///   <item>The Lua telemetry ring buffer (<see cref="ILuaPlantDataProvider"/>, fed by ingestion) for trend series.</item>
/// </list>
/// Values without a data source are returned as null / empty collections; nothing is synthesized.
/// </summary>
public class PredictiveMaintenanceService : IPredictiveMaintenanceService
{
    /// <summary>Look-back window for incident-based KPIs (MTTR, MTBF, incident counts, top faulting).</summary>
    public static readonly TimeSpan IncidentWindow = TimeSpan.FromDays(30);

    /// <summary>
    /// Backend SLA rule (the only one the backend has; tickets carry no due date): an open
    /// Critical ticket older than this is breached. Same rule as the fleet backlog's CriticalBreached.
    /// </summary>
    public static readonly TimeSpan CriticalSlaWindow = TimeSpan.FromHours(12);

    private readonly IAssetRepository _assetRepository;
    private readonly IControllerRepository _controllerRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;
    private readonly ICacheService _cache;
    private readonly ILogger<PredictiveMaintenanceService> _logger;
    private readonly IMachineGroupRepository? _groupRepository;
    private readonly ILuaPlantDataProvider? _telemetryHistory;

    public PredictiveMaintenanceService(
        IAssetRepository assetRepository,
        IControllerRepository controllerRepository,
        IMaintenanceTicketRepository ticketRepository,
        ICacheService cache,
        ILogger<PredictiveMaintenanceService> logger,
        IMachineGroupRepository? groupRepository = null,
        ILuaPlantDataProvider? telemetryHistory = null)
    {
        _assetRepository = assetRepository;
        _controllerRepository = controllerRepository;
        _ticketRepository = ticketRepository;
        _cache = cache;
        _logger = logger;
        _groupRepository = groupRepository;
        _telemetryHistory = telemetryHistory;
    }

    /// <summary>
    /// Computes statistical Z-scores across a telemetry sample and identifies anomalies.
    /// Flags points with |z| > 2.5 (Warning) and |z| > 3.0 (Critical).
    /// </summary>
    public List<AnomalyDetailDto> DetectAnomalies(string metricName, List<TelemetryPointDto> points)
    {
        var anomalies = new List<AnomalyDetailDto>();
        if (points == null || points.Count < 3) return anomalies;

        double mean = points.Average(p => p.Value);
        double sumSquares = points.Sum(p => Math.Pow(p.Value - mean, 2));
        double stdDev = Math.Sqrt(sumSquares / points.Count);

        if (stdDev < 0.0001) return anomalies; // No variance

        foreach (var p in points)
        {
            double z = (p.Value - mean) / stdDev;
            p.ZScore = Math.Round(z, 2);

            if (Math.Abs(z) > 2.5)
            {
                p.IsAnomaly = true;
                string severity = Math.Abs(z) > 3.0 ? "Critical" : "Warning";
                anomalies.Add(new AnomalyDetailDto
                {
                    Timestamp = p.Timestamp,
                    Metric = metricName,
                    Value = Math.Round(p.Value, 2),
                    ExpectedValue = Math.Round(mean, 2),
                    ZScore = Math.Round(z, 2),
                    Severity = severity,
                    Description = $"{metricName} deviated by {z:F1} standard deviations from mean ({mean:F1})."
                });
            }
        }

        return anomalies;
    }

    /// <summary>
    /// Heuristic health score (0-100) from real counts: -6.5 per recent anomaly, -12 per open ticket.
    /// </summary>
    public double CalculateHealthIndex(string machineId, int recentAnomalyCount, int openTicketCount)
    {
        double health = 100.0 - Math.Max(0, recentAnomalyCount) * 6.5 - Math.Max(0, openTicketCount) * 12.0;
        return Math.Clamp(Math.Round(health, 1), 0.0, 100.0);
    }

    private static bool IsOpen(MaintenanceTicket t) => t.Status is not ("Closed" or "Resolved");

    private static bool IsSlaBreached(MaintenanceTicket t, DateTimeOffset now) =>
        IsOpen(t) && t.Priority == "Critical" && now - t.CreatedAt > CriticalSlaWindow;

    private static string MachineLabel(Machine m) =>
        m.DisplayName ?? (string.IsNullOrWhiteSpace(m.CustomIdentifier) ? m.Name : m.CustomIdentifier);

    /// <summary>Mean resolution time (minutes) of resolved tickets, null when none were resolved.</summary>
    private static double? MttrMinutes(IEnumerable<MaintenanceTicket> tickets)
    {
        var durations = tickets
            .Where(t => t.ResolvedAt.HasValue && t.ResolvedAt.Value >= t.CreatedAt)
            .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes)
            .ToList();
        return durations.Count > 0 ? Math.Round(durations.Average(), 1) : null;
    }

    /// <summary>
    /// Observed MTBF (hours): mean gap between consecutive incidents on the same machine, averaged
    /// over machines with at least two incidents. Null without such machines (no runtime counters exist).
    /// </summary>
    private static double? MtbfHours(IEnumerable<MaintenanceTicket> tickets)
    {
        var gaps = tickets
            .Where(t => t.MachineId.HasValue)
            .GroupBy(t => t.MachineId!.Value)
            .Select(g => g.Select(t => t.CreatedAt).OrderBy(d => d).ToList())
            .Where(times => times.Count >= 2)
            .Select(times => (times[^1] - times[0]).TotalHours / (times.Count - 1))
            .ToList();
        return gaps.Count > 0 ? Math.Round(gaps.Average(), 1) : null;
    }

    private static double? MeanOrNull(IEnumerable<double?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count > 0 ? Math.Round(list.Average(), 1) : null;
    }

    private async Task<Dictionary<Guid, string>> LoadLineNamesAsync(CancellationToken cancellationToken)
    {
        if (_groupRepository == null) return new();
        try
        {
            var groups = await _groupRepository.GetAllAsync(cancellationToken);
            return groups.GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First().Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load machine groups for analytics");
            return new();
        }
    }

    private async Task<StationTelemetrySnapshot?> GetSnapshotAsync(Guid machineId)
    {
        try
        {
            return await _cache.GetAsync<StationTelemetrySnapshot>(StationTelemetryStore.Key(machineId));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read station telemetry snapshot for {Machine}", machineId);
            return null;
        }
    }

    /// <summary>
    /// Fleet operational statistics. Not cached across requests: the repositories are tenant-scoped,
    /// so a shared cache entry would leak one organization's summary to another.
    /// </summary>
    public async Task<FleetSummaryDto> GetFleetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var machines = await _assetRepository.GetMachinesAsync();
        var controllers = await _controllerRepository.GetAllAsync();
        var tickets = await _ticketRepository.GetAllAsync();
        var stockItems = await _assetRepository.GetStockAsync();
        var lineNames = await LoadLineNamesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var openTickets = tickets.Where(IsOpen).ToList();
        var windowTickets = tickets.Where(t => now - t.CreatedAt <= IncidentWindow).ToList();

        // Share of controllers that reported within the last 5 minutes.
        double? availability = controllers.Count > 0
            ? Math.Round(controllers.Count(c => c.LastOnline.HasValue && now - c.LastOnline.Value < TimeSpan.FromMinutes(5)) * 100.0 / controllers.Count, 1)
            : null;

        var backlog = new BacklogAgeDto
        {
            Under24Hours = openTickets.Count(t => (now - t.CreatedAt).TotalHours < 24),
            OneToThreeDays = openTickets.Count(t => (now - t.CreatedAt).TotalHours >= 24 && (now - t.CreatedAt).TotalDays < 3),
            OverThreeDays = openTickets.Count(t => (now - t.CreatedAt).TotalDays >= 3),
            CriticalBreached = openTickets.Count(t => IsSlaBreached(t, now))
        };

        var depletedStock = stockItems
            .Where(s => s.MinStockThreshold.HasValue && (s.StockQuantity ?? 0) <= s.MinStockThreshold.Value)
            .Take(5)
            .Select(s => new StockDepletionItemDto
            {
                PartNumber = s.SerialNumber ?? s.Name,
                Description = s.DisplayName ?? s.Name,
                CurrentStock = s.StockQuantity ?? 0,
                MinStockThreshold = s.MinStockThreshold ?? 0,
                Criticality = (s.StockQuantity ?? 0) == 0 ? "Critical" : "Warning"
            })
            .ToList();

        var machineById = machines.ToDictionary(m => m.Id);
        var topFaulting = windowTickets
            .Where(t => t.MachineId.HasValue && machineById.ContainsKey(t.MachineId.Value))
            .GroupBy(t => t.MachineId!.Value)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g =>
            {
                var m = machineById[g.Key];
                var downtime = g.Where(t => t.LineStopDurationMinutes.HasValue).Select(t => t.LineStopDurationMinutes!.Value).ToList();
                return new TopFaultingMachineDto
                {
                    MachineId = m.Id.ToString(),
                    Name = MachineLabel(m),
                    Line = m.LineGroupId.HasValue && lineNames.TryGetValue(m.LineGroupId.Value, out var ln) ? ln : string.Empty,
                    IncidentCount = g.Count(),
                    TotalDowntimeMinutes = downtime.Count > 0 ? Math.Round(downtime.Sum(), 1) : null,
                    PrimaryAlarmCode = g.Where(t => !string.IsNullOrWhiteSpace(t.AlarmCode))
                        .GroupBy(t => t.AlarmCode!)
                        .OrderByDescending(a => a.Count())
                        .Select(a => a.Key)
                        .FirstOrDefault() ?? string.Empty,
                    HealthIndex = CalculateHealthIndex(m.Id.ToString(), 0, g.Count(IsOpen))
                };
            })
            .ToList();

        return new FleetSummaryDto
        {
            TotalMachines = machines.Count,
            TotalMachineHours = null,
            AvailabilityPercentage = availability,
            AverageMtbfHours = MtbfHours(windowTickets),
            AverageMttrMinutes = MttrMinutes(windowTickets),
            SlaCompliancePercentage = null,
            TopFaultingMachines = topFaulting,
            MaintenanceBacklog = backlog,
            StockDepletion = depletedStock
        };
    }

    /// <summary>
    /// Catalog limits for the trend metrics (mirrors the BFF telemetry metric catalog). These are
    /// configured engineering limits, not measurements.
    /// </summary>
    private static (string MetricKey, string Label, string Unit, double Nominal, double Upper, double Lower)? ResolveTrendMetric(string metricName)
    {
        switch ((metricName ?? string.Empty).ToLowerInvariant())
        {
            case "temperature":
            case "temp":
            case "temperature_c":
                return ("temperature_c", "Temperature Drift", "°C", 48.5, 65.0, 25.0);
            case "vibration":
            case "vibration_mms":
                return ("vibration_mms", "Spindle Vibration Index", "mm/s", 1.45, 2.80, 0.50);
            case "cycle_time":
            case "cycle_time_ms":
                return ("cycle_time_ms", "Cycle Time", "ms", 1200.0, 1280.0, 1120.0);
            default:
                var known = StationTelemetryExtractor.MetricKeys.Values.FirstOrDefault(k => string.Equals(k, metricName, StringComparison.OrdinalIgnoreCase));
                return known == null ? null : (known, known, string.Empty, 0, 0, 0);
        }
    }

    /// <summary>
    /// Trend series for a machine metric from the ingested telemetry history (Lua telemetry ring
    /// buffer, last 200 samples per metric, in-process). Empty when no samples exist. Recipe-based
    /// forecasting has no data source (recipes carry no tolerance or wear model), so
    /// <see cref="MachineTrendSeriesDto.RecipeForecast"/> is null and no recipes are offered.
    /// </summary>
    public async Task<MachineTrendSeriesDto> GetMachineTrendsAsync(string machineId, string metricName, TimeSpan timeRange, string? recipeId = null, CancellationToken cancellationToken = default)
    {
        var metric = ResolveTrendMetric(metricName);
        var result = new MachineTrendSeriesDto
        {
            MachineId = machineId,
            MetricName = metric?.Label ?? metricName,
            Unit = metric?.Unit ?? string.Empty,
            NominalValue = metric?.Nominal ?? 0,
            UpperTolerance = metric?.Upper ?? 0,
            LowerTolerance = metric?.Lower ?? 0
        };
        if (metric == null || _telemetryHistory == null || string.IsNullOrWhiteSpace(machineId)) return result;

        // Resolve through the tenant-filtered repository so one tenant cannot read another's series.
        var machines = await _assetRepository.GetMachinesAsync();
        var machine = machines.FirstOrDefault(m =>
            string.Equals(m.Id.ToString(), machineId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.CustomIdentifier, machineId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Name, machineId, StringComparison.OrdinalIgnoreCase));
        if (machine == null) return result;

        var since = DateTimeOffset.UtcNow - timeRange;
        var history = await _telemetryHistory.GetTelemetryHistoryAsync(machine.Id.ToString(), metric.Value.MetricKey, 200, cancellationToken);
        var points = history
            .Where(h => h.Timestamp >= since)
            .OrderBy(h => h.Timestamp)
            .Select(h => new TelemetryPointDto { Timestamp = h.Timestamp.UtcDateTime, Value = Math.Round(h.Value, 3) })
            .ToList();
        result.Points = points;
        if (points.Count == 0) return result;

        var nominal = result.NominalValue;
        metricName = result.MetricName;
        var anomalies = DetectAnomalies(metricName, points);

        // Compute rolling statistical trends (Linear Regression, Moving Averages, Std Dev Corridors)
        int n = points.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0, sumYY = 0;
        for (int i = 0; i < n; i++)
        {
            double x = i;
            double y = points[i].Value;
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumXX += x * x;
            sumYY += y * y;
        }

        double denominator = (n * sumXX - sumX * sumX);
        double slope = denominator != 0 ? (n * sumXY - sumX * sumY) / denominator : 0;
        double intercept = n > 0 ? (sumY - slope * sumX) / n : nominal;

        // R^2 calculation
        double totalSumSquares = sumYY - (sumY * sumY) / (n > 0 ? n : 1);
        double regressionSumSquares = slope * (sumXY - (sumX * sumY) / (n > 0 ? n : 1));
        double r2 = totalSumSquares > 0.0001 ? Math.Clamp(regressionSumSquares / totalSumSquares, 0.0, 1.0) : 0.0;

        // Moving averages and standard deviation corridor
        const int window = 5;
        var movingAverages = new List<double>();
        var upperCorridor = new List<double>();
        var lowerCorridor = new List<double>();

        double overallMean = n > 0 ? sumY / n : nominal;
        double overallStdDev = n > 0 ? Math.Sqrt(Math.Max(0, (sumYY / n) - (overallMean * overallMean))) : 1.0;

        for (int i = 0; i < n; i++)
        {
            int startIdx = Math.Max(0, i - window + 1);
            int count = i - startIdx + 1;
            double windowSum = 0;
            for (int w = startIdx; w <= i; w++)
            {
                windowSum += points[w].Value;
            }
            double ma = Math.Round(windowSum / count, 2);
            movingAverages.Add(ma);
            upperCorridor.Add(Math.Round(ma + 1.5 * overallStdDev, 2));
            lowerCorridor.Add(Math.Round(ma - 1.5 * overallStdDev, 2));
        }

        // Trend Direction
        string direction = "Stable";
        double normalizedSlope = slope / (nominal != 0 ? nominal : 1.0);
        if (normalizedSlope > 0.002)
        {
            direction = "DriftingUp";
        }
        else if (normalizedSlope < -0.002)
        {
            direction = "DriftingDown";
        }
        else if (overallStdDev / (overallMean != 0 ? overallMean : 1.0) > 0.12)
        {
            direction = "Volatile";
        }

        var rollingTrends = new RollingTrendsDto
        {
            LinearRegressionSlope = Math.Round(slope, 4),
            LinearRegressionIntercept = Math.Round(intercept, 2),
            R2Score = Math.Round(r2, 3),
            MovingAverageWindow = window,
            MovingAverages = movingAverages,
            UpperStdDevCorridor = upperCorridor,
            LowerStdDevCorridor = lowerCorridor,
            TrendDirection = direction
        };

        result.DetectedAnomalies = anomalies;
        result.StatisticalTrends = rollingTrends;
        return result;
    }

    /// <summary>
    /// Line KPIs grouped by each machine's line group. Incident metrics come from tickets in the
    /// last <see cref="IncidentWindow"/>; OEE / A / P / Q are the mean over the line's stations that
    /// report them (null when none do). Lines without machines are not listed.
    /// </summary>
    public async Task<List<LineKpiDto>> GetLineKpisAsync(CancellationToken cancellationToken = default)
    {
        var machines = (await _assetRepository.GetMachinesAsync()).Where(m => m.LineGroupId.HasValue).ToList();
        if (machines.Count == 0) return new();

        var lineNames = await LoadLineNamesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var tickets = (await _ticketRepository.GetAllAsync())
            .Where(t => t.MachineId.HasValue && now - t.CreatedAt <= IncidentWindow)
            .ToList();

        var result = new List<LineKpiDto>();
        foreach (var line in machines.GroupBy(m => m.LineGroupId!.Value))
        {
            var ids = line.Select(m => m.Id).ToHashSet();
            var lineTickets = tickets.Where(t => ids.Contains(t.MachineId!.Value)).ToList();
            var snapshots = new List<StationTelemetrySnapshot>();
            foreach (var m in line)
            {
                var snap = await GetSnapshotAsync(m.Id);
                if (snap != null) snapshots.Add(snap);
            }

            result.Add(new LineKpiDto
            {
                LineId = line.Key.ToString(),
                LineName = lineNames.TryGetValue(line.Key, out var name) ? name : line.Key.ToString(),
                Oee = MeanOrNull(snapshots.Select(s => s.EffectiveOee)),
                Availability = MeanOrNull(snapshots.Select(s => s.Availability)),
                Performance = MeanOrNull(snapshots.Select(s => s.Performance)),
                Quality = MeanOrNull(snapshots.Select(s => s.EffectiveQuality)),
                MtbfHours = MtbfHours(lineTickets),
                MttrMinutes = MttrMinutes(lineTickets),
                IncidentCount = lineTickets.Count
            });
        }
        return result.OrderBy(l => l.LineName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Station overlays for the plant map: one entry per (tenant-visible) machine. Production
    /// values come from the station's telemetry snapshot and are null until its PLC reports them;
    /// ticket counts and SLA breach come from stored maintenance tickets.
    /// </summary>
    public async Task<List<StationKpiDto>> GetStationTelemetryKpisAsync(string? lineId = null, string? stationId = null, CancellationToken cancellationToken = default)
    {
        var machines = await _assetRepository.GetMachinesAsync();
        if (machines.Count == 0) return new();

        var lineNames = await LoadLineNamesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var openByMachine = (await _ticketRepository.GetAllAsync())
            .Where(t => t.MachineId.HasValue && IsOpen(t))
            .GroupBy(t => t.MachineId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<StationKpiDto>();
        foreach (var m in machines)
        {
            var handle = TelemetryIngestionService.StationHandle(m);
            string? lineName = m.LineGroupId.HasValue && lineNames.TryGetValue(m.LineGroupId.Value, out var ln) ? ln : null;

            if (!string.IsNullOrWhiteSpace(lineId) &&
                !string.Equals(m.LineGroupId?.ToString(), lineId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(lineName, lineId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (!string.IsNullOrWhiteSpace(stationId) &&
                !string.Equals(m.Id.ToString(), stationId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(handle, stationId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(m.CustomIdentifier, stationId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var snap = await GetSnapshotAsync(m.Id);
            var open = openByMachine.TryGetValue(m.Id, out var list) ? list : new List<MaintenanceTicket>();

            result.Add(new StationKpiDto
            {
                StationId = m.Id.ToString(),
                Handle = handle,
                Name = MachineLabel(m),
                LineId = m.LineGroupId?.ToString(),
                LineName = lineName,
                Oee = snap?.EffectiveOee,
                Availability = snap?.Availability,
                Performance = snap?.Performance,
                Quality = snap?.EffectiveQuality,
                Yield = snap?.Yield,
                PartsPerHour = snap?.PartsPerHour,
                TargetPartsPerHour = snap?.TargetPartsPerHour,
                CycleTimeSeconds = snap?.EffectiveCycleTimeSeconds,
                TargetCycleTimeSeconds = snap?.TargetCycleTimeSeconds,
                TotalPartsProduced = snap?.TotalPartsProduced,
                ScrapParts = snap?.ScrapParts,
                OperationalStatus = snap?.OperationalStatus
                    ?? (string.Equals(m.Status, "maintenance", StringComparison.OrdinalIgnoreCase) ? "MAINTENANCE" : "UNKNOWN"),
                ActiveTicketCount = open.Count,
                CriticalTicketCount = open.Count(t => t.Priority == "Critical"),
                HasSlaBreach = open.Any(t => IsSlaBreached(t, now)),
                LastTelemetryAt = snap?.LastTelemetryAt
            });
        }
        return result;
    }
}
