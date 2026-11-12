namespace App.Agent.Daemon.Infrastructure.Supervisor;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class CrashDumpRecord
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

public interface IDaemonSupervisor
{
    void GenerateCrashDump(string reason, Exception? ex = null, int exitCode = 1);
    IReadOnlyList<CrashDumpRecord> GetPendingCrashDumps();
    Task<int> FlushPendingCrashTelemetryAsync(string backendUrl, HttpClient? client = null);
    bool CheckMemoryLimit();
}

public class DaemonSupervisorService : IHostedService, IDaemonSupervisor, IDisposable
{
    private readonly ConfigurationService _configService;
    private readonly ISystemInfoService? _systemInfoService;
    private readonly ILogger<DaemonSupervisorService> _logger;
    private readonly string _dumpDirectory;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public DaemonSupervisorService(
        ConfigurationService configService,
        ILogger<DaemonSupervisorService> logger,
        ISystemInfoService? systemInfoService = null)
    {
        _configService = configService;
        _logger = logger;
        _systemInfoService = systemInfoService;
        _dumpDirectory = Path.Combine(AppContext.BaseDirectory, "telemetry_spool", "crash_dumps");

        Directory.CreateDirectory(_dumpDirectory);

        // Hook unhandled domain exceptions for post-mortem diagnostics
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configService.Config.WatchdogEnabled)
        {
            _logger.LogInformation("Daemon supervisor watchdog is disabled via configuration.");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting Daemon Supervisor Watchdog. Monitoring memory limit: {Limit}MB", _configService.Config.MaxMemoryMb);
        _cts = new CancellationTokenSource();
        _monitorTask = MonitorLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
            if (_monitorTask != null)
            {
                await Task.WhenAny(_monitorTask, Task.Delay(1000, cancellationToken));
            }
        }
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        // On startup, attempt to flush any pending crash telemetry from previous unexpected termination
        await TryFlushOnStartupAsync();

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _configService.Config.HeartbeatIntervalSeconds)));
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                CheckMemoryLimit();

                // Periodically check and flush pending crash dumps if backend is reachable
                if (Directory.Exists(_dumpDirectory) && Directory.GetFiles(_dumpDirectory, "*.json").Length > 0)
                {
                    await FlushPendingCrashTelemetryAsync(_configService.Config.BackendUrl);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Supervisor background check cycle encountered a non-fatal error");
            }
        }
    }

    public bool CheckMemoryLimit()
    {
        using var process = Process.GetCurrentProcess();
        long workingSetBytes = process.WorkingSet64;
        long limitBytes = (long)_configService.Config.MaxMemoryMb * 1024 * 1024;

        if (workingSetBytes > limitBytes)
        {
            _logger.LogWarning(
                "Memory consumption ({WorkingSetMB}MB) exceeded policy limit ({LimitMB}MB). Triggering gen-2 garbage collection.",
                workingSetBytes / (1024 * 1024),
                _configService.Config.MaxMemoryMb);

            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            return false;
        }

        return true;
    }

    public void GenerateCrashDump(string reason, Exception? ex = null, int exitCode = 1)
    {
        try
        {
            Directory.CreateDirectory(_dumpDirectory);
            using var process = Process.GetCurrentProcess();

            var sysInfo = _systemInfoService?.GetSystemInfo();
            string machineId = !string.IsNullOrEmpty(sysInfo?.MachineIdentifier) ? sysInfo!.MachineIdentifier : Environment.MachineName;
            string hostname = !string.IsNullOrEmpty(sysInfo?.Hostname) ? sysInfo!.Hostname : Environment.MachineName;

            var record = new CrashDumpRecord
            {
                Hostname = hostname,
                MachineIdentifier = machineId,
                TimestampUtc = DateTimeOffset.UtcNow,
                ExitCode = exitCode,
                Reason = reason,
                ExceptionType = ex?.GetType().FullName,
                StackTrace = ex?.ToString(),
                WorkingSetBytes = process.WorkingSet64,
                ThreadCount = process.Threads.Count,
                EnvironmentInfo = $"{Environment.OSVersion}; .NET {Environment.Version}; CPU={Environment.ProcessorCount}"
            };

            string filename = $"dump_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{process.Id}.json";
            string path = Path.Combine(_dumpDirectory, filename);

            string json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
            // Durable: flushed to disk and renamed into place, so a dump written just before the
            // process (or the machine) dies is complete or absent, never torn.
            App.Agent.Daemon.Infrastructure.Persistence.AtomicFile.Write(path, System.Text.Encoding.UTF8.GetBytes(json), keepPreviousAsBackup: false);

            _logger.LogError("Generated post-mortem diagnostic dump: {Path} (Reason: {Reason})", path, reason);
        }
        catch (Exception dumpEx)
        {
            _logger.LogCritical(dumpEx, "Failed to write diagnostic post-mortem dump");
        }
    }

    public IReadOnlyList<CrashDumpRecord> GetPendingCrashDumps()
    {
        var list = new List<CrashDumpRecord>();
        if (!Directory.Exists(_dumpDirectory)) return list;

        foreach (var file in Directory.GetFiles(_dumpDirectory, "*.json"))
        {
            try
            {
                string json = File.ReadAllText(file);
                var dump = JsonSerializer.Deserialize<CrashDumpRecord>(json);
                if (dump != null) list.Add(dump);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse crash dump file {File}", file);
            }
        }
        return list;
    }

    public async Task<int> FlushPendingCrashTelemetryAsync(string backendUrl, HttpClient? client = null)
    {
        if (!Directory.Exists(_dumpDirectory)) return 0;
        var files = Directory.GetFiles(_dumpDirectory, "*.json");
        if (files.Length == 0) return 0;

        bool createdClient = false;
        if (client == null)
        {
            client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            createdClient = true;
        }

        int flushed = 0;
        try
        {
            string endpoint = $"{backendUrl.TrimEnd('/')}/api/v1/DiagnosticSnapshot/crash-telemetry";

            foreach (var file in files)
            {
                try
                {
                    string json = await File.ReadAllTextAsync(file);
                    var dump = JsonSerializer.Deserialize<CrashDumpRecord>(json);
                    if (dump == null) continue;

                    var response = await client.PostAsJsonAsync(endpoint, dump);
                    if (response.IsSuccessStatusCode)
                    {
                        File.Delete(file);
                        flushed++;
                        _logger.LogInformation("Successfully delivered crash telemetry report {File} to backend", Path.GetFileName(file));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Transient failure sending crash telemetry: {Message}", ex.Message);
                    break; // Stop iteration if backend is unreachable
                }
            }
        }
        finally
        {
            if (createdClient) client.Dispose();
        }

        return flushed;
    }

    private async Task TryFlushOnStartupAsync()
    {
        try
        {
            await Task.Delay(2000); // Allow network sockets to initialize
            await FlushPendingCrashTelemetryAsync(_configService.Config.BackendUrl);
        }
        catch
        {
            // Silent fallback on startup
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        GenerateCrashDump($"Unhandled Domain Exception (IsTerminating={e.IsTerminating})", ex, exitCode: 139);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        GenerateCrashDump("Unobserved Task Exception", e.Exception, exitCode: 1);
        e.SetObserved();
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _cts?.Dispose();
    }
}
