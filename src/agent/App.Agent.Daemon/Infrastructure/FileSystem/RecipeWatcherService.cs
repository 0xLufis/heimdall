namespace App.Agent.Daemon.Infrastructure.FileSystem;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class RecipeChangedEvent
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

public interface IRecipeWatcherService
{
    Task<string> ComputeChecksumSha256Async(string filePath);
    Task DispatchRecipeChangeEventAsync(RecipeChangedEvent changeEvent);
    IReadOnlyDictionary<string, string> GetBaselineChecksums();
}

public class RecipeWatcherService : IHostedService, IRecipeWatcherService, IDisposable
{
    private readonly ConfigurationService _configService;
    private readonly ISystemInfoService? _systemInfoService;
    private readonly ILogger<RecipeWatcherService> _logger;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, string> _checksumCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _debounceTimers = new();
    private HttpClient? _httpClient;

    public RecipeWatcherService(
        ConfigurationService configService,
        ILogger<RecipeWatcherService> logger,
        ISystemInfoService? systemInfoService = null)
    {
        _configService = configService;
        _logger = logger;
        _systemInfoService = systemInfoService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var directories = _configService.Config.RecipeWatchDirectories;
        if (directories == null || directories.Count == 0)
        {
            _logger.LogInformation("No recipe watch directories configured.");
            return Task.CompletedTask;
        }

        foreach (var dir in directories)
        {
            try
            {
                string targetPath = Path.IsPathRooted(dir)
                    ? dir
                    : Path.Combine(AppContext.BaseDirectory, dir);

                if (!Directory.Exists(targetPath))
                {
                    Directory.CreateDirectory(targetPath);
                }

                // Initial baseline snapshot
                ScanBaseline(targetPath);

                var watcher = new FileSystemWatcher(targetPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileCreated;
                watcher.Deleted += OnFileDeleted;
                watcher.Renamed += OnFileRenamed;

                _watchers.Add(watcher);
                _logger.LogInformation("Attached real-time recipe change watcher to: {Path}", targetPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize recipe watcher on directory: {Dir}", dir);
            }
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, string> GetBaselineChecksums() => _checksumCache;

    private void ScanBaseline(string path)
    {
        try
        {
            foreach (var file in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
            {
                string hash = ComputeFileSha256(file);
                if (!string.IsNullOrEmpty(hash))
                {
                    _checksumCache[file] = hash;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Baseline scan encountered a non-fatal error for path {Path}", path);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e) => HandleEventDebounced(e.FullPath, "Modified");
    private void OnFileCreated(object sender, FileSystemEventArgs e) => HandleEventDebounced(e.FullPath, "Created");
    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _checksumCache.TryRemove(e.FullPath, out var previousHash);
        string machineId = _systemInfoService?.GetSystemInfo()?.MachineIdentifier ?? Environment.MachineName;

        _ = DispatchRecipeChangeEventAsync(new RecipeChangedEvent
        {
            MachineIdentifier = machineId,
            FilePath = e.FullPath,
            FileName = Path.GetFileName(e.FullPath),
            ChecksumSha256 = "DELETED",
            PreviousChecksumSha256 = previousHash,
            ChangeType = "Deleted",
            TimestampUtc = DateTimeOffset.UtcNow,
            FileSizeBytes = 0
        });
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _checksumCache.TryRemove(e.OldFullPath, out var oldHash);
        HandleEventDebounced(e.FullPath, "Renamed", oldHash);
    }

    private void HandleEventDebounced(string fullPath, string changeType, string? knownPreviousHash = null)
    {
        // 500ms debounce to let file writing settle
        var now = DateTime.UtcNow;
        if (_debounceTimers.TryGetValue(fullPath, out var lastTime) && (now - lastTime).TotalMilliseconds < 500)
        {
            return;
        }
        _debounceTimers[fullPath] = now;

        _ = Task.Run(async () =>
        {
            await Task.Delay(300); // Wait for OS write buffer
            try
            {
                if (!File.Exists(fullPath)) return;

                string newHash = await ComputeChecksumSha256Async(fullPath);
                string? prevHash = knownPreviousHash ?? (_checksumCache.TryGetValue(fullPath, out var h) ? h : null);

                // Detect actual content drift (checksum difference)
                if (newHash == prevHash && changeType == "Modified")
                {
                    return; // No real content change
                }

                _checksumCache[fullPath] = newHash;

                long fileSize = new FileInfo(fullPath).Length;
                string machineId = _systemInfoService?.GetSystemInfo()?.MachineIdentifier ?? Environment.MachineName;

                var evt = new RecipeChangedEvent
                {
                    MachineIdentifier = machineId,
                    FilePath = fullPath,
                    FileName = Path.GetFileName(fullPath),
                    ChecksumSha256 = newHash,
                    PreviousChecksumSha256 = prevHash,
                    ChangeType = changeType,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    FileSizeBytes = fileSize
                };

                await DispatchRecipeChangeEventAsync(evt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process recipe file change event for {Path}", fullPath);
            }
        });
    }

    public async Task<string> ComputeChecksumSha256Async(string filePath)
    {
        if (!File.Exists(filePath)) return string.Empty;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sha = SHA256.Create();
                var hashBytes = await sha.ComputeHashAsync(stream);
                return Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
        }
        return string.Empty;
    }

    private string ComputeFileSha256(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(stream);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task DispatchRecipeChangeEventAsync(RecipeChangedEvent changeEvent)
    {
        _logger.LogInformation(
            "Recipe change event detected: {FileName} ({Type}), Checksum: {Checksum}",
            changeEvent.FileName, changeEvent.ChangeType, changeEvent.ChecksumSha256);

        if (_httpClient == null || string.IsNullOrWhiteSpace(_configService.Config.BackendUrl)) return;

        try
        {
            string url = $"{_configService.Config.BackendUrl.TrimEnd('/')}/api/v1/DiagnosticSnapshot/recipe-changed";
            var resp = await _httpClient.PostAsJsonAsync(url, changeEvent);
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully dispatched recipe change event to backend");
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to dispatch recipe change event to backend: {Message}", ex.Message);
        }
    }

    public void Dispose()
    {
        foreach (var w in _watchers)
        {
            w.Dispose();
        }
        _watchers.Clear();
        _httpClient?.Dispose();
    }
}
