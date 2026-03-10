using App.Shared.Protos;
using Grpc.Net.Client;
using Google.Protobuf.WellKnownTypes;
using App.Shared.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace App.Agent.Daemon;

public class SystemInfoReporter
{
    private readonly ILogger<SystemInfoReporter> _logger;
    private readonly SystemInfoCollector.SystemInfoCollectorClient _client;

    public SystemInfoReporter(ILogger<SystemInfoReporter> logger, IConfiguration configuration)
    {
        _logger = logger;
        // TODO: Move gRPC channel management to a centralized location and add TLS/Auth
        var backendUrl = configuration["BACKEND_URL"] ?? configuration["Backend:Url"] ?? "http://localhost:5000";
        var channel = GrpcChannel.ForAddress(backendUrl);
        _client = new SystemInfoCollector.SystemInfoCollectorClient(channel);
    }

    public async Task ReportInfoAsync(ClientPc pc)
    {
        try
        {
            var request = new SystemInfoRequest
            {
                Hostname = pc.Hostname,
                MachineIdentifier = pc.MachineIdentifier,
                MacAddress = pc.MacAddress,
                LastOnline = Timestamp.FromDateTimeOffset(pc.LastOnline ?? DateTimeOffset.UtcNow),
                HardwareConfig = new App.Shared.Protos.HardwareConfig
                {
                    Cpu = pc.HardwareConfig.Cpu,
                    Ram = pc.HardwareConfig.Ram,
                    Storage = pc.HardwareConfig.Storage
                },
                SoftwareConfig = new App.Shared.Protos.SoftwareConfig
                {
                    OsVersion = pc.SoftwareConfig.OsVersion
                }
            };

            if (pc.SoftwareConfig.InstalledPackages != null)
            {
                foreach (var pkg in pc.SoftwareConfig.InstalledPackages)
                {
                    request.SoftwareConfig.InstalledPackages.Add(pkg);
                }
            }

            var response = await _client.ReportSystemInfoAsync(request);

            if (response.Success)
            {
                _logger.LogInformation("Successfully reported system info via gRPC: {Message}", response.Message);
            }
            else
            {
                _logger.LogWarning("Failed to report system info via gRPC: {Message}", response.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while reporting system info via gRPC.");
        }
    }
}
