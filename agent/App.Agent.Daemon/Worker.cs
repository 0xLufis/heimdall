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
        // Initial random delay to spread out startup reports
        await Task.Delay(Random.Shared.Next(0, 10000), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var systemInfo = _systemInfoService.GetSystemInfo();
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                    // Report via gRPC and get commands
                    var response = await _systemInfoReporter.ReportInfoAsync(systemInfo);
                    
                    if (response != null && response.Commands.Any())
                    {
                        foreach (var command in response.Commands)
                        {
                            await HandleCommandAsync(command);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker loop");
            }

            // 60 seconds interval with +/- 10% jitter
            int baseDelayMs = 60000;
            int jitterMs = Random.Shared.Next(-6000, 6000);
            await Task.Delay(baseDelayMs + jitterMs, stoppingToken);
        }
    }

    private async Task HandleCommandAsync(App.Shared.Protos.ServerCommand command)
    {
        _logger.LogInformation("Received command from server: {Type}", command.Type);
        
        switch (command.Type)
        {
            case "UPDATE_CONFIG":
                var configService = _systemInfoReporter.GetConfigService();
                configService.UpdateConfigSigned(command.Payload, command.Signature);
                break;
            
            case "FILE_CHECK":
                // Logic to add a file to the next report
                // For now just log it, but we could add it to a list in SystemInfoService
                _logger.LogInformation("Server requested file check: {Path}", command.Payload);
                break;
        }
    }
}
