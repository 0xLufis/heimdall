namespace App.Backend.Api.Dtos;

public class FleetSummaryDto
{
    public double TotalMachineHours { get; set; }
    public double AvailabilityPercentage { get; set; }
    public double AverageMtbfHours { get; set; }
    public double AverageMttrMinutes { get; set; }
    public double SlaCompliancePercentage { get; set; }
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
    public double TotalDowntimeMinutes { get; set; }
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
    public string LineId { get; set; } = string.Empty;
    public string LineName { get; set; } = string.Empty;
    public double Oee { get; set; }
    public double Availability { get; set; }
    public double Performance { get; set; }
    public double Quality { get; set; }
    public double MtbfHours { get; set; }
    public double MttrMinutes { get; set; }
    public int IncidentCount { get; set; }
}

public class PowerBiConfigDto
{
    public string EmbedUrl { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string DatasetId { get; set; } = string.Empty;
    public string WorkspaceId { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public string AuthStatus { get; set; } = string.Empty;
}
