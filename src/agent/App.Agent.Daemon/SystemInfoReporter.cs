namespace App.Agent.Daemon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
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

    private readonly App.Agent.Daemon.Lifecycle.IAgentLifecycle? _lifecycle;
    private readonly TelemetrySanityFilter _sanity;

    /// <summary>Client-side sanity checks and their counters.</summary>
    public TelemetrySanityFilter SanityFilter => _sanity;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _contributorFailures = new();
    private int _draining;

    public IReadOnlyList<IComponentContributor> Contributors => _contributors;

    /// <summary>Exceptions thrown by each contributor since start (a failing contributor is skipped, the report still goes out).</summary>
    public IReadOnlyDictionary<string, long> ContributorFailures => _contributorFailures;

    public SystemInfoReporter(
        ILogger<SystemInfoReporter> logger,
        IConfigurationService configService,
        IMqttAgentClient mqttClient,
        IEnumerable<IComponentContributor>? contributors = null,
        ITelemetrySpooler? spooler = null,
        ISystemInfoService? systemInfoService = null,
        App.Agent.Daemon.Lifecycle.IAgentLifecycle? lifecycle = null,
        TelemetrySanityFilter? sanityFilter = null)
    {
        _lifecycle = lifecycle;
        _sanity = sanityFilter ?? new TelemetrySanityFilter();
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

    public Task<SystemInfoResponse?> ReportInfoAsync(SystemInfoData data) => ReportCoreAsync(data, spoolOnFailure: true);

    private async Task<SystemInfoResponse?> ReportCoreAsync(SystemInfoData data, bool spoolOnFailure)
    {
        // Sanity checks before anything is sent or spooled (live reports also get the monotonic time rule).
        var sanity = _sanity.Check(data, DateTimeOffset.UtcNow, liveReport: spoolOnFailure);
        if (sanity.Issues.Count > 0)
        {
            _logger.LogWarning("Report sanity check {Outcome}: {Issues}", sanity.Accepted ? "corrected values" : "rejected the report",
                string.Join(", ", sanity.Issues.Distinct()));
        }
        if (!sanity.Accepted)
        {
            // Not spooled either: it would be rejected again on every drain.
            return spoolOnFailure ? null : new SystemInfoResponse { Success = true, Message = "Rejected by sanity checks; dropped from spool." };
        }

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

            // Build inventory components dynamically through contributor pipeline. Each contributor is
            // isolated: one that throws (a plugin, an extension with unserializable data) is skipped
            // and counted instead of failing - and spooling - the whole report on every cycle.
            foreach (var contributor in _contributors)
            {
                try
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
                catch (Exception ex)
                {
                    long failures = _contributorFailures.AddOrUpdate(contributor.GetType().Name, 1, (_, n) => n + 1);
                    if (failures == 1 || failures % 100 == 0)
                    {
                        _logger.LogWarning(ex, "Component contributor {Contributor} failed ({Failures} failures since start); report sent without it.",
                            contributor.GetType().Name, failures);
                    }
                }
            }

            int maxBytes = Math.Max(16_384, _configService.Config.MaxReportPayloadBytes);
            if (!_sanity.EnforceSize(request, maxBytes, out var dropped))
            {
                _logger.LogError("System info report exceeds {Max} bytes even without inventory components; not sent.", maxBytes);
                return spoolOnFailure ? null : new SystemInfoResponse { Success = true, Message = "Oversized; dropped from spool." };
            }
            if (dropped.Count > 0)
            {
                _logger.LogWarning("System info report exceeded {Max} bytes; left out components: {Components}", maxBytes, string.Join(", ", dropped));
            }

            var topic = MqttTopics.SystemInfo(data.MachineIdentifier);
            bool published = await _mqttClient.PublishProtobufAsync(topic, request, qos: 1);

            if (published)
            {
                _logger.LogInformation("Successfully reported system info via MQTT to topic {Topic}", topic);
                _lifecycle?.MarkReportDelivered();

                // Drain the spool after a live report got through - but not from inside a drain, and
                // a spooled record that fails again is NOT spooled a second time (it stays queued).
                if (_spooler != null && spoolOnFailure && Interlocked.CompareExchange(ref _draining, 1, 0) == 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _spooler.DrainSpoolAsync(async payload =>
                            {
                                var spooledData = JsonSerializer.Deserialize<SystemInfoData>(payload);
                                if (spooledData == null) return true;
                                var res = await ReportCoreAsync(spooledData, spoolOnFailure: false);
                                return res?.Success == true;
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Background spool draining encountered an issue.");
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _draining, 0);
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
                _logger.LogWarning("Failed to publish system info via MQTT.{Action}", spoolOnFailure ? " Spooling to local buffer." : string.Empty);
                if (spoolOnFailure) await SpoolDataAsync(data);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting system info via MQTT.{Action}", spoolOnFailure ? " Spooling to local buffer." : string.Empty);
            if (spoolOnFailure) await SpoolDataAsync(data);
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
