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
    private readonly IAdsSimulationServer _adsServer;
    private readonly IMinimalOpcServer _opcServer;
    private readonly IMinimalOpcClient _opcClient;
    private readonly ITelemetryTriggerEngine _triggerEngine;
    private readonly App.Contracts.Configuration.AgentFeatureFlags _featureFlags;
    private readonly IAdsMemoryReporter? _adsMemoryReporter;
    private readonly IMqttAgentClient? _mqttClient;

    private DateTimeOffset _lastReportedTime = DateTimeOffset.MinValue;
    private ushort _lastAdsState = AdsSimulationServer.ADSSTATE_RUN;
    private bool _forceReportRequested = false;
    private string? _forceReportReason = null;
    private readonly AutoResetEvent _wakeUpSignal = new(false);

    public IAdsSimulationServer AdsServer => _adsServer;
    public IMinimalOpcServer OpcServer => _opcServer;
    public IMinimalOpcClient OpcClient => _opcClient;
    public ITelemetryTriggerEngine TriggerEngine => _triggerEngine;
    public App.Contracts.Configuration.AgentFeatureFlags FeatureFlags => _featureFlags;
    public IAdsMemoryReporter? AdsMemoryReporter => _adsMemoryReporter;
    public IMqttAgentClient? MqttClient => _mqttClient;

    public Worker(
        ILogger<Worker> logger,
        ISystemInfoService systemInfoService,
        ISystemInfoReporter systemInfoReporter,
        ICommandHandler commandHandler,
        IAdsSimulationServer? adsServer = null,
        IMinimalOpcServer? opcServer = null,
        IMinimalOpcClient? opcClient = null,
        ITelemetryTriggerEngine? triggerEngine = null,
        App.Contracts.Configuration.AgentFeatureFlags? featureFlags = null,
        IAdsMemoryReporter? adsMemoryReporter = null,
        IMqttAgentClient? mqttClient = null)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _systemInfoReporter = systemInfoReporter;
        _commandHandler = commandHandler;

        _adsServer = adsServer ?? new AdsSimulationServer();
        _opcServer = opcServer ?? new MinimalOpcServer();
        _opcClient = opcClient ?? new MinimalOpcClient();
        _triggerEngine = triggerEngine ?? new TelemetryTriggerEngine();
        _featureFlags = featureFlags ?? App.Contracts.Configuration.AgentFeatureFlags.FromEnvironment();
        _adsMemoryReporter = adsMemoryReporter;
        _mqttClient = mqttClient;
    }

    public void RequestImmediateReport(string reason = "On-demand telemetry requested")
    {
        _forceReportRequested = true;
        _forceReportReason = reason;
        _wakeUpSignal.Set();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Start industrial OT simulation services only when development features are enabled
        if (_featureFlags.EnableDevFeatures)
        {
            try
            {
                _adsServer.Start();
                _opcServer.Start();
                _logger.LogInformation("Industrial OT mock simulation servers initialized in Development mode (ADS: port {AdsPort}, OPC Server: port {OpcPort})",
                    _adsServer.Port, _opcServer.Port);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting industrial OT simulation services");
            }
        }
        else
        {
            _logger.LogInformation("Production mode active: Industrial OT mock simulation servers (TwinCAT ADS, Minimal OPC UA) are disabled (EnableDevFeatures = false).");
        }

        try
        {
            _opcClient.Start(pollIntervalMs: 2000);
            _logger.LogInformation("Industrial OT OPC Client initialized (Endpoint: {OpcEndpoint})", _opcClient.EndpointUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting OPC Client");
        }

        if (_mqttClient != null)
        {
            try
            {
                var initialSysInfo = _systemInfoService.GetSystemInfo();
                await _mqttClient.EnsureConnectedAsync(stoppingToken);
                await _mqttClient.SubscribeToCommandsAsync(initialSysInfo.MachineIdentifier, async command =>
                {
                    var result = await _commandHandler.HandleCommandAsync(command);
                    _logger.LogInformation("Async MQTT command handled: {CommandType} -> Success={Success}, Message={Message}",
                        command.Type, result.Success, result.Message);
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Initial MQTT connection/command subscription deferred to reporter loop.");
            }
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
                    AdsAmsNetId = _featureFlags.EnableDevFeatures ? $"{_adsServer.AmsNetId}:{_adsServer.AmsPort}" : string.Empty,
                    AdsPort = _featureFlags.EnableDevFeatures ? _adsServer.Port : 0,
                    AdsState = _featureFlags.EnableDevFeatures ? (_adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN ? "RUN" :
                               _adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_STOP ? "STOP" :
                               $"State_{_adsServer.CurrentAdsState}") : "DISABLED",
                    AdsSymbols = _featureFlags.EnableDevFeatures ? new Dictionary<string, object>(_adsServer.SimulatedVariables) : new Dictionary<string, object>(),
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

                    // Dispatch ADS PLC Memory telemetry via Protobuf over MQTT
                    if (_adsMemoryReporter != null && _featureFlags.EnableDevFeatures)
                    {
                        await _adsMemoryReporter.ReportPlcMemoryAsync(systemInfo.MachineIdentifier, stoppingToken);
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

        if (_featureFlags.EnableDevFeatures)
        {
            _adsServer.Stop();
            _opcServer.Stop();
        }
        _opcClient.Stop();
    }

    public override void Dispose()
    {
        try { _adsServer?.Dispose(); } catch { }
        try { _opcServer?.Dispose(); } catch { }
        try { _opcClient?.Dispose(); } catch { }
        try { _wakeUpSignal?.Dispose(); } catch { }
        base.Dispose();
    }
}
