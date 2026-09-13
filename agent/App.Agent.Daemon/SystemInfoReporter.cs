namespace App.Agent.Daemon;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using App.Agent.Daemon.Interfaces;
using App.Agent.Daemon.Reporting;
using App.Shared.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;

public class SystemInfoReporter : ISystemInfoReporter
{
    private readonly ILogger<SystemInfoReporter> _logger;
    private readonly IConfigurationService _configService;
    private readonly List<IComponentContributor> _contributors;
    private readonly ITelemetrySpooler? _spooler;

    private SystemInfoCollector.SystemInfoCollectorClient? _client;
    private readonly ISystemInfoService? _systemInfoService;
    private string? _lastBackendUrl;
    private string? _lastAuthType;

    public SystemInfoReporter(
        ILogger<SystemInfoReporter> logger,
        IConfigurationService configService,
        IEnumerable<IComponentContributor>? contributors = null,
        ITelemetrySpooler? spooler = null,
        ISystemInfoService? systemInfoService = null)
    {
        _logger = logger;
        _configService = configService;
        _spooler = spooler;
        _systemInfoService = systemInfoService;

        // Register default contributors if not injected
        if (contributors != null && contributors.Any())
        {
            _contributors = contributors.ToList();
        }
        else
        {
            _contributors = new List<IComponentContributor>
            {
                new HardwareComponentContributor(),
                new SoftwareComponentContributor(),
                new PhysicalDrivesComponentContributor(),
                new DriversComponentContributor(),
                new EventsComponentContributor()
            };
        }
    }

    private SystemInfoCollector.SystemInfoCollectorClient GetClient()
    {
        var config = _configService.Config;
        var currentUrl = config.BackendUrl;
        var currentAuth = config.AuthType;

        if (_client == null || _lastBackendUrl != currentUrl || _lastAuthType != currentAuth)
        {
            _logger.LogInformation("Creating gRPC client for {Url} with Auth={Auth}", currentUrl, currentAuth);

            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true
            };

            var parsedAuth = ParseAuthMode(currentAuth);
            if (parsedAuth == AuthMode.HeimdallCert || parsedAuth == AuthMode.UserCert)
            {
                var clientCerts = new X509CertificateCollection();
                if (!string.IsNullOrEmpty(config.ClientCertificatePath))
                {
                    try
                    {
                        var cert = X509CertificateLoader.LoadCertificateFromFile(config.ClientCertificatePath);
                        clientCerts.Add(cert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load client certificate from {Path}", config.ClientCertificatePath);
                    }
                }
                else if (parsedAuth == AuthMode.UserCert && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);
                    var certs = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, true);
                    if (certs.Count > 0)
                    {
                        clientCerts.Add(certs[0]);
                        _logger.LogInformation("Loaded certificate from Windows Machine Store: {Subject}", certs[0].Subject);
                    }
                }

                if (clientCerts.Count > 0)
                {
                    handler.SslOptions.ClientCertificates = clientCerts;
                }
            }

            var httpClient = new HttpClient(handler)
            {
                DefaultRequestVersion = System.Net.HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
            };

            var channel = GrpcChannel.ForAddress(currentUrl, new GrpcChannelOptions
            {
                HttpClient = httpClient
            });
            _client = new SystemInfoCollector.SystemInfoCollectorClient(channel);
            _lastBackendUrl = currentUrl;
            _lastAuthType = currentAuth;
        }

        return _client;
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

            var client = GetClient();
            var headers = new Grpc.Core.Metadata();
            var agentKey = Environment.GetEnvironmentVariable("HEIMDALL_AGENT_KEY") ?? "heimdall-dev-agent-key";
            headers.Add("x-agent-key", agentKey);

            var response = await client.ReportSystemInfoAsync(request, headers);

            if (response.Success)
            {
                _logger.LogInformation("Successfully reported system info via gRPC: {Message}", response.Message);
                if (_spooler != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _spooler.DrainSpoolAsync(async payload =>
                            {
                                var spooledData = System.Text.Json.JsonSerializer.Deserialize<SystemInfoData>(payload);
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
            }
            else
            {
                _logger.LogWarning("Failed to report system info via gRPC: {Message}", response.Message);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting system info via gRPC. Spooling to local buffer.");
            if (_spooler != null)
            {
                try
                {
                    string json = System.Text.Json.JsonSerializer.Serialize(data);
                    await _spooler.SpoolPayloadAsync(json);
                }
                catch (Exception spoolEx)
                {
                    _logger.LogError(spoolEx, "Failed to buffer payload into local spooler.");
                }
            }
            return null;
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

    private static AuthMode ParseAuthMode(string? auth) => auth switch
    {
        "HeimdallCert" => AuthMode.HeimdallCert,
        "UserCert" => AuthMode.UserCert,
        "ApiKey" => AuthMode.ApiKey,
        _ => AuthMode.NoAuth
    };
}
