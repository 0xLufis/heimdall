//using System;
//using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace App.Shared.Entities;

// --- DOMAIN ENTITIES ---

public class Manufacturer
{
    public Guid Id { get; set; }
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? SupportContact { get; set; }
}

public class Supplier
{
    public Guid Id { get; set; }
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
}

public class Machine
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string CustomIdentifier { get; set; } = string.Empty;

    // JSONB Columns
    public JsonDocument? HwComponents { get; set; }
    public JsonDocument? SwComponents { get; set; }

    public string? PinnedObjectHandle { get; set; } // Reference to a DXF handle

    // Relationship: Many-to-many with Client PCs
    public List<ClientPc> ClientPcs { get; set; } = new();
}

public class SoftwareComponent
{
    public Guid Id { get; set; }
    
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? Version { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public string? SerialNumber { get; set; }
    
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    
    public decimal? CostInHUF { get; set; }
}

public class HardwareComponent
{
    public Guid Id { get; set; }
    
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public string? ModelNumber { get; set; }
    public string? Revision { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public string? SerialNumber { get; set; }
    
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    
    public decimal? CostInHUF { get; set; }

    // Technical Specifications (JSONB)
    public ComponentTechnicalSpecs? TechnicalSpecs { get; set; }
}

public class ComponentTechnicalSpecs
{
    public string? Category { get; set; } // Sensor, Screwdriver, Controller, etc.
    
    // Vision Sensors (Keyence etc)
    public string? Resolution { get; set; }
    public string? FrameRate { get; set; }
    public string? InterfaceType { get; set; } // Ethernet/IP, Profinet
    
    // Proximity Sensors
    public string? SensingDistance { get; set; }
    public string? OutputType { get; set; } // PNP/NPN, NO/NC
    public string? ConnectionType { get; set; } // M8, M12
    
    // Screwdrivers (Deprag etc)
    public double? TorqueMin { get; set; }
    public double? TorqueMax { get; set; }
    public int? MaxSpeed { get; set; }
    
    // Controllers (AST etc)
    public string? FirmwareVersion { get; set; }
    public List<string>? SupportedProfiles { get; set; }
    
    // Generic
    public Dictionary<string, object>? ExtraAttributes { get; set; }
}

public class UserRole
{
    public Guid Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public List<string> Privileges { get; set; } = new();
}

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

    // Relationships
    public List<Machine> Machines { get; set; } = new();

    // JSONB Columns
    public HardwareConfig HardwareConfig { get; set; } = new();
    public SoftwareConfig SoftwareConfig { get; set; } = new();

    // Dynamic user-defined data points (Flags, WMIC data, FS data)
    public JsonDocument? CustomDataPoints { get; set; }

    // Predecessors stored as a JSONB array of objects
    public List<PcPredecessor> Predecessors { get; set; } = new();

    // Floor Plan Pinning
    public Guid? FloorPlanId { get; set; }
    public string? PinnedObjectHandle { get; set; } // Reference to a DXF handle (e.g., "1A2B")
}

public class FloorPlan
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string SvgContent { get; set; } = string.Empty;

    // List of extractable anchors from the DXF (Blocks, Named Objects)
    public List<FloorPlanAnchor> Anchors { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class FloorPlanAnchor
{
    public string Handle { get; set; } = string.Empty; // Persistent DXF Handle
    public string Name { get; set; } = string.Empty;   // Block Name or Attribute value
    public double? X { get; set; }                     // Optional Centroid for UI centering
    public double? Y { get; set; }
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

// --- AUTH ENTITIES (Better-Auth) ---

[Table("user", Schema = "heimdall_dev_db")]
public class AuthUser
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string? Image { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? Role { get; set; }
    public string? Username { get; set; }
    public bool? Banned { get; set; }
    public string? BanReason { get; set; }
    public DateTimeOffset? BanExpiresAt { get; set; }

    public List<AuthSession> Sessions { get; set; } = new();
}

[Table("session", Schema = "heimdall_dev_db")]
public class AuthSession
{
    [Key]
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string UserId { get; set; } = string.Empty;
    public AuthUser User { get; set; } = null!;
    public string? ActiveOrganizationId { get; set; }
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

