using App.Shared.Protos;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace App.Agent.Daemon;

public class SystemInfoReporter
{
    private readonly ILogger<SystemInfoReporter> _logger;
    private readonly ConfigurationService _configService;
    private SystemInfoCollector.SystemInfoCollectorClient? _client;
    private string? _lastBackendUrl;
    private string? _lastAuthType;

    public SystemInfoReporter(ILogger<SystemInfoReporter> logger, ConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    private SystemInfoCollector.SystemInfoCollectorClient GetClient()
    {
        var config = _configService.Config;
        var currentUrl = config.BackendUrl;
        var currentAuth = config.AuthType;

        if (_client == null || _lastBackendUrl != currentUrl || _lastAuthType != currentAuth)
        {
            _logger.LogInformation("Creating gRPC client for {Url} with Auth={Auth}", currentUrl, currentAuth);
            
            var handler = new HttpClientHandler();
            
            if (currentAuth == "HeimdallCert" || currentAuth == "UserCert")
            {
                if (!string.IsNullOrEmpty(config.ClientCertificatePath))
                {
                    try
                    {
                        var cert = X509CertificateLoader.LoadCertificateFromFile(config.ClientCertificatePath);
                        handler.ClientCertificates.Add(cert);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load client certificate from {Path}", config.ClientCertificatePath);
                    }
                }
                else if (currentAuth == "UserCert" && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Fallback to searching machine store if path is empty but UserCert is requested
                    using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                    store.Open(OpenFlags.ReadOnly);
                    var certs = store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, true);
                    if (certs.Count > 0)
                    {
                         handler.ClientCertificates.Add(certs[0]);
                         _logger.LogInformation("Loaded certificate from Windows Machine Store: {Subject}", certs[0].Subject);
                    }
                }
            }

            var channel = GrpcChannel.ForAddress(currentUrl, new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            _client = new SystemInfoCollector.SystemInfoCollectorClient(channel);
            _lastBackendUrl = currentUrl;
            _lastAuthType = currentAuth;
        }
        return _client;
    }

    public ConfigurationService GetConfigService() => _configService;

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

            request.Components.Add(new App.Shared.Protos.InventoryComponent
            {
                Name = "Hardware",
                Technology = "Agent",
                Type = "hardware",
                DataJson = System.Text.Json.JsonSerializer.Serialize(data.Hardware)
            });

            request.Components.Add(new App.Shared.Protos.InventoryComponent
            {
                Name = "Software",
                Technology = "Agent",
                Type = "software",
                DataJson = System.Text.Json.JsonSerializer.Serialize(data.Software)
            });

            request.Components.Add(new App.Shared.Protos.InventoryComponent
            {
                Name = "PhysicalDrives",
                Technology = "Agent",
                Type = "hardware",
                DataJson = System.Text.Json.JsonSerializer.Serialize(data.Disk.PhysicalDrives)
            });

            if (data.Events != null && data.Events.Any())
            {
                request.Components.Add(new App.Shared.Protos.InventoryComponent
                {
                    Name = "Events",
                    Technology = "Agent",
                    Type = "logs",
                    DataJson = System.Text.Json.JsonSerializer.Serialize(data.Events)
                });
            }

            var client = GetClient();
            var response = await client.ReportSystemInfoAsync(request);

            if (response.Success)
            {
                _logger.LogInformation("Successfully reported system info via gRPC: {Message}", response.Message);
            }
            else
            {
                _logger.LogWarning("Failed to report system info via gRPC: {Message}", response.Message);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting system info via gRPC.");
            return null;
        }
    }
}

