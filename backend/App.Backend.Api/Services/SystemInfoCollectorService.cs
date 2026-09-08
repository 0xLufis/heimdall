using Grpc.Core;
using App.Shared.Protos;
using App.Shared.Entities;
using Google.Protobuf.WellKnownTypes;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using App.Backend.Api.Hubs;
using static App.Shared.Protos.SystemInfoCollector;

namespace App.Backend.Api.Services;

/// <summary>
/// gRPC service for collecting system information from client PCs.
/// Implements the <see cref="SystemInfoCollector.SystemInfoCollectorBase"/> contract.
/// </summary>
public class SystemInfoCollectorService : SystemInfoCollector.SystemInfoCollectorBase
{
    private readonly ILogger<SystemInfoCollectorService> _logger;
    private readonly IClientPcRepository _repository;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<MaintenanceHub, IMaintenanceClient>? _hubContext;
    private readonly ICacheService? _cache;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemInfoCollectorService"/> class.
    /// </summary>
    /// <param name="logger">The logger for the service.</param>
    /// <param name="repository">The repository for Client PC data operations.</param>
    /// <param name="dbContextFactory">The DB context factory for command handling.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <param name="configuration">The configuration provider.</param>
    /// <param name="hubContext">Optional SignalR hub context for real-time notifications.</param>
    /// <param name="cache">Optional cache service for invalidating cached inventory trees.</param>
    public SystemInfoCollectorService(
        ILogger<SystemInfoCollectorService> logger,
        IClientPcRepository repository,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IHostEnvironment environment,
        IConfiguration configuration,
        IHubContext<MaintenanceHub, IMaintenanceClient>? hubContext = null,
        ICacheService? cache = null)
    {
        _logger = logger;
        _repository = repository;
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _configuration = configuration;
        _hubContext = hubContext;
        _cache = cache;
    }

    private async Task<bool> ValidateCallerAuthenticationAsync(ServerCallContext context)
    {
        // 1. Check mTLS Client Certificate if available
        var httpContext = context.GetHttpContext();
        var clientCert = httpContext?.Connection?.ClientCertificate;
        if (clientCert != null)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var activeCert = await db.ClientCertificates
                    .AnyAsync(c => c.Thumbprint == clientCert.Thumbprint && c.Status == "Active");
                if (activeCert)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate client certificate against PKI database.");
            }
        }

        // 2. Check Agent Key header (x-agent-key or Authorization)
        var agentKey = context.RequestHeaders.GetValue("x-agent-key");
        if (string.IsNullOrEmpty(agentKey))
        {
            var authHeader = context.RequestHeaders.GetValue("authorization");
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                agentKey = authHeader.Substring("Bearer ".Length).Trim();
            }
        }

        var configuredKey = _configuration["HEIMDALL_AGENT_KEY"];
        if (!string.IsNullOrEmpty(configuredKey) && !string.IsNullOrEmpty(agentKey))
        {
            if (string.Equals(agentKey, configuredKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // 3. Fallback for Development or Test environments
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Test"))
        {
            if (string.IsNullOrEmpty(configuredKey) || string.Equals(agentKey, "heimdall-dev-agent-key", StringComparison.Ordinal) || string.IsNullOrEmpty(agentKey))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports system information from a client PC and updates it in the database.
    /// </summary>
    /// <param name="request">The <see cref="SystemInfoRequest"/> containing the client's system information.</param>
    /// <param name="context">The gRPC server call context.</param>
    /// <returns>A <see cref="SystemInfoResponse"/> indicating the success or failure of the operation.</returns>
    public override async Task<SystemInfoResponse> ReportSystemInfo(SystemInfoRequest request, ServerCallContext context)
    {
        if (!await ValidateCallerAuthenticationAsync(context))
        {
            _logger.LogWarning("Rejecting unauthenticated gRPC telemetry report from {Hostname}", request.Hostname);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authentication required to submit telemetry."));
        }

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

            var telemetryComp = request.Components.FirstOrDefault(c => c.Name == "Live Telemetry" || c.Name == "Real-Time Metrics");
            if (telemetryComp != null && !string.IsNullOrEmpty(telemetryComp.DataJson))
            {
                clientPc.ResourceAverages = new ResourceAverages();
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(telemetryComp.DataJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("CpuLoad", out var cpuProp))
                    {
                        var str = cpuProp.GetString()?.Replace("%", "").Trim();
                        if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cpu))
                            clientPc.ResourceAverages.CpuUsageAverage = cpu;
                    }
                    else if (root.TryGetProperty("CpuUsagePercent", out var cpuPct) && cpuPct.TryGetDouble(out var cpuVal))
                    {
                        clientPc.ResourceAverages.CpuUsageAverage = cpuVal;
                    }

                    if (root.TryGetProperty("RamUsage", out var ramProp))
                    {
                        var str = ramProp.GetString()?.Replace("%", "").Trim();
                        if (double.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ram))
                            clientPc.ResourceAverages.RamUsageAverage = ram;
                    }
                    else if (root.TryGetProperty("RamUsagePercent", out var ramPct) && ramPct.TryGetDouble(out var ramVal))
                    {
                        clientPc.ResourceAverages.RamUsageAverage = ramVal;
                    }
                }
                catch
                {
                    // Fallback to initialized empty averages
                }
            }

            // 2. Perform Upsert
            var dbClientPc = await _repository.UpsertByMacAddressAsync(clientPc);

            // Invalidate Redis inventory caches so client requests fetch fresh data
            if (_cache != null)
            {
                try
                {
                    await _cache.RemoveAsync("inventory:tree");
                    await _cache.RemoveAsync("inventory:metadata_keys");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evict inventory cache on telemetry ingestion");
                }
            }

            // Broadcast real-time inventory and telemetry event via SignalR
            if (_hubContext != null)
            {
                try
                {
                    await _hubContext.Clients.All.InventoryUpdated("gRPC_Telemetry", request.Hostname, request.MacAddress);
                    await _hubContext.Clients.All.TelemetryReceived(request.Hostname, request.MacAddress, new
                    {
                        hostname = request.Hostname,
                        macAddress = request.MacAddress,
                        machineIdentifier = request.MachineIdentifier,
                        lastOnline = clientPc.LastOnline,
                        freeDiskSpace = clientPc.FreeDiskSpace,
                        resourceAverages = clientPc.ResourceAverages,
                        componentsCount = request.Components.Count
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast telemetry update to SignalR clients");
                }
            }

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
                            e.OrganizationId = dbClientPc.OrganizationId;
                        }
                        dbContext.AgentEvents.AddRange(agentEvents);
                        dataChanged = true;
                        _logger.LogInformation("Queued {Count} events for {Hostname}", agentEvents.Count, request.Hostname);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse agent events for {Hostname}. Quarantining malformed payload.", request.Hostname);
                    dbContext.MalformedTelemetryRecords.Add(new MalformedTelemetryRecord
                    {
                        SourceIdentifier = request.Hostname,
                        IngestionChannel = "gRPC_SystemInfo_Events",
                        ErrorReason = ex.Message,
                        RawPayload = eventsJson ?? string.Empty,
                        OrganizationId = dbClientPc.OrganizationId,
                        QuarantinedAt = DateTimeOffset.UtcNow
                    });
                    dataChanged = true;
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
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Internal error processing system info report from {Hostname}", request.Hostname);
            return new SystemInfoResponse
            {
                Success = false,
                Message = "An internal error occurred while processing the telemetry report."
            };
        }
    }
}
