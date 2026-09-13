namespace App.Agent.Daemon;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using App.Agent.Daemon.Interfaces;

/// <summary>
/// Background worker driving the endpoint telemetry reporting and command processing loop.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISystemInfoService _systemInfoService;
    private readonly ISystemInfoReporter _systemInfoReporter;
    private readonly ICommandHandler _commandHandler;

    public Worker(
        ILogger<Worker> logger,
        ISystemInfoService systemInfoService,
        ISystemInfoReporter systemInfoReporter,
        ICommandHandler commandHandler)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _systemInfoReporter = systemInfoReporter;
        _commandHandler = commandHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial spread delay to prevent thundering herd
        await Task.Delay(Random.Shared.Next(0, 10000), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    var systemInfo = _systemInfoService.GetSystemInfo();
                    _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

                    // Report via gRPC and fetch queued server commands
                    var response = await _systemInfoReporter.ReportInfoAsync(systemInfo);

                    if (response != null && response.Commands.Any())
                    {
                        foreach (var command in response.Commands)
                        {
                            var result = await _commandHandler.HandleCommandAsync(command);
                            _logger.LogInformation("Command execution result: {CommandType} -> Success={Success}, Message={Message}",
                                command.Type, result.Success, result.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker execution loop");
            }

            // 60-second polling interval with +/- 10% jitter
            int baseDelayMs = 60000;
            int jitterMs = Random.Shared.Next(-6000, 6000);
            await Task.Delay(baseDelayMs + jitterMs, stoppingToken);
        }
    }
}
