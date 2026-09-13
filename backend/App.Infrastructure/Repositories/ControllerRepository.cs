using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

public class ControllerRepository : IControllerRepository
{
    private readonly AppDbContext _context;

    public ControllerRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ClientPc>> GetAllAsync()
    {
        return await _context.ClientPcs
            .Include(c => c.ControlledMachines)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.InventoryItems)
                .ThenInclude(i => i.Children)
                    .ThenInclude(c => c.Children)
                        .ThenInclude(c => c.Children)
                            .ThenInclude(c => c.Children)
            .ToListAsync();
    }

    public async Task<ClientPc?> GetByIdAsync(Guid id)
    {
        return await _context.ClientPcs
            .Include(pc => pc.ControlledMachines)
            .Include(pc => pc.ResponsibleTeams)
            .Include(pc => pc.InventoryItems)
            .FirstOrDefaultAsync(pc => pc.Id == id);
    }

    public async Task<ClientPc> CreateAsync(ClientPc pc)
    {
        _context.ClientPcs.Add(pc);
        await _context.SaveChangesAsync();
        return pc;
    }

    public async Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc)
    {
        var existingByMac = await _context.ClientPcs
            .Include(x => x.InventoryItems)
            .FirstOrDefaultAsync(x => x.MacAddress == pc.MacAddress);

        var existingByHostname = await _context.ClientPcs
            .Where(x => x.Hostname == pc.Hostname && x.MacAddress != pc.MacAddress)
            .FirstOrDefaultAsync();

        if (existingByHostname != null)
        {
            existingByHostname.Hostname = $"{existingByHostname.Hostname}-OLD-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        if (existingByMac == null)
        {
            _context.ClientPcs.Add(pc);
            await _context.SaveChangesAsync();
            return pc;
        }

        bool changed = false;
        if (existingByMac.Hostname != pc.Hostname) { existingByMac.Hostname = pc.Hostname; changed = true; }
        if (existingByMac.MachineIdentifier != pc.MachineIdentifier) { existingByMac.MachineIdentifier = pc.MachineIdentifier; changed = true; }
        
        existingByMac.LastOnline = pc.LastOnline;
        changed = true;
        
        if (pc.FreeDiskSpace != null)
        {
            existingByMac.FreeDiskSpace = pc.FreeDiskSpace;
            changed = true;
        }

        if (pc.SystemMetadata != null)
        {
            existingByMac.SystemMetadata = pc.SystemMetadata;
            changed = true;
        }

        if (pc.ResourceAverages != null)
        {
            existingByMac.ResourceAverages = pc.ResourceAverages;
            changed = true;
        }

        if (pc.InventoryItems != null)
        {
            var reportedHardwares = pc.InventoryItems.OfType<PcHardware>().ToList();
            var existingHardwares = existingByMac.InventoryItems.OfType<PcHardware>().ToList();
            var reportedNames = new HashSet<string>(reportedHardwares.Select(h => h.Name));
            
            int removedCount = existingByMac.InventoryItems.RemoveAll(i => i is PcHardware h && !reportedNames.Contains(h.Name));
            if (removedCount > 0) changed = true;

            foreach (var reported in reportedHardwares)
            {
                var existing = existingHardwares.FirstOrDefault(h => h.Name == reported.Name);
                if (existing != null)
                {
                    if (existing.Type != reported.Type || existing.Capacity != reported.Capacity)
                    {
                        existing.Type = reported.Type;
                        existing.Capacity = reported.Capacity;
                        existing.Metadata = reported.Metadata;
                        changed = true;
                    }
                }
                else
                {
                    existingByMac.InventoryItems.Add(reported);
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }
        return existingByMac;
    }

    public async Task<ClientPc?> UpdateAsync(
        Guid id, 
        string? name, 
        string? hostname, 
        string? macAddress, 
        string? pinnedObjectHandle, 
        List<Guid>? controlledMachineIds)
    {
        var pc = await _context.ClientPcs
            .Include(c => c.ControlledMachines)
            .Include(c => c.ResponsibleTeams)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (pc == null) return null;

        if (!string.IsNullOrEmpty(pinnedObjectHandle)) pc.PinnedObjectHandle = pinnedObjectHandle;
        if (!string.IsNullOrEmpty(name)) pc.Name = name;
        if (!string.IsNullOrEmpty(hostname)) pc.Hostname = hostname;
        if (!string.IsNullOrEmpty(macAddress)) pc.MacAddress = macAddress;

        if (controlledMachineIds != null)
        {
            pc.ControlledMachines.Clear();
            foreach (var machineId in controlledMachineIds)
            {
                var existingMachine = await _context.Machines.FindAsync(machineId);
                if (existingMachine != null)
                {
                    pc.ControlledMachines.Add(existingMachine);
                }
            }
        }

        await _context.SaveChangesAsync();
        return pc;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var pc = await _context.ClientPcs.FindAsync(id);
        if (pc == null) return false;
        _context.ClientPcs.Remove(pc);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<int> GetCountAsync()
    {
        return await _context.ClientPcs.CountAsync();
    }

    public async Task<int> GetActiveCountAsync(TimeSpan activeThreshold)
    {
        var threshold = DateTimeOffset.UtcNow.Subtract(activeThreshold);
        return await _context.ClientPcs.CountAsync(c => c.LastOnline >= threshold);
    }

    public async Task<List<ClientPc>> GetRecentClientsAsync(int count)
    {
        return await _context.ClientPcs
            .OrderByDescending(c => c.LastOnline)
            .Take(count)
            .ToListAsync();
    }

    public async Task<DiagnosticSnapshot> CreateDiagnosticSnapshotAsync(Guid clientPcId, string userId, string? userName, string? orgId)
    {
        var pc = await _context.ClientPcs
            .Include(c => c.ControlledMachines)
            .Include(c => c.ResponsibleTeams)
            .Include(c => c.InventoryItems)
            .Include(c => c.Events)
            .FirstOrDefaultAsync(c => c.Id == clientPcId);

        if (pc == null)
        {
            throw new KeyNotFoundException($"ClientPc with ID '{clientPcId}' not found.");
        }

        var snapshotId = Guid.NewGuid();
        var capturedAt = DateTimeOffset.UtcNow;

        var snapshotData = new
        {
            SnapshotId = snapshotId,
            ClientPcId = pc.Id,
            Hostname = pc.Hostname ?? pc.Name,
            pc.MachineIdentifier,
            pc.MacAddress,
            pc.IpAddress,
            pc.LastOnline,
            CapturedAtUtc = capturedAt,
            CapturedBy = new { UserId = userId, UserName = userName },
            ResourceMetrics = new
            {
                CpuUsageAverage = pc.ResourceAverages?.CpuUsageAverage ?? 0,
                RamUsageAverage = pc.ResourceAverages?.RamUsageAverage ?? 0,
                Disk = pc.FreeDiskSpace != null ? new
                {
                    pc.FreeDiskSpace.TotalFreeGB,
                    pc.FreeDiskSpace.OsDriveFreeGB,
                    pc.FreeDiskSpace.Drives
                } : null
            },
            HardwareComponents = pc.InventoryItems.Select(i => new
            {
                i.Id,
                i.Name,
                i.DisplayName,
                ItemType = i.GetType().Name,
                i.Technology,
                Metadata = i.Metadata
            }).ToList(),
            ControlledStations = pc.ControlledMachines.Select(m => new
            {
                m.Id,
                m.Name,
                m.CustomIdentifier,
                m.MachineType
            }).ToList(),
            RecentEvents = pc.Events.OrderByDescending(e => e.Timestamp).Take(25).Select(e => new
            {
                e.Id,
                e.Source,
                e.Level,
                e.Message,
                e.Timestamp
            }).ToList(),
            SystemMetadata = pc.SystemMetadata
        };

        string jsonPayload = JsonSerializer.Serialize(snapshotData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Compute SHA-256 integrity hash
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(jsonPayload));
        string payloadHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        var snapshot = new DiagnosticSnapshot
        {
            Id = snapshotId,
            ClientPcId = pc.Id,
            Hostname = pc.Hostname ?? pc.Name,
            MachineIdentifier = pc.MachineIdentifier,
            CapturedByUserId = userId,
            CapturedByUserName = userName,
            CapturedAtUtc = capturedAt,
            SnapshotPayloadJson = jsonPayload,
            PayloadHashSha256 = payloadHash,
            OrganizationId = orgId ?? pc.OrganizationId
        };

        _context.DiagnosticSnapshots.Add(snapshot);

        // Record AuditLog for compliance
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            UserName = userName,
            Action = "DIAGNOSTIC_SNAPSHOT",
            EntityType = "ClientPc",
            EntityId = pc.Id.ToString(),
            NewValuesJson = JsonSerializer.Serialize(new
            {
                SnapshotId = snapshot.Id,
                Hostname = snapshot.Hostname,
                PayloadHash = payloadHash,
                ComponentsCount = pc.InventoryItems.Count
            }),
            OrganizationId = orgId ?? pc.OrganizationId,
            Timestamp = DateTimeOffset.UtcNow
        };
        _context.AuditLogs.Add(auditLog);

        // Record operational AgentEvent
        var agentEvent = new AgentEvent
        {
            Id = Guid.NewGuid(),
            ClientPcId = pc.Id,
            Source = "DiagnosticSnapshot",
            Level = "Information",
            Message = $"Diagnostic snapshot '{snapshot.Id}' captured by {userName ?? userId} (SHA256: {payloadHash[..12]}...)",
            Timestamp = DateTime.UtcNow,
            OrganizationId = orgId ?? pc.OrganizationId
        };
        _context.AgentEvents.Add(agentEvent);

        // Queue command for edge agent to immediately re-sync state
        var cmd = new QueuedAgentCommand
        {
            Id = Guid.NewGuid(),
            ClientPcId = pc.Id,
            Type = "TRIGGER_DIAGNOSTIC_SNAPSHOT",
            Payload = JsonSerializer.Serialize(new { SnapshotId = snapshot.Id }),
            CreatedAt = DateTimeOffset.UtcNow,
            OrganizationId = orgId ?? pc.OrganizationId
        };
        _context.QueuedAgentCommands.Add(cmd);

        await _context.SaveChangesAsync();
        return snapshot;
    }

    public async Task<List<DiagnosticSnapshot>> GetSnapshotsByClientPcIdAsync(Guid clientPcId, int limit = 20)
    {
        return await _context.DiagnosticSnapshots
            .Where(s => s.ClientPcId == clientPcId)
            .OrderByDescending(s => s.CapturedAtUtc)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<DiagnosticSnapshot?> GetSnapshotByIdAsync(Guid snapshotId)
    {
        return await _context.DiagnosticSnapshots
            .FirstOrDefaultAsync(s => s.Id == snapshotId);
    }
}
