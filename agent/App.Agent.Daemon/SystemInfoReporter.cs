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
        var backendUrl = configuration["BACKEND_URL"] ?? configuration["Backend:Url"] ?? "http://localhost:5001";
        var channel = GrpcChannel.ForAddress(backendUrl);
        _client = new SystemInfoCollector.SystemInfoCollectorClient(channel);
    }

    public async Task ReportInfoAsync(SystemInfoData data)
    {
        try
        {
            var request = new SystemInfoRequest
            {
                Hostname = data.Hostname,
                MachineIdentifier = data.MachineIdentifier,
                MacAddress = data.MacAddress,
                LastOnline = Timestamp.FromDateTimeOffset(data.LastOnline),
                HardwareConfig = new App.Shared.Protos.HardwareConfig
                {
                    Cpu = data.Hardware.Cpu,
                    Ram = data.Hardware.Ram,
                    Storage = data.Hardware.Storage
                },
                SoftwareConfig = new App.Shared.Protos.SoftwareConfig
                {
                    OsVersion = data.Software.OsVersion
                },
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

            if (data.Software.InstalledPackages != null)
            {
                foreach (var pkg in data.Software.InstalledPackages)
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
