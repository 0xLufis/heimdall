namespace App.Backend.Api.Dtos;

using System;

public class DiagnosticSnapshotSummaryDto
{
    public Guid Id { get; set; }
    public Guid ClientPcId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string? MachineIdentifier { get; set; }
    public string CapturedByUserId { get; set; } = string.Empty;
    public string? CapturedByUserName { get; set; }
    public DateTimeOffset CapturedAtUtc { get; set; }
    public string PayloadHashSha256 { get; set; } = string.Empty;
    public int PayloadSizeBytes { get; set; }
}

public class DiagnosticSnapshotDetailDto : DiagnosticSnapshotSummaryDto
{
    public string SnapshotPayloadJson { get; set; } = string.Empty;
}

public class CrashTelemetryReportDto
{
    public string Hostname { get; set; } = string.Empty;
    public string MachineIdentifier { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public int ExitCode { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public long WorkingSetBytes { get; set; }
    public int ThreadCount { get; set; }
    public string? EnvironmentInfo { get; set; }
}

public class RecipeChangedEventDto
{
    public string MachineIdentifier { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string? PreviousChecksumSha256 { get; set; }
    public string ChangeType { get; set; } = "Modified";
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Actor { get; set; } = "SystemWatcher";
    public long FileSizeBytes { get; set; }
}

