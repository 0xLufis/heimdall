//using System;
//using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace App.Shared.Entities;

// --- DOMAIN ENTITIES ---

public class ClientPc
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Hostname { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string MachineIdentifier { get; set; } = string.Empty;

    [Required]
    [MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    public DateTimeOffset? LastOnline { get; set; }

    // JSONB Columns
    public HardwareConfig HardwareConfig { get; set; } = new();
    public SoftwareConfig SoftwareConfig { get; set; } = new();

    // Dynamic user-defined data points (Flags, WMIC data, FS data)
    public JsonDocument? CustomDataPoints { get; set; }

    // Predecessors stored as a JSONB array of objects
    public List<PcPredecessor> Predecessors { get; set; } = new();
}

public class Component
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ComponentType Type { get; set; }

    public string? Description { get; set; }

    // Consistent fields managed by admins stored as JSONB to allow flexible schema evolution
    public JsonDocument? AdminManagedFields { get; set; }
}

public enum ComponentType
{
    Hardware = 0,
    Software = 1
}

// --- JSONB POCOs ---

public class HardwareConfig
{
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    // Add other predictable HW properties here
}

public class SoftwareConfig
{
    public string OsVersion { get; set; } = string.Empty;
    public List<string> InstalledPackages { get; set; } = new();
    // Add other predictable SW properties here
}

public class PcPredecessor
{
    public string Hostname { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
}

