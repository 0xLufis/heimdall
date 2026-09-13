using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Backend.Api.Services;

public record CopiaCommitRecord(
    string RepositoryName,
    string Branch,
    string CommitHash,
    string Author,
    string Message,
    IReadOnlyList<string> ChangedPlcFiles,
    DateTimeOffset Timestamp
);

/// <summary>
/// Copia Automation Webhook Service (Git-based automation version control for PLCs & Industrial IPCs).
/// Handles HMAC-SHA256 signature verification, branch commits, diff metadata extraction,
/// and automatic version synchronization with SoftwareAssets in the database.
/// </summary>
public class CopiaIntegrationService
{
    private readonly ILogger<CopiaIntegrationService> _logger;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public CopiaIntegrationService(ILogger<CopiaIntegrationService> logger, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Validates an incoming Copia HMAC-SHA256 signature.
    /// </summary>
    public bool VerifyWebhookSignature(string payload, string signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedSignature = "sha256=" + Convert.ToHexString(hashBytes).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signatureHeader.Trim())
        );
    }

    /// <summary>
    /// Processes an incoming Copia webhook event.
    /// </summary>
    public async Task<bool> ProcessWebhookAsync(string eventType, string jsonPayload, string? signature = null, string? secret = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Copia Integration: Processing event '{EventType}'", eventType);

        if (!string.IsNullOrEmpty(signature) && !string.IsNullOrEmpty(secret))
        {
            if (!VerifyWebhookSignature(jsonPayload, signature, secret))
            {
                _logger.LogWarning("Copia Integration: Invalid HMAC signature rejected");
                return false;
            }
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;

            switch (eventType.ToLowerInvariant())
            {
                case "push":
                case "commit":
                    return await HandlePushEventAsync(root, cancellationToken);
                case "pull_request":
                    return await HandlePullRequestEventAsync(root, cancellationToken);
                default:
                    _logger.LogWarning("Copia Integration: Unhandled event type '{EventType}'", eventType);
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copia Integration: Failed to parse webhook payload");
            return false;
        }
    }

    private async Task<bool> HandlePushEventAsync(JsonElement root, CancellationToken cancellationToken)
    {
        string repoName = root.TryGetProperty("repository", out var repo) && repo.TryGetProperty("name", out var rName)
            ? rName.GetString() ?? "UnknownRepo"
            : "UnknownRepo";

        string commitHash = root.TryGetProperty("after", out var after)
            ? after.GetString() ?? Guid.NewGuid().ToString("N")[..8]
            : Guid.NewGuid().ToString("N")[..8];

        string refName = root.TryGetProperty("ref", out var refProp)
            ? refProp.GetString() ?? "refs/heads/main"
            : "refs/heads/main";

        string author = "Copia Developer";
        string message = "Automated PLC Logic Commit";
        var changedFiles = new List<string>();

        if (root.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array && commits.GetArrayLength() > 0)
        {
            var headCommit = commits[commits.GetArrayLength() - 1];
            if (headCommit.TryGetProperty("author", out var authObj) && authObj.TryGetProperty("email", out var emailProp))
            {
                author = emailProp.GetString() ?? author;
            }
            if (headCommit.TryGetProperty("message", out var msgProp))
            {
                message = msgProp.GetString() ?? message;
            }
            if (headCommit.TryGetProperty("modified", out var modFiles) && modFiles.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in modFiles.EnumerateArray())
                {
                    if (f.GetString() is { } fStr) changedFiles.Add(fStr);
                }
            }
        }

        _logger.LogInformation("Copia Push Event: Repository '{Repo}', Ref '{Ref}', Commit '{Commit}', Author '{Author}', Files: {FileCount}",
            repoName, refName, commitHash, author, changedFiles.Count);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var asset = await dbContext.SoftwareAssets.FirstOrDefaultAsync(s => s.Name == repoName, cancellationToken);
        if (asset != null)
        {
            asset.Version = commitHash;
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Copia Integration: Updated SoftwareAsset '{AssetName}' to commit '{Commit}'", asset.Name, commitHash);
        }

        return true;
    }

    private async Task<bool> HandlePullRequestEventAsync(JsonElement root, CancellationToken cancellationToken)
    {
        string action = root.TryGetProperty("action", out var act) ? act.GetString() ?? "opened" : "opened";
        _logger.LogInformation("Copia PR Event Action: {Action}", action);
        await Task.CompletedTask;
        return true;
    }
}
