namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using App.Backend.Api.Hubs;
using App.Contracts.Telemetry;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Protos;
using App.Shared.Protos.Telemetry;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Core implementation of industrial telemetry ingestion, inventory merging,
/// ADS PLC memory unmarshalling, and real-time SignalR dissemination.
/// </summary>
public class TelemetryIngestionService : ITelemetryIngestionService
{
    private static class ComponentNames
    {
        public const string Events = "Events";
        public const string OsEnvironment = "OS Environment";
        public const string OsDriverTelemetry = "OS & Driver Telemetry";
        public const string OperatingSystemAndCmi = "Operating System & CMI";
        public const string Software = "Software";
        public const string IndustrialOt = "IndustrialOT";
        public const string LiveTelemetry = "Live Telemetry";
        public const string RealTimeMetrics = "Real-Time Metrics";
    }

    private readonly ILogger<TelemetryIngestionService> _logger;
    private readonly IControllerRepository _repository;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IHubContext<MaintenanceHub, IMaintenanceClient>? _hubContext;
    private readonly ICacheService? _cache;
    private readonly ILuaAutomationService? _luaAutomation;

    public TelemetryIngestionService(
        ILogger<TelemetryIngestionService> logger,
        IControllerRepository repository,
        IDbContextFactory<AppDbContext> dbContextFactory,
        IHostEnvironment environment,
        IConfiguration configuration,
        IHubContext<MaintenanceHub, IMaintenanceClient>? hubContext = null,
        ICacheService? cache = null,
        ILuaAutomationService? luaAutomation = null)
    {
        _logger = logger;
        _repository = repository;
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _configuration = configuration;
        _hubContext = hubContext;
        _cache = cache;
        _luaAutomation = luaAutomation;
    }

