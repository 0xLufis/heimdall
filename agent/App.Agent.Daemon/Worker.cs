namespace App.Agent.Daemon;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SystemInfoService _systemInfoService;
    private readonly SystemInfoReporter _systemInfoReporter;

    public Worker(ILogger<Worker> logger, SystemInfoService systemInfoService, SystemInfoReporter systemInfoReporter)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _systemInfoReporter = systemInfoReporter;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                var systemInfo = _systemInfoService.GetSystemInfo();
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                _logger.LogInformation("System Info: {Hostname}, OS: {OsVersion}", systemInfo.Hostname, systemInfo.SoftwareConfig.OsVersion);

                // Report via gRPC
                await _systemInfoReporter.ReportInfoAsync(systemInfo);
            }
            await Task.Delay(5000, stoppingToken);
        }
    }
}
