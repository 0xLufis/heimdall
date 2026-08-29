using System.Text.Json;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Backend.Api.Services;

/// <summary>
/// Stub service for parsing Copia Webhooks (Git-based automation version control for PLCs).
/// Extracts repository events, commits, and links software versions to SoftwareAssets in the database.
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
    /// Processes an incoming Copia webhook event.
    /// </summary>
    public async Task<bool> ProcessWebhookAsync(string eventType, string jsonPayload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Copia Integration: Processing event '{EventType}'", eventType);

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

        _logger.LogInformation("Copia Push Event: Repository '{Repo}', Ref '{Ref}', Commit '{Commit}'", repoName, refName, commitHash);

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