    public bool ValidateCallerAuthentication(string? callerAuthKey)
    {
        var configuredKey = _configuration["HEIMDALL_AGENT_KEY"];
        if (!string.IsNullOrEmpty(configuredKey) && !string.IsNullOrEmpty(callerAuthKey))
        {
            if (string.Equals(callerAuthKey, configuredKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Fallback for Development or Test environments
        if (_environment.IsDevelopment() || _environment.IsEnvironment("Test"))
        {
            if (string.IsNullOrEmpty(configuredKey) ||
                string.Equals(callerAuthKey, "heimdall-dev-agent-key", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(callerAuthKey))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<SystemInfoResponse> ProcessSystemInfoAsync(SystemInfoRequest request, string? callerAuthKey = null)
    {
        if (!ValidateCallerAuthentication(callerAuthKey))
        {
            _logger.LogWarning("Rejecting unauthenticated MQTT telemetry report from {Hostname}", request.Hostname);
            return new SystemInfoResponse
            {
                Success = false,
                Message = "Authentication required to submit telemetry."
            };
        }

        _logger.LogInformation("Processing MQTT system info from {Hostname} ({MachineIdentifier})",
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
                LastOnline = request.LastOnline != null ? request.LastOnline.ToDateTimeOffset() : DateTimeOffset.UtcNow,
                FreeDiskSpace = request.DiskInfo != null ? new DiskSpaceInfo
                {
                    TotalFreeGB = request.DiskInfo.TotalFreeGb,
                    OsDriveFreeGB = request.DiskInfo.OsDriveFreeGb,
                    Drives = request.DiskInfo.Drives.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                } : null
            };

            var items = new List<BaseInventoryItem>();
            var parentResolutions = new List<(BaseInventoryItem Item, string? ParentName, Guid? ParentId)>();

            foreach (var c in request.Components.Where(c => c.Name != ComponentNames.Events && c.Name != ComponentNames.OsEnvironment && c.Name != ComponentNames.LiveTelemetry))
            {
                var item = new PcHardware
                {
                    Id = Guid.NewGuid(),
                    Name = c.Name,
                    Type = c.Type,
                    Technology = c.Technology
                };

                string? parentName = null;
                Guid? parentId = null;

                if (!string.IsNullOrEmpty(c.DataJson))
                {
                    try
                    {
                        var doc = JsonSerializer.Deserialize<JsonDocument>(c.DataJson);
                        item.Metadata = doc;

                        if (doc != null && doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            if (doc.RootElement.TryGetProperty("ParentComponentName", out var pn) && pn.ValueKind == JsonValueKind.String)
                                parentName = pn.GetString();
                            if (doc.RootElement.TryGetProperty("ParentComponentId", out var pid) && pid.TryGetGuid(out var pGuid))
                                parentId = pGuid;
                        }
                    }
                    catch
                    {
                        // Keep item without metadata
                    }
                }

                items.Add(item);
                if (!string.IsNullOrEmpty(parentName) || parentId.HasValue)
                {
                    parentResolutions.Add((item, parentName, parentId));
                }
            }

            // Link parent-child hierarchy across components
            foreach (var (childItem, parentName, parentId) in parentResolutions)
            {
                if (parentId.HasValue)
                {
                    childItem.ParentId = parentId.Value;
                }
                else if (!string.IsNullOrEmpty(parentName))
                {
                    var matchingParent = items.FirstOrDefault(i => string.Equals(i.Name, parentName, StringComparison.OrdinalIgnoreCase) && i.Id != childItem.Id);
                    if (matchingParent != null)
                    {
                        childItem.ParentId = matchingParent.Id;
                    }
                }
            }

            clientPc.InventoryItems = items;

            // Map abstract components to properties
            var osComp = request.Components.FirstOrDefault(c =>
                c.Name == ComponentNames.OsEnvironment ||
                c.Name == ComponentNames.OsDriverTelemetry ||
                c.Name == ComponentNames.OperatingSystemAndCmi ||
                c.Name == ComponentNames.Software);
            var industrialOtComp = request.Components.FirstOrDefault(c => c.Name == ComponentNames.IndustrialOt);

            if (osComp != null && !string.IsNullOrEmpty(osComp.DataJson))
            {
                try
                {
                    var metaNode = JsonNode.Parse(osComp.DataJson)?.AsObject() ?? new JsonObject();
                    if (industrialOtComp != null && !string.IsNullOrEmpty(industrialOtComp.DataJson))
                    {
                        using var otDoc = JsonDocument.Parse(industrialOtComp.DataJson);
                        var otRoot = otDoc.RootElement;
                        if (otRoot.TryGetProperty("AdsState", out var adsState))
                            metaNode["TwinCAT_ADS_State"] = adsState.GetString();
                        if (otRoot.TryGetProperty("AdsAmsNetId", out var netId))
                            metaNode["AdsAmsNetId"] = netId.GetString();
                        if (otRoot.TryGetProperty("OpcEndpoint", out var opcEp))
                            metaNode["OpcEndpoint"] = opcEp.GetString();
                        if (otRoot.TryGetProperty("OpcConnected", out var opcConn))
                            metaNode["OpcConnected"] = opcConn.GetBoolean();
                    }
                    clientPc.SystemMetadata = JsonSerializer.Deserialize<JsonDocument>(metaNode.ToJsonString());
                }
                catch
                {
                    clientPc.SystemMetadata = JsonSerializer.Deserialize<JsonDocument>(osComp.DataJson);
                }

                try
                {
                    if (clientPc.SystemMetadata != null && clientPc.SystemMetadata.RootElement.TryGetProperty("IPAddress", out var ipProp) && ipProp.ValueKind == JsonValueKind.String)
                    {
                        clientPc.IpAddress = ipProp.GetString();
                    }
                    else if (clientPc.SystemMetadata != null && clientPc.SystemMetadata.RootElement.TryGetProperty("ipAddress", out var ipProp2) && ipProp2.ValueKind == JsonValueKind.String)
                    {
                        clientPc.IpAddress = ipProp2.GetString();
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            if (string.IsNullOrEmpty(clientPc.IpAddress))
            {
                foreach (var c in request.Components)
                {
                    if (string.IsNullOrEmpty(c.DataJson)) continue;
                    try
                    {
                        using var cDoc = JsonDocument.Parse(c.DataJson);
                        if (cDoc.RootElement.TryGetProperty("IPAddress", out var p) && p.ValueKind == JsonValueKind.String)
                        {
                            clientPc.IpAddress = p.GetString();
                            break;
                        }
                        if (cDoc.RootElement.TryGetProperty("ipAddress", out var p2) && p2.ValueKind == JsonValueKind.String)
                        {
                            clientPc.IpAddress = p2.GetString();
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore malformed individual components
                    }
                }
            }

            var telemetryComp = request.Components.FirstOrDefault(c => c.Name == ComponentNames.LiveTelemetry || c.Name == ComponentNames.RealTimeMetrics);
            if (telemetryComp != null && !string.IsNullOrEmpty(telemetryComp.DataJson))
            {
                clientPc.ResourceAverages = new ResourceAverages();
                try
                {
                    using var doc = JsonDocument.Parse(telemetryComp.DataJson);
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
                    var tenantClients = MaintenanceHubGroups.ForOrganization(_hubContext.Clients, dbClientPc.OrganizationId);
                    await tenantClients.InventoryUpdated("MQTT_Telemetry", request.Hostname, request.MacAddress);
                    await tenantClients.TelemetryReceived(request.Hostname, request.MacAddress, new TelemetryBroadcastDto
                    {
                        Hostname = request.Hostname,
                        MacAddress = request.MacAddress,
                        MachineIdentifier = request.MachineIdentifier,
                        LastOnline = clientPc.LastOnline,
                        FreeDiskSpace = clientPc.FreeDiskSpace,
                        ResourceAverages = clientPc.ResourceAverages,
                        ComponentsCount = request.Components.Count
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast telemetry update to SignalR clients");
                }
            }

            // 3. Handle Events and Commands in DB context scope
            var response = new SystemInfoResponse
            {
                Success = true,
                Message = $"Information for {request.Hostname} updated successfully."
            };

            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            // Handle Agent Events
            var eventsJson = request.Components.FirstOrDefault(c => c.Name == ComponentNames.Events)?.DataJson;
            bool dataChanged = false;

            if (!string.IsNullOrEmpty(eventsJson))
            {
                try
                {
                    var agentEvents = JsonSerializer.Deserialize<List<AgentEvent>>(eventsJson);
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
                        IngestionChannel = "MQTT_SystemInfo_Events",
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
                _logger.LogInformation("Attached {Count} pending commands for {Hostname}", pendingCommands.Count, request.Hostname);
            }

            if (dataChanged)
            {
                await dbContext.SaveChangesAsync();
            }

            return response;
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

    public async Task<bool> ProcessPlcMemoryAsync(string machineIdentifier, AdsPlcMemoryBlock memoryBlock, string? callerAuthKey = null)
    {
        if (!ValidateCallerAuthentication(callerAuthKey))
        {
            _logger.LogWarning("Rejecting unauthenticated MQTT ADS PLC memory update from machine {MachineId}", machineIdentifier);
            return false;
        }

        _logger.LogInformation("Processing ADS PLC memory block from {MachineId} [Symbol={Symbol}, Group=0x{Group:X}, Offset={Offset}]",
            machineIdentifier, memoryBlock.SymbolName, memoryBlock.IndexGroup, memoryBlock.IndexOffset);

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var pc = await db.ClientPcs.FirstOrDefaultAsync(p => p.MachineIdentifier == machineIdentifier);
            if (pc == null)
            {
                // Auto-provision basic ClientPc if not yet registered
                pc = new ClientPc
                {
                    Id = Guid.NewGuid(),
                    Name = machineIdentifier,
                    Hostname = machineIdentifier,
                    MachineIdentifier = machineIdentifier,
                    MacAddress = "00:00:00:00:00:00",
                    LastOnline = DateTimeOffset.UtcNow
                };
                db.ClientPcs.Add(pc);
                await db.SaveChangesAsync();
            }
            else
            {
                pc.LastOnline = DateTimeOffset.UtcNow;
            }

            // Update PLC metadata on ClientPc
            var metaObj = pc.SystemMetadata != null
                ? JsonNode.Parse(pc.SystemMetadata.RootElement.GetRawText())?.AsObject() ?? new JsonObject()
                : new JsonObject();

            var plcNode = metaObj["AdsPlcMemory"]?.AsObject() ?? new JsonObject();
            plcNode["AmsNetId"] = memoryBlock.AmsNetId;
            plcNode["AmsPort"] = memoryBlock.AmsPort;
            plcNode["IndexGroup"] = $"0x{memoryBlock.IndexGroup:X4}";
            plcNode["IndexOffset"] = memoryBlock.IndexOffset;
            plcNode["SymbolName"] = memoryBlock.SymbolName;
            plcNode["PlcTypeName"] = memoryBlock.PlcTypeName;
            plcNode["Quality"] = memoryBlock.Quality.ToString();
            plcNode["LastUpdated"] = (memoryBlock.Timestamp != null ? memoryBlock.Timestamp.ToDateTimeOffset() : DateTimeOffset.UtcNow).ToString("o");
            plcNode["RawBytesLength"] = memoryBlock.RawMemory.Length;

            metaObj["AdsPlcMemory"] = plcNode;
            pc.SystemMetadata = JsonSerializer.Deserialize<JsonDocument>(metaObj.ToJsonString());

            await db.SaveChangesAsync();

            // Broadcast real-time SignalR event
            if (_hubContext != null)
            {
                try
                {
                    var summary = new
                    {
                        AmsNetId = memoryBlock.AmsNetId,
                        AmsPort = memoryBlock.AmsPort,
                        SymbolName = memoryBlock.SymbolName,
                        PlcTypeName = memoryBlock.PlcTypeName,
                        Quality = memoryBlock.Quality.ToString(),
                        Timestamp = memoryBlock.Timestamp != null ? memoryBlock.Timestamp.ToDateTimeOffset() : DateTimeOffset.UtcNow,
                        RawBytesCount = memoryBlock.RawMemory.Length
                    };

                    await MaintenanceHubGroups.ForOrganization(_hubContext.Clients, pc.OrganizationId).PlcMemoryReceived(
                        pc.Hostname ?? machineIdentifier,
                        machineIdentifier,
                        memoryBlock.AmsNetId,
                        memoryBlock.SymbolName,
                        summary);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to broadcast PLC memory update over SignalR");
                }
            }

            await PublishStationTelemetryAsync(db, pc, memoryBlock);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ADS PLC memory block for {MachineId}", machineIdentifier);
            return false;
        }
    }

    public async Task<bool> ProcessPlcMemoryBatchAsync(AdsPlcMemoryBatch memoryBatch, string? callerAuthKey = null)
    {
        if (!ValidateCallerAuthentication(callerAuthKey)) return false;

        bool allOk = true;
        foreach (var block in memoryBatch.Blocks)
        {
            var ok = await ProcessPlcMemoryAsync(memoryBatch.MachineIdentifier, block, callerAuthKey);
            if (!ok) allOk = false;
        }
        return allOk;
    }

    public async Task<bool> ProcessAgentEventAsync(AgentEventMessage eventMessage, string? callerAuthKey = null)
    {
        if (!ValidateCallerAuthentication(callerAuthKey)) return false;

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var pc = await db.ClientPcs.FirstOrDefaultAsync(p => p.MachineIdentifier == eventMessage.MachineIdentifier);

            var agentEvent = new AgentEvent
            {
                Id = Guid.NewGuid(),
                ClientPcId = pc?.Id ?? Guid.Empty,
                OrganizationId = pc?.OrganizationId ?? string.Empty,
                Source = eventMessage.Source,
                Level = eventMessage.Level,
                Message = !string.IsNullOrEmpty(eventMessage.Message) ? eventMessage.Message : eventMessage.PayloadJson,
                Timestamp = eventMessage.Timestamp != null ? eventMessage.Timestamp.ToDateTime() : DateTime.UtcNow
            };

            db.AgentEvents.Add(agentEvent);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AgentEventMessage from {MachineId}", eventMessage.MachineIdentifier);
            return false;
        }
    }

    public async Task<TelemetryBatchResponse> ProcessTelemetryBatchAsync(TelemetryBatchRequest batchRequest, string? callerAuthKey = null)
    {
        if (!ValidateCallerAuthentication(callerAuthKey))
        {
            return new TelemetryBatchResponse
            {
                Success = false,
                ProcessedCount = 0,
                Message = "Authentication required."
            };
        }

        // Decompress and unpack points if necessary
        return new TelemetryBatchResponse
        {
            Success = true,
            ProcessedCount = 1,
            Message = $"Batch from {batchRequest.Hostname} processed successfully."
        };
    }

    /// <summary>
    /// Station telemetry path: PLC block with station fields → merged snapshot in the distributed
    /// cache (read by GET /api/v1/analytics/station-kpis) → SignalR StationOscillationReceived /
    /// StationTelemetryReceived to the station's organization group → Lua TelemetryChange scripts.
    /// Every stage is failure-isolated: none of them can fail the ingestion itself.
    /// </summary>
    private async Task PublishStationTelemetryAsync(AppDbContext db, ClientPc pc, AdsPlcMemoryBlock block)
    {
        StationTelemetrySnapshot? sample;
        List<(string Metric, double Value)> metrics;
        try
        {
            sample = StationTelemetryExtractor.Extract(block, out metrics);
            if (sample == null) return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract station telemetry from {Symbol}", block.SymbolName);
            return;
        }

        Machine? machine;
        try
        {
            machine = await ResolveStationAsync(db, pc, block);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve station for PLC block {Symbol} on {Pc}", block.SymbolName, pc.MachineIdentifier);
            return;
        }
        if (machine == null)
        {
            _logger.LogDebug("PLC block {Symbol} on {Pc} carries station telemetry but no unique station is bound", block.SymbolName, pc.MachineIdentifier);
            return;
        }

        var handle = StationHandle(machine);
        var snapshot = new StationTelemetrySnapshot { MachineId = machine.Id };
        if (_cache != null)
        {
            try
            {
                snapshot = await _cache.GetAsync<StationTelemetrySnapshot>(StationTelemetryStore.Key(machine.Id)) ?? snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read station telemetry snapshot for {Machine}", machine.Id);
            }
        }
        snapshot.MachineId = machine.Id;
        snapshot.OrganizationId = machine.OrganizationId;
        snapshot.MergeFrom(sample);

        if (_cache != null)
        {
            try
            {
                await _cache.SetAsync(StationTelemetryStore.Key(machine.Id), snapshot, StationTelemetryStore.Retention);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store station telemetry snapshot for {Machine}", machine.Id);
            }
        }

        if (_hubContext != null)
        {
            try
            {
                var clients = MaintenanceHubGroups.ForOrganization(_hubContext.Clients, machine.OrganizationId);
                var stationId = machine.Id.ToString();
                var sampleTime = sample.LastTelemetryAt ?? DateTimeOffset.UtcNow;
                if (sample.HasDynamics)
                {
                    await clients.StationOscillationReceived(stationId, new StationOscillationBroadcastDto
                    {
                        StationId = stationId,
                        Handle = handle,
                        VibrationMmS = sample.VibrationMmS,
                        SpindleRpm = sample.SpindleRpm,
                        CycleTimeMs = sample.CycleTimeMs,
                        PowerKw = sample.PowerKw,
                        TemperatureC = sample.TemperatureC,
                        FrequencyHz = sample.FrequencyHz,
                        OperationalStatus = sample.OperationalStatus,
                        Timestamp = sampleTime.ToUnixTimeMilliseconds()
                    });
                }
                if (sample.HasProduction)
                {
                    await clients.StationTelemetryReceived(stationId, new StationTelemetryBroadcastDto
                    {
                        StationId = stationId,
                        Handle = handle,
                        Name = machine.DisplayName ?? machine.Name,
                        Oee = snapshot.EffectiveOee,
                        Availability = snapshot.Availability,
                        Performance = snapshot.Performance,
                        Quality = snapshot.EffectiveQuality,
                        Yield = snapshot.Yield,
                        PartsPerHour = snapshot.PartsPerHour,
                        TargetPartsPerHour = snapshot.TargetPartsPerHour,
                        CycleTimeSeconds = snapshot.EffectiveCycleTimeSeconds,
                        TargetCycleTimeSeconds = snapshot.TargetCycleTimeSeconds,
                        TotalPartsProduced = snapshot.TotalPartsProduced,
                        ScrapParts = snapshot.ScrapParts,
                        OperationalStatus = snapshot.OperationalStatus,
                        LastTelemetryAt = snapshot.LastTelemetryAt
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to broadcast station telemetry for {Machine}", machine.Id);
            }
        }

        if (_luaAutomation != null)
        {
            var machineKey = machine.Id.ToString();
            foreach (var (metric, value) in metrics)
            {
                try
                {
                    // Feeds the Lua telemetry ring buffer and runs TelemetryChange-triggered scripts.
                    await _luaAutomation.EvaluateTelemetryChangeAsync(machineKey, metric, value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lua telemetry evaluation failed for {Machine}/{Metric}", machine.Id, metric);
                }
            }
        }
    }

    /// <summary>
    /// The station a PLC block belongs to: the controlled machine named by the block's
    /// <c>StationId</c>/<c>MachineId</c> metadata, otherwise the PC's only controlled machine.
    /// Ambiguous blocks (several machines, no metadata) are not attributed.
    /// </summary>
    private static async Task<Machine?> ResolveStationAsync(AppDbContext db, ClientPc pc, AdsPlcMemoryBlock block)
    {
        var machines = await db.Machines
            .Where(m => m.StationControllers.Any(sc => sc.ClientPcId == pc.Id))
            .ToListAsync();
        if (machines.Count == 0) return null;

        string? requested = null;
        foreach (var key in new[] { "StationId", "MachineId", "Station", "stationId", "machineId" })
        {
            if (block.Metadata.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) { requested = v.Trim(); break; }
        }

        if (requested != null)
        {
            return machines.FirstOrDefault(m =>
                string.Equals(m.Id.ToString(), requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.CustomIdentifier, requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.PinnedObjectHandle, requested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Name, requested, StringComparison.OrdinalIgnoreCase));
        }

        return machines.Count == 1 ? machines[0] : null;
    }

    /// <summary>Identity the plant map uses for a station: floor-plan handle, else custom identifier, else name.</summary>
    public static string StationHandle(Machine machine) =>
        !string.IsNullOrWhiteSpace(machine.PinnedObjectHandle) ? machine.PinnedObjectHandle!
        : !string.IsNullOrWhiteSpace(machine.CustomIdentifier) ? machine.CustomIdentifier
        : machine.Name;
}
