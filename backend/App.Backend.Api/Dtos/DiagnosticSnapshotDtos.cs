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
