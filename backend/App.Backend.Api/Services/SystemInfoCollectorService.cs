using Grpc.Core;
using App.Shared.Protos;
using App.Shared.Entities;
using Google.Protobuf.WellKnownTypes;
using App.Infrastructure.Repositories;

namespace App.Backend.Api.Services;

public class SystemInfoCollectorService : SystemInfoCollector.SystemInfoCollectorBase
{
    private readonly ILogger<SystemInfoCollectorService> _logger;
    private readonly ClientPcRepository _repository;

    public SystemInfoCollectorService(ILogger<SystemInfoCollectorService> logger, ClientPcRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    public override async Task<SystemInfoResponse> ReportSystemInfo(SystemInfoRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received system info from {Hostname} ({MachineIdentifier})", 
            request.Hostname, request.MachineIdentifier);

        try
        {
            var clientPc = new ClientPc
            {
                Hostname = request.Hostname,
                MachineIdentifier = request.MachineIdentifier,
                MacAddress = request.MacAddress,
                LastOnline = request.LastOnline.ToDateTimeOffset(),
                HardwareConfig = new App.Shared.Entities.HardwareConfig
                {
                    Cpu = request.HardwareConfig.Cpu,
                    Ram = request.HardwareConfig.Ram,
                    Storage = request.HardwareConfig.Storage
                },
                SoftwareConfig = new App.Shared.Entities.SoftwareConfig
                {
                    OsVersion = request.SoftwareConfig.OsVersion,
                    InstalledPackages = request.SoftwareConfig.InstalledPackages.ToList()
                }
            };

            await _repository.UpsertByMacAddressAsync(clientPc);

            return new SystemInfoResponse
            {
                Success = true,
                Message = $"Information for {request.Hostname} updated successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing system info report from {Hostname}", request.Hostname);
            return new SystemInfoResponse
            {
                Success = false,
                Message = "Error while saving reported data."
            };
        }
    }
}
