using App.Backend.Api.Dtos;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Api.Services;

/// <summary>
/// Predictive maintenance analytics computed exclusively from stored data:
/// <list type="bullet">
///   <item>Machines, stock and controllers from the (tenant-filtered) repositories.</item>
///   <item>Maintenance tickets for incident counts, MTTR, repeat-incident interval and backlog.</item>
///   <item>Station telemetry snapshots (distributed cache, written by <see cref="TelemetryIngestionService"/>)
///   for OEE / availability / performance / quality / throughput.</item>
///   <item>The Lua telemetry ring buffer (<see cref="ILuaPlantDataProvider"/>, fed by ingestion) for trend series.</item>
///   <item>Machine groups (and their membership rows) for the business unit / project / line / sub-line of a machine.</item>
/// </list>
/// Values without a data source are returned as null / empty collections; nothing is synthesized.
/// </summary>
public class PredictiveMaintenanceService : IPredictiveMaintenanceService
{
    /// <summary>Default look-back window for incident-based KPIs (MTTR, repeat interval, incident counts, top faulting).</summary>
    public static readonly TimeSpan IncidentWindow = TimeSpan.FromDays(30);

    /// <summary>Longest look-back window a caller may request.</summary>
    public static readonly TimeSpan MaxIncidentWindow = TimeSpan.FromDays(365);

    /// <summary>
    /// Backend SLA rule (the only one the backend has; tickets carry no due date): an open
    /// Critical ticket older than this is breached. Same rule as the fleet backlog's CriticalBreached.
    /// </summary>
    public static readonly TimeSpan CriticalSlaWindow = TimeSpan.FromHours(12);

    /// <summary>A controller whose agent reported within this window counts as online.</summary>
    public static readonly TimeSpan ControllerOnlineWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default age after which a station's last telemetry is stale. Healthy stations report at
    /// least every keep-alive interval (10 s by default), so a minute of silence means the
    /// station, its controller or the agent stopped reporting.
    /// </summary>
    public static readonly TimeSpan DefaultTelemetryStaleAfter = TimeSpan.FromSeconds(60);

    /// <summary>Samples the telemetry history keeps per station metric (see DbPlantDataProvider).</summary>
    public const int TrendBufferCapacity = 200;

    /// <summary>Stock items listed in the fleet summary (the total is reported separately).</summary>
    private const int StockDepletionLimit = 8;

    private readonly IAssetRepository _assetRepository;
    private readonly IControllerRepository _controllerRepository;
    private readonly IMaintenanceTicketRepository _ticketRepository;
    private readonly ICacheService _cache;
    private readonly ILogger<PredictiveMaintenanceService> _logger;
    private readonly IMachineGroupRepository? _groupRepository;
    private readonly ILuaPlantDataProvider? _telemetryHistory;
    private readonly AppDbContext? _db;

