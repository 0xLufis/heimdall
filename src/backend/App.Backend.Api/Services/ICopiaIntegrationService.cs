namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public record CopiaCommitRecord(
    string RepositoryName,
    string Branch,
    string CommitHash,
    string Author,
    string Message,
    IReadOnlyList<string> ChangedPlcFiles,
    DateTimeOffset Timestamp
);

public enum CloudSyncStatus
{
    PendingUserAuthorization,
    Syncing,
    Completed,
    Failed
}

public record PendingCloudSyncRecord(
    string SyncId,
    string DeviceId,
    string LocalRepositoryPath,
    string Branch,
    string LocalCommitHash,
    string ServiceUserAuthor,
    IReadOnlyList<string> ModifiedFiles,
    DateTimeOffset CreatedAt,
    string NotificationMessage,
    CloudSyncStatus Status,
    string? SyncedByUserEmail = null,
    DateTimeOffset? SyncedAt = null,
    string? CloudCommitHash = null,
    string? ErrorMessage = null
);

public record LocalPlcTrackingResult(
    bool Success,
    string SyncId,
    string DeviceId,
    string LocalCommitHash,
    string ServiceUserAuthor,
    string NotificationMessage,
    IReadOnlyList<string> ModifiedFiles,
    DateTimeOffset Timestamp,
    string? Error = null
);

public record CopiaCloudSyncResult(
    bool Success,
    string SyncId,
    string LocalCommitHash,
    string CloudCommitHash,
    string UserEmail,
    DateTimeOffset SyncedAt,
    string? Error = null
);

public record TwinCatExtractionResult(
    bool Success,
    string DeviceId,
    string TargetBranch,
    string LocalCommitHash,
    string SyncId,
    IReadOnlyList<string> ExtractedFiles,
    IReadOnlyList<string> IgnoredVolatileFiles,
    string NotificationMessage,
    DateTimeOffset Timestamp,
    string? Error = null
);

/// <summary>
/// Interface for Copia Automation Webhook Service and TwinCAT Integration.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// Enforces EULA compliance: Local tracking uses 'heimdall-probe' service identity,
/// while Copia Cloud sync requires user-specific API keys to prevent user pooling violations.
/// </summary>
public interface ICopiaIntegrationService
{
    // Webhook processing & HMAC-SHA256 signature verification
    bool VerifyWebhookSignature(string payload, string signatureHeader, string secret);
    Task<bool> ProcessWebhookAsync(string eventType, string jsonPayload, string? signature = null, string? secret = null, CancellationToken cancellationToken = default);

    // Event debouncing
    bool ShouldProcessPlcEvent(string plcAmsNetId, string eventType, TimeSpan? debounceWindow = null);

    // Stage 1: Local Git tracking with 'heimdall-probe' service identity (strictly on-premise, no Copia Cloud interaction)
    Task<LocalPlcTrackingResult> TrackLocalPlcChangeAsync(
        string deviceId,
        string repositoryPath,
        string branch,
        IReadOnlyList<string> modifiedFiles,
        string? commitMessage = null,
        CancellationToken cancellationToken = default);

    // Pending sync notifications & queue management
    IReadOnlyList<PendingCloudSyncRecord> GetPendingCloudSyncs();
    PendingCloudSyncRecord? GetPendingCloudSyncById(string syncId);

    // Stage 2: User-authenticated sync to Copia Cloud using user-specific API key (enforces EULA anti-pooling compliance)
    Task<CopiaCloudSyncResult> SyncLocalRepoToCopiaCloudAsync(
        string syncId,
        string userApiKey,
        string userEmail,
        string? userDisplayName = null,
        CancellationToken cancellationToken = default);

    // TwinCAT CurrentConfig.tnzip project archive extraction & controller branch rebase (autocommiting flow)
    Task<TwinCatExtractionResult> ExtractAndRebasePlcConfigAsync(
        string deviceId,
        System.IO.Stream archiveStream,
        string? targetBranch = null,
        CancellationToken cancellationToken = default);

    // DeviceLink backup trigger: Enforces user API key or delegates to local 'heimdall-probe' tracking
    Task<bool> TriggerDeviceLinkBackupAsync(
        string deviceId,
        string reason,
        string? userApiKey = null,
        string? userEmail = null,
        CancellationToken cancellationToken = default);
}
