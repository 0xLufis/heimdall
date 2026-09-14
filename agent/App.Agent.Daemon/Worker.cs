namespace App.Agent.Daemon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Infrastructure.Beckhoff;
using App.Agent.Daemon.Infrastructure.Opc;
using App.Agent.Daemon.Reporting.Triggers;

/// <summary>
/// Background worker driving trigger-based endpoint telemetry reporting,
/// Beckhoff TwinCAT ADS simulation, minimal OPC UA polling, and command processing.
/// </summary>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly ISystemInfoService _systemInfoService;
    private readonly ISystemInfoReporter _systemInfoReporter;
    private readonly ICommandHandler _commandHandler;
    private readonly AdsSimulationServer _adsServer;
    private readonly MinimalOpcClient _opcClient;
    private readonly TelemetryTriggerEngine _triggerEngine;

    private DateTimeOffset _lastReportedTime = DateTimeOffset.MinValue;
    private ushort _lastAdsState = AdsSimulationServer.ADSSTATE_RUN;
    private bool _forceReportRequested = false;
    private string? _forceReportReason = null;
    private readonly AutoResetEvent _wakeUpSignal = new(false);

    public AdsSimulationServer AdsServer => _adsServer;
    public MinimalOpcClient OpcClient => _opcClient;
    public TelemetryTriggerEngine TriggerEngine => _triggerEngine;

    public Worker(
        ILogger<Worker> logger,
        ISystemInfoService systemInfoService,
        ISystemInfoReporter systemInfoReporter,
        ICommandHandler commandHandler,
        AdsSimulationServer? adsServer = null,
        MinimalOpcClient? opcClient = null,
        TelemetryTriggerEngine? triggerEngine = null)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _systemInfoReporter = systemInfoReporter;
        _commandHandler = commandHandler;

        _adsServer = adsServer ?? new AdsSimulationServer();
        _opcClient = opcClient ?? new MinimalOpcClient();
        _triggerEngine = triggerEngine ?? new TelemetryTriggerEngine();
    }

    public void RequestImmediateReport(string reason = "On-demand telemetry requested")
    {
        _forceReportRequested = true;
        _forceReportReason = reason;
        _wakeUpSignal.Set();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start industrial OT simulation services
        try
        {
            _adsServer.Start();
            _opcClient.Start(pollIntervalMs: 2000);
            _logger.LogInformation("Industrial OT subsystems initialized (ADS: port {AdsPort}, OPC: {OpcEndpoint})",
                _adsServer.Port, _opcClient.EndpointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting industrial OT simulation services");
        }

        // Initial spread delay to prevent thundering herd
        await Task.Delay(Random.Shared.Next(0, 3000), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var systemInfo = _systemInfoService.GetSystemInfo();

                // Populate Industrial OT telemetry
                systemInfo.IndustrialOt = new IndustrialOtData
                {
                    AdsAmsNetId = $"{_adsServer.AmsNetId}:{_adsServer.AmsPort}",
                    AdsPort = _adsServer.Port,
                    AdsState = _adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN ? "RUN" :
                               _adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_STOP ? "STOP" :
                               $"State_{_adsServer.CurrentAdsState}",
                    AdsSymbols = new Dictionary<string, object>(_adsServer.SimulatedVariables),
                    OpcEndpoint = _opcClient.EndpointUrl,
                    OpcConnected = _opcClient.IsConnected,
                    OpcNodes = new Dictionary<string, object>(_opcClient.MonitoredNodes)
                };

                // Build trigger context
                var triggerContext = new TriggerContext
                {
                    CurrentSystemInfo = systemInfo,
                    CurrentAdsState = _adsServer.CurrentAdsState,
                    PreviousAdsState = _lastAdsState,
                    PlcVariables = _adsServer.SimulatedVariables,
                    OpcNodes = _opcClient.MonitoredNodes,
                    LastReportedTime = _lastReportedTime,
                    ForceReport = _forceReportRequested,
                    ForceReason = _forceReportReason
                };

                // Evaluate triggers to decide whether and what to report
                var triggerEvaluation = _triggerEngine.Evaluate(triggerContext);

                if (triggerEvaluation.Triggered)
                {
                    systemInfo.IndustrialOt.LastTriggerReason = triggerEvaluation.Reason;
                    systemInfo.IndustrialOt.TriggerPriority = triggerEvaluation.Priority.ToString();

                    _logger.LogInformation("Reporting triggered [{Priority}]: {Reason}",
                        triggerEvaluation.Priority, triggerEvaluation.Reason);

                    // Selective Data Slice Filtering:
                    // If hardware or software wasn't requested by any trigger, omit them to conserve bandwidth
                    if (!triggerEvaluation.SlicesToReport.IncludeHardware)
                    {
                        systemInfo.Hardware = new HardwareInfo();
                    }
                    if (!triggerEvaluation.SlicesToReport.IncludeSoftware)
                    {
                        systemInfo.Software = new SoftwareInfo();
                    }

                    // Dispatch via gRPC / spooler and process returned commands
                    var response = await _systemInfoReporter.ReportInfoAsync(systemInfo);

                    _lastReportedTime = DateTimeOffset.UtcNow;
                    _lastAdsState = _adsServer.CurrentAdsState;
                    _forceReportRequested = false;
                    _forceReportReason = null;

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
                else
                {
                    _logger.LogDebug("Trigger evaluation: No conditions met. Telemetry payload omitted to conserve bandwidth.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in worker execution loop");
            }

            // Polling cycle: 10s evaluation interval with +/- 10% jitter, wakeable on demand
            int baseDelayMs = 10000;
            int jitterMs = Random.Shared.Next(-1000, 1000);
            await Task.Delay(baseDelayMs + jitterMs, stoppingToken);
        }

        _adsServer.Stop();
        _opcClient.Stop();
    }

    public override void Dispose()
    {
        try { _adsServer?.Dispose(); } catch { }
        try { _opcClient?.Dispose(); } catch { }
        try { _wakeUpSignal?.Dispose(); } catch { }
        base.Dispose();
    }
}