    public PredictiveMaintenanceService(
        IAssetRepository assetRepository,
        IControllerRepository controllerRepository,
        IMaintenanceTicketRepository ticketRepository,
        ICacheService cache,
        ILogger<PredictiveMaintenanceService> logger,
        IMachineGroupRepository? groupRepository = null,
        ILuaPlantDataProvider? telemetryHistory = null,
        AppDbContext? db = null)
    {
        _assetRepository = assetRepository;
        _controllerRepository = controllerRepository;
        _ticketRepository = ticketRepository;
        _cache = cache;
        _logger = logger;
        _groupRepository = groupRepository;
        _telemetryHistory = telemetryHistory;
        _db = db;
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

    // ------------------------------------------------------------------ facts loaded from storage

    /// <summary>The ticket columns the analytics need (no navigation graphs).</summary>
    private sealed record TicketFact(
        Guid? MachineId,
        string Status,
        string Priority,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ResolvedAt,
        double? LineStopDurationMinutes,
        string? AlarmCode)
    {
        public bool IsOpen => Status is not ("Closed" or "Resolved");
        public bool IsSlaBreached(DateTimeOffset now) => IsOpen && Priority == "Critical" && now - CreatedAt > CriticalSlaWindow;
    }

    /// <summary>A client PC with the machines it controls.</summary>
    private sealed record ControllerFact(Guid Id, DateTimeOffset? LastOnline, IReadOnlyList<Guid> MachineIds)
    {
        public bool IsOnline(DateTimeOffset now) => LastOnline.HasValue && now - LastOnline.Value < ControllerOnlineWindow;
    }

    private static TicketFact ToFact(MaintenanceTicket t) =>
        new(t.MachineId, t.Status, t.Priority, t.CreatedAt, t.ResolvedAt, t.LineStopDurationMinutes, t.AlarmCode);

    /// <summary>
    /// Tenant-visible tickets. With a DbContext only the needed columns are read; the repository
    /// fallback (unit tests, hosts without a context) loads the full entities.
    /// </summary>
    private async Task<List<TicketFact>> LoadTicketsAsync(CancellationToken cancellationToken)
    {
        if (_db != null)
        {
            var rows = await _db.MaintenanceTickets.AsNoTracking()
                .Select(t => new { t.MachineId, t.Status, t.Priority, t.CreatedAt, t.ResolvedAt, t.LineStopDurationMinutes, t.AlarmCode })
                .ToListAsync(cancellationToken);
            return rows
                .Select(t => new TicketFact(t.MachineId, t.Status, t.Priority, t.CreatedAt, t.ResolvedAt, t.LineStopDurationMinutes, t.AlarmCode))
                .ToList();
        }
        return (await _ticketRepository.GetAllAsync()).Select(ToFact).ToList();
    }

    private async Task<List<ControllerFact>> LoadControllersAsync(CancellationToken cancellationToken)
    {
        if (_db != null)
        {
            var rows = await _db.ClientPcs.AsNoTracking()
                .Select(c => new { c.Id, c.LastOnline, MachineIds = c.ControlledMachines.Select(m => m.Id).ToList() })
                .ToListAsync(cancellationToken);
            return rows.Select(c => new ControllerFact(c.Id, c.LastOnline, c.MachineIds)).ToList();
        }
        return (await _controllerRepository.GetAllAsync())
            .Select(c => new ControllerFact(c.Id, c.LastOnline, (c.ControlledMachines ?? new List<Machine>()).Select(m => m.Id).ToList()))
            .ToList();
    }

    /// <summary>Machine groups (plus membership rows when a DbContext is available) as a hierarchy resolver.</summary>
    private async Task<MachineHierarchyResolver> LoadHierarchyAsync(CancellationToken cancellationToken)
    {
        var groups = new List<MachineGroup>();
        var memberships = new List<(Guid GroupId, Guid MachineId)>();
        try
        {
            if (_groupRepository != null) groups = await _groupRepository.GetAllAsync(cancellationToken);
            if (_db != null)
            {
                var rows = await _db.MachineGroupMemberships.AsNoTracking()
                    .Select(m => new { m.GroupId, m.MachineId })
                    .ToListAsync(cancellationToken);
                memberships = rows.Select(m => (m.GroupId, m.MachineId)).ToList();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load machine groups for analytics");
        }
        return new MachineHierarchyResolver(groups, memberships);
    }

    /// <summary>Display names of the given organizations; empty without a DbContext.</summary>
    private async Task<Dictionary<string, string>> LoadOrganizationNamesAsync(IEnumerable<string?> organizationIds, CancellationToken cancellationToken)
    {
        var ids = organizationIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct().ToList();
        if (_db == null || ids.Count == 0) return new();
        try
        {
            var rows = await _db.AuthOrganizations.AsNoTracking()
                .Where(o => ids.Contains(o.Id))
                .Select(o => new { o.Id, o.Name })
                .ToListAsync(cancellationToken);
            return rows.Where(o => !string.IsNullOrWhiteSpace(o.Name)).ToDictionary(o => o.Id, o => o.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to load organization names for analytics");
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

    // ------------------------------------------------------------------ shared figures

    private static string MachineLabel(Machine m) =>
        m.DisplayName ?? (string.IsNullOrWhiteSpace(m.CustomIdentifier) ? m.Name : m.CustomIdentifier);

    private static bool IsDecommissioned(Machine m) =>
        string.Equals(m.Status, "decommissioned", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(m.Status, "dismantled", StringComparison.OrdinalIgnoreCase);

    /// <summary>Requested window clamped to [1 day, <see cref="MaxIncidentWindow"/>]; the default when omitted.</summary>
    public static TimeSpan NormalizeWindow(TimeSpan? window)
    {
        if (window is not { } w || w <= TimeSpan.Zero) return IncidentWindow;
        if (w < TimeSpan.FromDays(1)) return TimeSpan.FromDays(1);
        return w > MaxIncidentWindow ? MaxIncidentWindow : w;
    }

    /// <summary>Mean resolution time (minutes) of resolved tickets, null when none were resolved.</summary>
    private static double? MttrMinutes(IEnumerable<TicketFact> tickets)
    {
        var durations = ResolutionMinutes(tickets);
        return durations.Count > 0 ? Math.Round(durations.Average(), 1) : null;
    }

    private static List<double> ResolutionMinutes(IEnumerable<TicketFact> tickets) =>
        tickets
            .Where(t => t.ResolvedAt.HasValue && t.ResolvedAt.Value >= t.CreatedAt)
            .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes)
            .ToList();

    /// <summary>
    /// Repeat-incident interval per machine (hours): mean gap between consecutive incidents on the
    /// same machine, for machines with at least two incidents.
    /// </summary>
    private static List<double> RepeatIncidentGapsHours(IEnumerable<TicketFact> tickets) =>
        tickets
            .Where(t => t.MachineId.HasValue)
            .GroupBy(t => t.MachineId!.Value)
            .Select(g => g.Select(t => t.CreatedAt).OrderBy(d => d).ToList())
            .Where(times => times.Count >= 2)
            .Select(times => (times[^1] - times[0]).TotalHours / (times.Count - 1))
            .ToList();

    private static double? MtbfHours(IEnumerable<TicketFact> tickets)
    {
        var gaps = RepeatIncidentGapsHours(tickets);
        return gaps.Count > 0 ? Math.Round(gaps.Average(), 1) : null;
    }

    private static double? MeanOrNull(IEnumerable<double?> values)
    {
        var list = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return list.Count > 0 ? Math.Round(list.Average(), 1) : null;
    }

    private static double? DowntimeMinutes(IEnumerable<TicketFact> tickets)
    {
        var reported = tickets.Where(t => t.LineStopDurationMinutes.HasValue).Select(t => t.LineStopDurationMinutes!.Value).ToList();
        return reported.Count > 0 ? Math.Round(reported.Sum(), 1) : null;
    }

    /// <summary>
    /// Live state of a station from its snapshot: the PLC reported state while telemetry is fresh,
    /// UNKNOWN when fresh telemetry carries no state, STALE once the last sample is older than
    /// <paramref name="staleAfter"/>, NO_DATA when the station never reported.
    /// </summary>
    public static string LiveState(StationTelemetrySnapshot? snapshot, DateTimeOffset now, TimeSpan staleAfter)
    {
        if (snapshot?.LastTelemetryAt is not { } last) return "NO_DATA";
        if (now - last > staleAfter) return "STALE";
        return snapshot.OperationalStatus ?? "UNKNOWN";
    }

    // ------------------------------------------------------------------ fleet summary

    /// <summary>
    /// Fleet maintenance figures. Not cached across requests: the repositories are tenant-scoped,
    /// so a shared cache entry would leak one organization's summary to another.
    /// </summary>
    public async Task<FleetSummaryDto> GetFleetSummaryAsync(TimeSpan? window = null, CancellationToken cancellationToken = default)
    {
        var effectiveWindow = NormalizeWindow(window);
        var machines = await _assetRepository.GetMachinesAsync();
        var controllers = await LoadControllersAsync(cancellationToken);
        var tickets = await LoadTicketsAsync(cancellationToken);
        var stockItems = await _assetRepository.GetStockAsync();
        var hierarchy = await LoadHierarchyAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var openTickets = tickets.Where(t => t.IsOpen).ToList();
        var windowTickets = tickets.Where(t => now - t.CreatedAt <= effectiveWindow).ToList();

        var controllersOnline = controllers.Count(c => c.IsOnline(now));

        var backlog = new BacklogAgeDto
        {
            Under24Hours = openTickets.Count(t => (now - t.CreatedAt).TotalHours < 24),
            OneToThreeDays = openTickets.Count(t => (now - t.CreatedAt).TotalHours >= 24 && (now - t.CreatedAt).TotalDays < 3),
            OverThreeDays = openTickets.Count(t => (now - t.CreatedAt).TotalDays >= 3),
            CriticalBreached = openTickets.Count(t => t.IsSlaBreached(now))
        };

        var depleted = stockItems
            .Where(s => s.MinStockThreshold.HasValue && (s.StockQuantity ?? 0) <= s.MinStockThreshold.Value)
            .ToList();
        var depletedStock = depleted
            // Out of stock first, then the largest shortfall against the minimum.
            .OrderBy(s => (s.StockQuantity ?? 0) == 0 ? 0 : 1)
            .ThenByDescending(s => (s.MinStockThreshold ?? 0) - (s.StockQuantity ?? 0))
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Take(StockDepletionLimit)
            .Select(s => new StockDepletionItemDto
            {
                PartNumber = s.SerialNumber ?? s.Name,
                Description = s.DisplayName ?? s.Name,
                CurrentStock = s.StockQuantity ?? 0,
                MinStockThreshold = s.MinStockThreshold ?? 0,
                Criticality = (s.StockQuantity ?? 0) == 0 ? "Critical" : "Warning"
            })
            .ToList();

        var machineById = machines.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        var topFaulting = windowTickets
            .Where(t => t.MachineId.HasValue && machineById.ContainsKey(t.MachineId.Value))
            .GroupBy(t => t.MachineId!.Value)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Count(t => t.IsOpen))
            .ThenBy(g => MachineLabel(machineById[g.Key]), StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(g =>
            {
                var m = machineById[g.Key];
                return new TopFaultingMachineDto
                {
                    MachineId = m.Id.ToString(),
                    Name = MachineLabel(m),
                    Identifier = string.IsNullOrWhiteSpace(m.CustomIdentifier) ? m.Name : m.CustomIdentifier,
                    Line = hierarchy.Resolve(m).Line.Name ?? string.Empty,
                    IncidentCount = g.Count(),
                    OpenTicketCount = g.Count(t => t.IsOpen),
                    TotalDowntimeMinutes = DowntimeMinutes(g),
                    PrimaryAlarmCode = g.Where(t => !string.IsNullOrWhiteSpace(t.AlarmCode))
                        .GroupBy(t => t.AlarmCode!)
                        .OrderByDescending(a => a.Count())
                        .ThenBy(a => a.Key, StringComparer.Ordinal)
                        .Select(a => a.Key)
                        .FirstOrDefault() ?? string.Empty
                };
            })
            .ToList();

        return new FleetSummaryDto
        {
            GeneratedAt = now,
            WindowDays = (int)Math.Round(effectiveWindow.TotalDays),
            TotalMachines = machines.Count,
            ControllerCount = controllers.Count,
            ControllersOnline = controllersOnline,
            ControllerOnlinePercentage = controllers.Count > 0 ? Math.Round(controllersOnline * 100.0 / controllers.Count, 1) : null,
            ControllerOnlineWindowSeconds = (int)ControllerOnlineWindow.TotalSeconds,
            IncidentCount = windowTickets.Count,
            ResolvedIncidentCount = ResolutionMinutes(windowTickets).Count,
            AverageMttrMinutes = MttrMinutes(windowTickets),
            AverageMtbfHours = MtbfHours(windowTickets),
            MtbfMachineCount = RepeatIncidentGapsHours(windowTickets).Count,
            CriticalSlaHours = CriticalSlaWindow.TotalHours,
            TopFaultingMachines = topFaulting,
            MaintenanceBacklog = backlog,
            StockDepletion = depletedStock,
            StockDepletionTotal = depleted.Count
        };
    }

    // ------------------------------------------------------------------ trends

    /// <summary>Station metrics with a telemetry history (key as written by ingestion, display name, unit).</summary>
    private static readonly (string Key, string Name, string Unit)[] TrendMetrics =
    {
        ("oee_pct", "OEE", "%"),
        ("availability_pct", "Availability", "%"),
        ("performance_pct", "Performance", "%"),
        ("quality_pct", "Quality", "%"),
        ("parts_per_hour", "Throughput", "parts/h"),
        ("target_parts_per_hour", "Target throughput", "parts/h"),
        ("cycle_time_ms", "Cycle time", "ms"),
        ("cycle_time_s", "Cycle time", "s"),
        ("target_cycle_time_s", "Target cycle time", "s"),
        ("parts_produced", "Parts produced", "parts"),
        ("scrap_parts", "Scrap parts", "parts"),
        ("vibration_mms", "Vibration", "mm/s"),
        ("temperature_c", "Temperature", "°C"),
        ("power_kw", "Active power", "kW"),
        ("spindle_rpm", "Spindle speed", "rpm"),
        ("frequency_hz", "Frequency", "Hz")
    };

    /// <summary>Request names accepted for a history key (short names and the BFF metric catalog keys).</summary>
    private static readonly Dictionary<string, string> TrendMetricAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["temperature"] = "temperature_c",
        ["temp"] = "temperature_c",
        ["motor_temp_c"] = "temperature_c",
        ["vibration"] = "vibration_mms",
        ["vibration_rms_mms"] = "vibration_mms",
        ["cycle_time"] = "cycle_time_ms",
        ["oee"] = "oee_pct",
        ["oee_percentage"] = "oee_pct",
        ["availability"] = "availability_pct",
        ["station_availability_pct"] = "availability_pct",
        ["performance"] = "performance_pct",
        ["quality"] = "quality_pct",
        ["production_yield_pct"] = "quality_pct",
        ["power"] = "power_kw",
        ["throughput"] = "parts_per_hour"
    };

    private static (string Key, string Name, string Unit)? ResolveTrendMetric(string? metricName)
    {
        var requested = (metricName ?? string.Empty).Trim();
        if (requested.Length == 0) return null;
        var key = TrendMetricAliases.TryGetValue(requested, out var alias) ? alias : requested;
        foreach (var metric in TrendMetrics)
        {
            if (string.Equals(metric.Key, key, StringComparison.OrdinalIgnoreCase)) return metric;
        }
        return null;
    }

    /// <summary>The target the station itself reports for a metric, null when it reports none.</summary>
    private static double? StationTarget(string metricKey, StationTelemetrySnapshot? snapshot) => metricKey switch
    {
        "cycle_time_ms" => snapshot?.TargetCycleTimeSeconds * 1000.0,
        "cycle_time_s" => snapshot?.TargetCycleTimeSeconds,
        "parts_per_hour" => snapshot?.TargetPartsPerHour,
        _ => null
    };

    /// <summary>
    /// Trend series for a machine metric from the ingested telemetry history (Lua telemetry ring
    /// buffer: the last <see cref="TrendBufferCapacity"/> samples per metric, in-process, lost on
    /// restart). Empty when no samples exist; the response says how much history is buffered so a
    /// caller can tell a short window from missing data. The nominal value is the target the
    /// station reports (cycle time, throughput); no tolerance limits or recipe wear models exist
    /// on the backend, so those stay null / empty.
    /// </summary>
    public async Task<MachineTrendSeriesDto> GetMachineTrendsAsync(string machineId, string metricName, TimeSpan timeRange, string? recipeId = null, CancellationToken cancellationToken = default)
    {
        var metric = ResolveTrendMetric(metricName);
        var result = new MachineTrendSeriesDto
        {
            MachineId = machineId,
            MetricKey = metric?.Key ?? string.Empty,
            MetricName = metric?.Name ?? metricName,
            Unit = metric?.Unit ?? string.Empty,
            BufferCapacity = TrendBufferCapacity
        };
        if (_telemetryHistory == null || string.IsNullOrWhiteSpace(machineId)) return result;

        // Resolve through the tenant-filtered repository so one tenant cannot read another's series.
        var machines = await _assetRepository.GetMachinesAsync();
        var machine = machines.FirstOrDefault(m =>
            string.Equals(m.Id.ToString(), machineId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.CustomIdentifier, machineId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(m.Name, machineId, StringComparison.OrdinalIgnoreCase));
        if (machine == null) return result;

        var historyId = machine.Id.ToString();
        foreach (var candidate in TrendMetrics)
        {
            var buffered = await _telemetryHistory.GetTelemetryHistoryAsync(historyId, candidate.Key, TrendBufferCapacity, cancellationToken);
            if (buffered.Count > 0)
            {
                result.AvailableMetrics.Add(new TrendMetricOptionDto
                {
                    Key = candidate.Key,
                    Name = candidate.Name,
                    Unit = candidate.Unit,
                    SampleCount = buffered.Count
                });
            }
        }
        if (metric == null) return result;

        result.NominalValue = StationTarget(metric.Value.Key, await GetSnapshotAsync(machine.Id));

        var history = (await _telemetryHistory.GetTelemetryHistoryAsync(historyId, metric.Value.Key, TrendBufferCapacity, cancellationToken))
            .OrderBy(h => h.Timestamp)
            .ToList();
        result.BufferedSampleCount = history.Count;
        if (history.Count > 0)
        {
            result.BufferedFrom = history[0].Timestamp.UtcDateTime;
            result.BufferedTo = history[^1].Timestamp.UtcDateTime;
        }

        var since = DateTimeOffset.UtcNow - timeRange;
        var points = history
            .Where(h => h.Timestamp >= since)
            .Select(h => new TelemetryPointDto { Timestamp = h.Timestamp.UtcDateTime, Value = Math.Round(h.Value, 3) })
            .ToList();
        result.Points = points;
        if (points.Count == 0) return result;

        var anomalies = DetectAnomalies(result.MetricName, points);

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
        double intercept = (sumY - slope * sumX) / n;

        // R^2 calculation
        double totalSumSquares = sumYY - (sumY * sumY) / n;
        double regressionSumSquares = slope * (sumXY - (sumX * sumY) / n);
        double r2 = totalSumSquares > 0.0001 ? Math.Clamp(regressionSumSquares / totalSumSquares, 0.0, 1.0) : 0.0;

        // Moving averages and standard deviation corridor
        const int window = 5;
        var movingAverages = new List<double>();
        var upperCorridor = new List<double>();
        var lowerCorridor = new List<double>();

        double overallMean = sumY / n;
        double overallStdDev = Math.Sqrt(Math.Max(0, (sumYY / n) - (overallMean * overallMean)));

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

        // Trend direction relative to the level of the series itself (its mean), so the rule
        // means the same for a 1.4 mm/s vibration and a 16 000 ms cycle time.
        double level = Math.Abs(overallMean) > 1e-9 ? Math.Abs(overallMean) : 1.0;
        string direction = "Stable";
        double normalizedSlope = slope / level;
        if (normalizedSlope > 0.002)
        {
            direction = "DriftingUp";
        }
        else if (normalizedSlope < -0.002)
        {
            direction = "DriftingDown";
        }
        else if (overallStdDev / level > 0.12)
        {
            direction = "Volatile";
        }

        result.DetectedAnomalies = anomalies;
        result.StatisticalTrends = new RollingTrendsDto
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
        return result;
    }

    // ------------------------------------------------------------------ line KPIs

    /// <summary>
    /// Line KPIs grouped by each machine's line: its line machine group, else the legacy line text
    /// on the machine. Incident metrics come from tickets in the last <see cref="IncidentWindow"/>;
    /// OEE / A / P / Q are the mean over the line's stations that report them (null when none do).
    /// Machines without a line and lines without machines are not listed.
    /// </summary>
    public async Task<List<LineKpiDto>> GetLineKpisAsync(CancellationToken cancellationToken = default)
    {
        var allMachines = await _assetRepository.GetMachinesAsync();
        if (allMachines.Count == 0) return new();

        var hierarchy = await LoadHierarchyAsync(cancellationToken);
        var lines = allMachines
            .Select(m => (Machine: m, Line: hierarchy.Resolve(m).Line))
            .Where(x => x.Line.IsAssigned)
            .GroupBy(x => x.Line.Key!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (lines.Count == 0) return new();

        var now = DateTimeOffset.UtcNow;
        var tickets = (await LoadTicketsAsync(cancellationToken))
            .Where(t => t.MachineId.HasValue && now - t.CreatedAt <= IncidentWindow)
            .ToList();

        var result = new List<LineKpiDto>();
        foreach (var line in lines)
        {
            var ids = line.Select(x => x.Machine.Id).ToHashSet();
            var lineTickets = tickets.Where(t => ids.Contains(t.MachineId!.Value)).ToList();
            var snapshots = new List<StationTelemetrySnapshot>();
            foreach (var x in line)
            {
                var snap = await GetSnapshotAsync(x.Machine.Id);
                if (snap != null) snapshots.Add(snap);
            }

            var level = line.First().Line;
            result.Add(new LineKpiDto
            {
                LineId = line.Key,
                LineName = level.Name ?? line.Key,
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

    // ------------------------------------------------------------------ station overlays

    private static void FillStationKpi(
        StationKpiDto dto, Machine m, HierarchyLevel line, StationTelemetrySnapshot? snap,
        IReadOnlyCollection<TicketFact> openTickets, DateTimeOffset now)
    {
        dto.StationId = m.Id.ToString();
        dto.Handle = TelemetryIngestionService.StationHandle(m);
        dto.Name = MachineLabel(m);
        dto.LineId = line.Id?.ToString();
        dto.LineName = line.Name;
        dto.Oee = snap?.EffectiveOee;
        dto.Availability = snap?.Availability;
        dto.Performance = snap?.Performance;
        dto.Quality = snap?.EffectiveQuality;
        dto.Yield = snap?.Yield;
        dto.PartsPerHour = snap?.PartsPerHour;
        dto.TargetPartsPerHour = snap?.TargetPartsPerHour;
        dto.CycleTimeSeconds = snap?.EffectiveCycleTimeSeconds;
        dto.TargetCycleTimeSeconds = snap?.TargetCycleTimeSeconds;
        dto.TotalPartsProduced = snap?.TotalPartsProduced;
        dto.ScrapParts = snap?.ScrapParts;
        dto.VibrationMmS = snap?.VibrationMmS;
        dto.SpindleRpm = snap?.SpindleRpm;
        dto.CycleTimeMs = snap?.CycleTimeMs;
        dto.PowerKw = snap?.PowerKw;
        dto.TemperatureC = snap?.TemperatureC;
        dto.FrequencyHz = snap?.FrequencyHz;
        dto.OperationalStatus = snap?.OperationalStatus
            ?? (string.Equals(m.Status, "maintenance", StringComparison.OrdinalIgnoreCase) ? "MAINTENANCE" : "UNKNOWN");
        dto.ActiveTicketCount = openTickets.Count;
        dto.CriticalTicketCount = openTickets.Count(t => t.Priority == "Critical");
        dto.HasSlaBreach = openTickets.Any(t => t.IsSlaBreached(now));
        dto.LastTelemetryAt = snap?.LastTelemetryAt;
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

        var hierarchy = await LoadHierarchyAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var openByMachine = (await LoadTicketsAsync(cancellationToken))
            .Where(t => t.MachineId.HasValue && t.IsOpen)
            .GroupBy(t => t.MachineId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<StationKpiDto>();
        foreach (var m in machines)
        {
            var handle = TelemetryIngestionService.StationHandle(m);
            var line = hierarchy.Resolve(m).Line;

            if (!string.IsNullOrWhiteSpace(lineId) &&
                !string.Equals(line.Id?.ToString(), lineId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(line.Name, lineId, StringComparison.OrdinalIgnoreCase))
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
            var open = openByMachine.TryGetValue(m.Id, out var list) ? list : new List<TicketFact>();
            var dto = new StationKpiDto();
            FillStationKpi(dto, m, line, snap, open, now);
            result.Add(dto);
        }
        return result;
    }

    // ------------------------------------------------------------------ fleet station list

    /// <summary>
    /// Fleet analytics rows: one per (tenant-visible) machine with its hierarchy, organization,
    /// type, latest telemetry snapshot, ticket figures for <paramref name="window"/> and the
    /// connectivity of its controllers. Decommissioned / dismantled machines are left out unless
    /// asked for. Nothing is cached across requests (tenant-scoped data).
    /// </summary>
    public async Task<FleetStationsDto> GetFleetStationsAsync(
        TimeSpan? window = null,
        bool includeDecommissioned = false,
        TimeSpan? telemetryStaleAfter = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveWindow = NormalizeWindow(window);
        var staleAfter = telemetryStaleAfter is { } s && s > TimeSpan.Zero ? s : DefaultTelemetryStaleAfter;
        var now = DateTimeOffset.UtcNow;
        var windowStart = now - effectiveWindow;

        var result = new FleetStationsDto
        {
            GeneratedAt = now,
            WindowDays = (int)Math.Round(effectiveWindow.TotalDays),
            WindowStart = windowStart,
            TelemetryStaleAfterSeconds = (int)Math.Round(staleAfter.TotalSeconds),
            ControllerOnlineWindowSeconds = (int)ControllerOnlineWindow.TotalSeconds,
            CriticalSlaHours = CriticalSlaWindow.TotalHours
        };

        var machines = (await _assetRepository.GetMachinesAsync())
            .Where(m => includeDecommissioned || !IsDecommissioned(m))
            .ToList();
        if (machines.Count == 0) return result;

        var hierarchy = await LoadHierarchyAsync(cancellationToken);
        var ticketsByMachine = (await LoadTicketsAsync(cancellationToken))
            .Where(t => t.MachineId.HasValue)
            .ToLookup(t => t.MachineId!.Value);
        var controllersByMachine = new Dictionary<Guid, List<ControllerFact>>();
        foreach (var controller in await LoadControllersAsync(cancellationToken))
        {
            foreach (var machineId in controller.MachineIds.Distinct())
            {
                if (!controllersByMachine.TryGetValue(machineId, out var list))
                {
                    controllersByMachine[machineId] = list = new List<ControllerFact>();
                }
                list.Add(controller);
            }
        }
        var organizationNames = await LoadOrganizationNamesAsync(machines.Select(m => m.OrganizationId), cancellationToken);

        foreach (var m in machines)
        {
            var placement = hierarchy.Resolve(m);
            var snap = await GetSnapshotAsync(m.Id);
            var tickets = ticketsByMachine[m.Id].ToList();
            var open = tickets.Where(t => t.IsOpen).ToList();
            var inWindow = tickets.Where(t => t.CreatedAt >= windowStart).ToList();
            var controllers = controllersByMachine.TryGetValue(m.Id, out var bound) ? bound : new List<ControllerFact>();
            var lastSeen = controllers.Where(c => c.LastOnline.HasValue).Select(c => c.LastOnline!.Value).DefaultIfEmpty().Max();

            var dto = new FleetStationDto
            {
                MachineName = m.Name,
                CustomIdentifier = string.IsNullOrWhiteSpace(m.CustomIdentifier) ? null : m.CustomIdentifier,
                PinnedObjectHandle = string.IsNullOrWhiteSpace(m.PinnedObjectHandle) ? null : m.PinnedObjectHandle,
                OrganizationId = string.IsNullOrWhiteSpace(m.OrganizationId) ? null : m.OrganizationId,
                OrganizationName = m.OrganizationId != null && organizationNames.TryGetValue(m.OrganizationId, out var orgName) ? orgName : null,
                MachineType = string.IsNullOrWhiteSpace(m.MachineType) ? null : m.MachineType,
                LifecycleStatus = string.IsNullOrWhiteSpace(m.Status) ? "active" : m.Status.Trim().ToLowerInvariant(),
                CellType = string.IsNullOrWhiteSpace(m.CellType) ? null : m.CellType,
                ParentStationId = m.ParentMachineId?.ToString(),
                SubLineId = placement.SubLine.Id?.ToString(),
                SubLineName = placement.SubLine.Name,
                ProjectId = placement.Project.Id?.ToString(),
                ProjectName = placement.Project.Name,
                BusinessUnitId = placement.BusinessUnit.Id?.ToString(),
                BusinessUnitName = placement.BusinessUnit.Name,
                LiveState = LiveState(snap, now, staleAfter),
                TelemetryAgeSeconds = snap?.LastTelemetryAt is { } last ? Math.Max(0, Math.Round((now - last).TotalSeconds, 1)) : null,
                IncidentCount = inWindow.Count,
                ResolvedIncidentCount = ResolutionMinutes(inWindow).Count,
                MttrMinutes = MttrMinutes(inWindow),
                DowntimeMinutes = DowntimeMinutes(inWindow),
                LastIncidentAt = tickets.Count > 0 ? tickets.Max(t => t.CreatedAt) : null,
                OldestOpenTicketAt = open.Count > 0 ? open.Min(t => t.CreatedAt) : null,
                ControllerCount = controllers.Count,
                ControllersOnline = controllers.Count(c => c.IsOnline(now)),
                ControllerLastSeenAt = lastSeen == default ? null : lastSeen
            };
            FillStationKpi(dto, m, placement.Line, snap, open, now);
            result.Stations.Add(dto);
        }
        return result;
    }
}
