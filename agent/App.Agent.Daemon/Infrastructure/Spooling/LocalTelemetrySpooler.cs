using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace App.Agent.Daemon.Infrastructure.Spooling;

/// <summary>
/// Offline local telemetry spooler buffering telemetry payloads when disconnected from Heimdall backend.
/// Automatically enforces storage quotas with FIFO eviction (Guideline 21, 22, 23).
/// </summary>
public class LocalTelemetrySpooler
{
    private readonly ILogger<LocalTelemetrySpooler> _logger;
    private readonly ConfigurationService _configService;
    private readonly string _spoolDir;
    private readonly object _lock = new();

    public LocalTelemetrySpooler(ILogger<LocalTelemetrySpooler> logger, ConfigurationService configService, string? spoolDirectory = null)
    {
        _logger = logger;
        _configService = configService;
        _spoolDir = spoolDirectory ?? Path.Combine(AppContext.BaseDirectory, "telemetry_spool");

        try
        {
            if (!Directory.Exists(_spoolDir))
            {
                Directory.CreateDirectory(_spoolDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize spool directory at {Path}", _spoolDir);
        }
    }

    /// <summary>
    /// Spools a raw telemetry or system info JSON payload to local disk.
    /// </summary>
    public async Task SpoolPayloadAsync(string payloadJson)
    {
        lock (_lock)
        {
            EnforceQuota();
        }

        string fileName = $"spool_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}.json";
        string filePath = Path.Combine(_spoolDir, fileName);

        try
        {
            await File.WriteAllTextAsync(filePath, payloadJson, Encoding.UTF8);
            _logger.LogInformation("Spooled telemetry payload to {Path} ({Bytes} bytes)", fileName, payloadJson.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write spooled telemetry file to {Path}", filePath);
        }
    }

    /// <summary>
    /// Drains spooled payloads in chronological order and sends them via the provided send function.
    /// </summary>
    public async Task<int> DrainSpoolAsync(Func<string, Task<bool>> sendFunction)
    {
        if (!Directory.Exists(_spoolDir)) return 0;

        string[] files;
        lock (_lock)
        {
            files = Directory.GetFiles(_spoolDir, "spool_*.json")
                .OrderBy(f => f)
                .ToArray();
        }

        if (files.Length == 0) return 0;

        _logger.LogInformation("Attempting to drain {Count} spooled telemetry entries", files.Length);
        int successCount = 0;

        foreach (var file in files)
        {
            try
            {
                string payload = await File.ReadAllTextAsync(file, Encoding.UTF8);
                bool sent = await sendFunction(payload);
                if (sent)
                {
                    File.Delete(file);
                    successCount++;
                }
                else
                {
                    _logger.LogWarning("Downstream backend rejected spooled payload {File}. Halting drain.", Path.GetFileName(file));
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while draining spooled file {File}", file);
                break;
            }
        }

        if (successCount > 0)
        {
            _logger.LogInformation("Successfully drained and removed {Count} spooled entries", successCount);
        }

        return successCount;
    }

    /// <summary>
    /// Enforces disk quota by deleting the oldest files until disk usage is below quota.
    /// </summary>
    private void EnforceQuota()
    {
        try
        {
            if (!Directory.Exists(_spoolDir)) return;

            long maxBytes = (long)_configService.Config.MaxSpoolDiskMb * 1024 * 1024;
            var dirInfo = new DirectoryInfo(_spoolDir);
            var files = dirInfo.GetFiles("spool_*.json").OrderBy(f => f.CreationTimeUtc).ToList();

            long totalBytes = files.Sum(f => f.Length);
            while (totalBytes > maxBytes && files.Count > 0)
            {
                var oldest = files[0];
                _logger.LogWarning("Spool quota exceeded ({Used} / {Max} bytes). Evicting oldest spool file {File}", totalBytes, maxBytes, oldest.Name);
                totalBytes -= oldest.Length;
                oldest.Delete();
                files.RemoveAt(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enforce spool disk quota");
        }
    }
}
