namespace App.Agent.Daemon;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SystemInfoService _systemInfoService;
    private readonly SystemInfoReporter _systemInfoReporter;
    private readonly ConfigurationService _configService;

    public Worker(
        ILogger<Worker> logger,
        SystemInfoService systemInfoService,
        SystemInfoReporter systemInfoReporter,
        ConfigurationService configService)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _systemInfoReporter = systemInfoReporter;
        _configService = configService;
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

        if (!_configService.VerifyCommandSignature(command))
        {
            _logger.LogWarning("Command of type {Type} rejected: signature verification failed.", command.Type);
            return;
        }
        
        switch (command.Type)
        {
            case "UPDATE_CONFIG":
            case "SET_MASTER_POLICY":
                _configService.UpdateConfigSigned(command.Payload, command.Signature);
                break;
            
            case "FILE_CHECK":
            case "SHELL_EXEC":
                if (!_configService.Config.AllowRemoteExecution)
                {
                    _logger.LogWarning("Remote execution command {Type} rejected: AllowRemoteExecution is disabled by master policy.", command.Type);
                    return;
                }
                _logger.LogInformation("Executing authorized diagnostic/file check: {Path}", command.Payload);
                break;

            default:
                _logger.LogWarning("Unknown command type received: {Type}", command.Type);
                break;
        }

        await Task.CompletedTask;
    }
}
