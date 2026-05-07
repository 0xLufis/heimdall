using Grpc.Core;
using App.Shared.Protos;
using App.Shared.Entities;
using Google.Protobuf.WellKnownTypes;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
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
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInfoCollectorService"/> class.
    /// </summary>
    /// <param name="logger">The logger for the service.</param>
    /// <param name="repository">The repository for Client PC data operations.</param>
    /// <param name="dbContextFactory">The DB context factory for command handling.</param>
    public SystemInfoCollectorService(ILogger<SystemInfoCollectorService> logger, ClientPcRepository repository, IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _logger = logger;
        _repository = repository;
        _dbContextFactory = dbContextFactory;
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
            // 1. Prepare ClientPc entity from request
            var clientPc = new ClientPc
            {
                Name = request.Hostname,
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
                InventoryItems = request.Components
                    .Where(c => c.Name != "Events" && c.Name != "OS Environment" && c.Name != "Live Telemetry")
                    .Select(c => (BaseInventoryItem)new PcHardware
                    {
                        Name = c.Name,
                        Type = c.Type,
                        Metadata = string.IsNullOrEmpty(c.DataJson) ? null : System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonDocument>(c.DataJson)
                    }).ToList()
            };

            // Map abstract components to properties
            var osComp = request.Components.FirstOrDefault(c => c.Name == "OS Environment");
            if (osComp != null && !string.IsNullOrEmpty(osComp.DataJson))
            {
                clientPc.SystemMetadata = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonDocument>(osComp.DataJson);
            }

            var telemetryComp = request.Components.FirstOrDefault(c => c.Name == "Live Telemetry");
            if (telemetryComp != null && !string.IsNullOrEmpty(telemetryComp.DataJson))
            {
                // We'll store live telemetry in SystemMetadata for now, or specifically map to ResourceAverages if needed
                // For this refactor, let's merge or store telemetry separately
                clientPc.ResourceAverages = new ResourceAverages(); // Just initializing for now
            }

            // 2. Perform Upsert
            var dbClientPc = await _repository.UpsertByMacAddressAsync(clientPc);

            // 3. Handle Events and Commands in a single additional context scope if needed
            var response = new SystemInfoResponse
            {
                Success = true,
                Message = $"Information for {request.Hostname} updated successfully."
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            
            // Handle Agent Events
            var eventsJson = request.Components.FirstOrDefault(c => c.Name == "Events")?.DataJson;
            bool dataChanged = false;

            if (!string.IsNullOrEmpty(eventsJson))
            {
                try
                {
                    var agentEvents = System.Text.Json.JsonSerializer.Deserialize<List<AgentEvent>>(eventsJson);
                    if (agentEvents != null && agentEvents.Any())
                    {
                        foreach (var e in agentEvents)
                        {
                            e.ClientPcId = dbClientPc.Id;
                            e.Id = Guid.NewGuid();
                        }
                        dbContext.AgentEvents.AddRange(agentEvents);
                        dataChanged = true;
                        _logger.LogInformation("Queued {Count} events for {Hostname}", agentEvents.Count, request.Hostname);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse agent events for {Hostname}", request.Hostname);
                }
            }

            // Check for pending commands
            var pendingCommands = await dbContext.QueuedAgentCommands
                .Where(c => c.ClientPcId == dbClientPc.Id && !c.IsProcessed)
                .ToListAsync();

            if (pendingCommands.Any())
            {
                foreach (var cmd in pendingCommands)
                {
                    response.Commands.Add(new ServerCommand
                    {
                        Type = cmd.Type,
                        Payload = cmd.Payload,
                        Signature = cmd.Signature ?? string.Empty
                    });
                    cmd.IsProcessed = true;
                }
                dataChanged = true;
                _logger.LogInformation("Sent {Count} pending commands to {Hostname}", pendingCommands.Count, request.Hostname);
            }

            if (dataChanged)
            {
                await dbContext.SaveChangesAsync();
            }

            return response;
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
