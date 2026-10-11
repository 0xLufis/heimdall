namespace App.Agent.Daemon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Reporting;
using App.Contracts.Mqtt;
using App.Shared.Protos;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

/// <summary>
/// Aggregates multi-contributor component telemetry and inventory (hardware, software, drives, drivers, events, OT PLC memory),
/// serializes them to Protobuf payloads, and publishes them over MQTT to the Heimdall Backend with offline spool fallback.
/// </summary>
public class SystemInfoReporter : ISystemInfoReporter
{
    private readonly ILogger<SystemInfoReporter> _logger;
    private readonly IConfigurationService _configService;
    private readonly List<IComponentContributor> _contributors;
    private readonly ITelemetrySpooler? _spooler;
    private readonly IMqttAgentClient _mqttClient;
    private readonly ISystemInfoService? _systemInfoService;

    public IReadOnlyList<IComponentContributor> Contributors => _contributors;

    public SystemInfoReporter(
        ILogger<SystemInfoReporter> logger,
        IConfigurationService configService,
        IMqttAgentClient mqttClient,
        IEnumerable<IComponentContributor>? contributors = null,
        ITelemetrySpooler? spooler = null,
        ISystemInfoService? systemInfoService = null)
    {
        _logger = logger;
        _configService = configService;
        _mqttClient = mqttClient;
        _spooler = spooler;
        _systemInfoService = systemInfoService;

        // Initialize all base contributors
        var allContributors = new List<IComponentContributor>
        {
            new HardwareComponentContributor(),
            new SoftwareComponentContributor(),
            new PhysicalDrivesComponentContributor(),
            new DriversComponentContributor(),
            new EventsComponentContributor(),
            new LiveTelemetryComponentContributor()
        };

        if (contributors != null && contributors.Any())
        {
            foreach (var c in contributors)
            {
                if (!allContributors.Any(b => b.GetType() == c.GetType()))
                {
                    allContributors.Add(c);
                }
            }
        }

        _contributors = allContributors;
    }

    public IConfigurationService GetConfigService() => _configService;

    public async Task<SystemInfoResponse?> ReportInfoAsync(SystemInfoData data)
    {
        try
        {
            var request = new SystemInfoRequest
            {
                Hostname = data.Hostname,
                MachineIdentifier = data.MachineIdentifier,
                MacAddress = data.MacAddress,
                LastOnline = Timestamp.FromDateTimeOffset(data.LastOnline),
                DiskInfo = new DiskInfo
                {
                    TotalFreeGb = data.Disk.TotalFreeGB,
                    OsDriveFreeGb = data.Disk.OsDriveFreeGB
                }
            };

            if (data.Disk.Drives != null)
            {
                foreach (var drive in data.Disk.Drives)
                {
                    request.DiskInfo.Drives.Add(drive.Key, drive.Value);
                }
            }

            // Build inventory components dynamically through contributor pipeline
            foreach (var contributor in _contributors)
            {
                if (contributor is IMultiComponentContributor multi)
                {
                    foreach (var component in multi.CreateComponents(data))
                    {
                        if (component != null)
                        {
                            request.Components.Add(component);
                        }
                    }
                }
                else
                {
                    var component = contributor.CreateComponent(data);
                    if (component != null)
                    {
                        request.Components.Add(component);
                    }
                }
            }

            var topic = MqttTopics.SystemInfo(data.MachineIdentifier);
            bool published = await _mqttClient.PublishProtobufAsync(topic, request, qos: 1);

            if (published)
            {
                _logger.LogInformation("Successfully reported system info via MQTT to topic {Topic}", topic);

                if (_spooler != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _spooler.DrainSpoolAsync(async payload =>
                            {
                                var spooledData = JsonSerializer.Deserialize<SystemInfoData>(payload);
                                if (spooledData == null) return true;
                                var res = await ReportInfoAsync(spooledData);
                                return res?.Success == true;
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Background spool draining encountered an issue.");
                        }
                    });
                }

                return new SystemInfoResponse
                {
                    Success = true,
                    Message = $"Successfully published to {topic}"
                };
            }
            else
            {
                _logger.LogWarning("Failed to publish system info via MQTT. Spooling to local buffer.");
                await SpoolDataAsync(data);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting system info via MQTT. Spooling to local buffer.");
            await SpoolDataAsync(data);
            return null;
        }
    }

    private async Task SpoolDataAsync(SystemInfoData data)
    {
        if (_spooler != null)
        {
            try
            {
                string json = JsonSerializer.Serialize(data);
                await _spooler.SpoolPayloadAsync(json);
            }
            catch (Exception spoolEx)
            {
                _logger.LogError(spoolEx, "Failed to buffer payload into local spooler.");
            }
        }
    }

    public async Task<SystemInfoResponse?> TriggerSyncAsync()
    {
        if (_systemInfoService == null)
        {
            _logger.LogWarning("Cannot trigger sync: ISystemInfoService not available.");
            return null;
        }

        var data = _systemInfoService.GetSystemInfo();
        return await ReportInfoAsync(data);
    }
}
