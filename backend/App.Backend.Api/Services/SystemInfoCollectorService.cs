using Grpc.Core;
using App.Shared.Protos;
using App.Shared.Entities;
using Google.Protobuf.WellKnownTypes;
using App.Infrastructure.Repositories;
using static App.Shared.Protos.SystemInfoCollector;

namespace App.Backend.Api.Services;

/// <summary>
/// gRPC service for collecting system information from client PCs.
/// Implements the <see cref="SystemInfoCollector.SystemInfoCollectorBase"/> contract.
/// </summary>
public class SystemInfoCollectorService : SystemInfoCollector.SystemInfoCollectorBase
{
    private readonly ILogger<SystemInfoCollectorService> _logger;
    private readonly ClientPcRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInfoCollectorService"/> class.
    /// </summary>
    /// <param name="logger">The logger for the service.</param>
    /// <param name="repository">The repository for Client PC data operations.</param>
    public SystemInfoCollectorService(ILogger<SystemInfoCollectorService> logger, ClientPcRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    /// <summary>
    /// Reports system information from a client PC and updates it in the database.
    /// </summary>
    /// <param name="request">The <see cref="SystemInfoRequest"/> containing the client's system information.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>A <see cref="SystemInfoResponse"/> indicating the success or failure of the operation.</returns>
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
                FreeDiskSpace = request.DiskInfo != null ? new DiskSpaceInfo
                {
                    TotalFreeGB = request.DiskInfo.TotalFreeGb,
                    OsDriveFreeGB = request.DiskInfo.OsDriveFreeGb,
                    Drives = request.DiskInfo.Drives.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                } : null,
                Components = request.Components.Select(c => new App.Shared.Entities.InventoryComponent
                {
                    Name = c.Name,
                    Technology = c.Technology,
                    TopLevelFlags = new ComponentTopLevelFlags { Type = c.Type },
                    Data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonDocument>(c.DataJson) ?? System.Text.Json.JsonSerializer.SerializeToDocument(new { })
                }).ToList()
            };

            // Ensure "Peripherals" component always exists to match previous behavior
            if (!clientPc.Components.Any(c => c.Name == "Peripherals"))
            {
                clientPc.Components.Add(new App.Shared.Entities.InventoryComponent 
                { 
                    Name = "Peripherals", 
                    Technology = "Agent",
                    TopLevelFlags = new ComponentTopLevelFlags { Type = "peripherals" },
                    Data = System.Text.Json.JsonSerializer.SerializeToDocument(new { })
                });
            }

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
