//using System;
//using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace App.Shared.Entities;

// --- DOMAIN ENTITIES ---

/// <summary>
/// Represents a manufacturer of hardware or software components.
/// </summary>
public class Manufacturer
{
    /// <summary>
    /// Gets or sets the unique identifier for the manufacturer.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Gets or sets the name of the manufacturer. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the official website URL of the manufacturer.
    /// </summary>
    public string? Website { get; set; }
    /// <summary>
    /// Gets or sets the contact information for manufacturer support.
    /// </summary>
    public string? SupportContact { get; set; }
}

/// <summary>
/// Represents a supplier of hardware or software components.
/// </summary>
public class Supplier
{
    /// <summary>
    /// Gets or sets the unique identifier for the supplier.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Gets or sets the name of the supplier. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the official website URL of the supplier.
    /// </summary>
    public string? Website { get; set; }
    /// <summary>
    /// Gets or sets the name of the primary contact person at the supplier.
    /// </summary>
    public string? ContactPerson { get; set; }
    /// <summary>
    /// Gets or sets the email address of the contact person at the supplier.
    /// </summary>
    public string? Email { get; set; }
}

/// <summary>
/// Represents a physical machine that can be associated with Client PCs.
/// </summary>
public class Machine
{
    /// <summary>
    /// Gets or sets the unique identifier for the machine.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Gets or sets a custom identifier for the machine, e.g., "Assembly Line 1". This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string CustomIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hardware components of the machine as a JSONB document.
    /// </summary>
    public JsonDocument? HwComponents { get; set; }
    /// <summary>
    /// Gets or sets the software components of the machine as a JSONB document.
    /// </summary>
    public JsonDocument? SwComponents { get; set; }

    /// <summary>
    /// Gets or sets a handle to an object on a floor plan (e.g., DXF handle) to pinpoint the machine's location.
    /// </summary>
    public string? PinnedObjectHandle { get; set; } // Reference to a DXF handle

    /// <summary>
    /// Gets or sets the list of <see cref="ClientPc"/> entities associated with this machine (many-to-many relationship).
    /// </summary>
    public List<ClientPc> ClientPcs { get; set; } = new();
}

/// <summary>
/// Represents a software component in the inventory.
/// </summary>
public class SoftwareComponent
{
    /// <summary>
    /// Gets or sets the unique identifier for the software component.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Gets or sets the foreign key to the <see cref="Manufacturer"/> of the software.
    /// </summary>
    public Guid? ManufacturerId { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="Manufacturer"/> of the software.
    /// </summary>
    public Manufacturer? Manufacturer { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the software component. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the version of the software.
    /// </summary>
    public string? Version { get; set; }
    /// <summary>
    /// Gets or sets a description of the software component.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Gets or sets the date the software was purchased.
    /// </summary>
    public DateTimeOffset? PurchaseDate { get; set; }
    /// <summary>
    /// Gets or sets the serial number of the software license.
    /// </summary>
    public string? SerialNumber { get; set; }
    
    /// <summary>
    /// Gets or sets the foreign key to the <see cref="Supplier"/> of the software.
    /// </summary>
    public Guid? SupplierId { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="Supplier"/> of the software.
    /// </summary>
    public Supplier? Supplier { get; set; }
    
    /// <summary>
    /// Gets or sets the cost of the software in Hungarian Forints (HUF).
    /// </summary>
    public decimal? CostInHUF { get; set; }

    // --- Recursive Relationship ---
    /// <summary>
    /// Gets or sets the foreign key to the parent software component.
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// Gets or sets the parent software component.
    /// </summary>
    public SoftwareComponent? Parent { get; set; }
    /// <summary>
    /// Gets or sets the list of child software components.
    /// </summary>
    public List<SoftwareComponent> Children { get; set; } = new();
}

/// <summary>
/// Represents a hardware component in the inventory.
/// </summary>
public class HardwareComponent
{
    /// <summary>
    /// Gets or sets the unique identifier for the hardware component.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Gets or sets the foreign key to the <see cref="Manufacturer"/> of the hardware.
    /// </summary>
    public Guid? ManufacturerId { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="Manufacturer"/> of the hardware.
    /// </summary>
    public Manufacturer? Manufacturer { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the hardware component. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the model number of the hardware.
    /// </summary>
    public string? ModelNumber { get; set; }
    /// <summary>
    /// Gets or sets the revision of the hardware.
    /// </summary>
    public string? Revision { get; set; }
    /// <summary>
    /// Gets or sets a description of the hardware component.
    /// </summary>
    public string? Description { get; set; }
    /// <summary>
    /// Gets or sets the date the hardware was purchased.
    /// </summary>
    public DateTimeOffset? PurchaseDate { get; set; }
    /// <summary>
    /// Gets or sets the serial number of the hardware.
    /// </summary>
    public string? SerialNumber { get; set; }
    
    /// <summary>
    /// Gets or sets the foreign key to the <see cref="Supplier"/> of the hardware.
    /// </summary>
    public Guid? SupplierId { get; set; }
    /// <summary>
    /// Gets or sets the <see cref="Supplier"/> of the hardware.
    /// </summary>
    public Supplier? Supplier { get; set; }
    
    /// <summary>
    /// Gets or sets the cost of the hardware in Hungarian Forints (HUF).
    /// </summary>
    public decimal? CostInHUF { get; set; }

    /// <summary>
    /// Gets or sets the technical specifications of the hardware component as a strongly-typed JSONB object.
    /// </summary>
    public ComponentTechnicalSpecs? TechnicalSpecs { get; set; }

    // --- Recursive Relationship ---
    /// <summary>
    /// Gets or sets the foreign key to the parent hardware component.
    /// </summary>
    public Guid? ParentId { get; set; }
    /// <summary>
    /// Gets or sets the parent hardware component.
    /// </summary>
    public HardwareComponent? Parent { get; set; }
    /// <summary>
    /// Gets or sets the list of child hardware components.
    /// </summary>
    public List<HardwareComponent> Children { get; set; } = new();
}

/// <summary>
/// Represents technical specifications for a component, stored as a JSONB object.
/// </summary>
public class ComponentTechnicalSpecs
{
    /// <summary>
    /// Gets or sets the categories of the component (e.g., "Sensor", "Screwdriver", "Controller").
    /// </summary>
    public List<string> Categories { get; set; } = new();
    
    // Vision Sensors (Keyence etc)
    /// <summary>
    /// Gets or sets the resolution for vision sensors.
    /// </summary>
    public string? Resolution { get; set; }
    /// <summary>
    /// Gets or sets the frame rate for vision sensors.
    /// </summary>
    public string? FrameRate { get; set; }
    /// <summary>
    /// Gets or sets the interface type for vision sensors (e.g., "Ethernet/IP", "Profinet").
    /// </summary>
    public string? InterfaceType { get; set; } // Ethernet/IP, Profinet
    
    // Proximity Sensors
    /// <summary>
    /// Gets or sets the sensing distance for proximity sensors.
    /// </summary>
    public string? SensingDistance { get; set; }
    /// <summary>
    /// Gets or sets the output type for proximity sensors (e.g., "PNP/NPN", "NO/NC").
    /// </summary>
    public string? OutputType { get; set; } // PNP/NPN, NO/NC
    /// <summary>
    /// Gets or sets the connection type for proximity sensors (e.g., "M8", "M12").
    /// </summary>
    public string? ConnectionType { get; set; } // M8, M12
    
    // Screwdrivers (Deprag etc)
    /// <summary>
    /// Gets or sets the minimum torque for screwdrivers.
    /// </summary>
    public double? TorqueMin { get; set; }
    /// <summary>
    /// Gets or sets the maximum torque for screwdrivers.
    /// </summary>
    public double? TorqueMax { get; set; }
    /// <summary>
    /// Gets or sets the maximum speed for screwdrivers.
    /// </summary>
    public int? MaxSpeed { get; set; }
    
    // Controllers (AST etc)
    /// <summary>
    /// Gets or sets the firmware version for controllers.
    /// </summary>
    public string? FirmwareVersion { get; set; }
    /// <summary>
    /// Gets or sets a list of supported profiles for controllers.
    /// </summary>
    public List<string>? SupportedProfiles { get; set; }
    
    // Generic
    /// <summary>
    /// Gets or sets a dictionary of extra, generic attributes for any component type.
    /// </summary>
    public Dictionary<string, object>? ExtraAttributes { get; set; }
}

/// <summary>
/// Represents a user role with a name and associated privileges.
/// </summary>
public class UserRole
{
    /// <summary>
    /// Gets or sets the unique identifier for the user role.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// Gets or sets the name of the role (e.g., "admin", "engineer"). This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets a list of privileges associated with this role.
    /// </summary>
    public List<string> Privileges { get; set; } = new();
}

/// <summary>
/// Represents a Client PC, collecting system information and linking to physical machines.
/// </summary>
public class ClientPc
{
    /// <summary>
    /// Gets or sets the unique identifier for the client PC.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the hostname of the client PC. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a unique identifier for the machine that the client PC reports itself as.
    /// This is typically a UUID generated by the agent. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string MachineIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the MAC address of the client PC's primary network interface. This field is required and has a maximum length of 17 characters.
    /// </summary>
    [Required]
    [MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp of the last time the client PC reported its status.
    /// </summary>
    public DateTimeOffset? LastOnline { get; set; }

    // Relationships
    /// <summary>
    /// Gets or sets the list of <see cref="Machine"/> entities this Client PC is associated with (many-to-many relationship).
    /// </summary>
    public List<Machine> Machines { get; set; } = new();

    // JSONB Columns
    /// <summary>
    /// Gets or sets the hardware configuration of the client PC as a strongly-typed JSONB object.
    /// </summary>
    public HardwareConfig HardwareConfig { get; set; } = new();
    /// <summary>
    /// Gets or sets the software configuration of the client PC as a strongly-typed JSONB object.
    /// </summary>
    public SoftwareConfig SoftwareConfig { get; set; } = new();

    /// <summary>
    /// Gets or sets dynamic user-defined data points for the client PC, stored as a JSONB document.
    /// This can include flags, WMIC data, filesystem data, etc.
    /// </summary>
    public JsonDocument? CustomDataPoints { get; set; }

    /// <summary>
    /// Gets or sets a list of predecessor PCs, stored as a JSONB array of <see cref="PcPredecessor"/> objects.
    /// </summary>
    public List<PcPredecessor> Predecessors { get; set; } = new();

    // Floor Plan Pinning
    /// <summary>
    /// Gets or sets the foreign key to the <see cref="FloorPlan"/> this Client PC is pinned to.
    /// </summary>
    public Guid? FloorPlanId { get; set; }
    /// <summary>
    /// Gets or sets a handle to an object on a floor plan (e.g., DXF handle) to pinpoint the Client PC's location.
    /// </summary>
    public string? PinnedObjectHandle { get; set; } // Reference to a DXF handle (e.g., "1A2B")
}

/// <summary>
/// Represents a floor plan (e.g., a factory layout) onto which machines can be pinned.
/// </summary>
public class FloorPlan
{
    /// <summary>
    /// Gets or sets the unique identifier for the floor plan.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the floor plan. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SVG content representing the floor plan. This field is required.
    /// </summary>
    [Required]
    public string SvgContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a list of extractable anchors (e.g., DXF Blocks, Named Objects) from the floor plan.
    /// </summary>
    public List<FloorPlanAnchor> Anchors { get; set; } = new();

    /// <summary>
    /// Gets or sets the creation timestamp of the floor plan. Defaults to UTC now.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents an anchor point on a floor plan, typically corresponding to a DXF entity.
/// </summary>
public class FloorPlanAnchor
{
    /// <summary>
    /// Gets or sets the persistent DXF Handle of the anchor object.
    /// </summary>
    public string Handle { get; set; } = string.Empty; // Persistent DXF Handle
    /// <summary>
    /// Gets or sets the name of the anchor (e.g., Block Name or Attribute value).
    /// </summary>
    public string Name { get; set; } = string.Empty;   // Block Name or Attribute value
    /// <summary>
    /// Gets or sets the optional X-coordinate of the anchor's centroid for UI centering.
    /// </summary>
    public double? X { get; set; }                     // Optional Centroid for UI centering
    /// <summary>
    /// Gets or sets the optional Y-coordinate of the anchor's centroid for UI centering.
    /// </summary>
    public double? Y { get; set; }
}

/// <summary>
/// Represents a generic component with a name and type.
/// </summary>
public class Component
{
    /// <summary>
    /// Gets or sets the unique identifier for the component.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the component. This field is required and has a maximum length of 255 characters.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of the component (Hardware or Software).
    /// </summary>
    [Required]
    public ComponentType Type { get; set; }

    /// <summary>
    /// Gets or sets a description of the component.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets consistent fields managed by administrators, stored as a JSONB document
    /// to allow flexible schema evolution.
    /// </summary>
    public JsonDocument? AdminManagedFields { get; set; }
}

/// <summary>
/// Defines the type of a component.
/// </summary>
public enum ComponentType
{
    /// <summary>
    /// Indicates a hardware component.
    /// </summary>
    Hardware = 0,
    /// <summary>
    /// Indicates a software component.
    /// </summary>
    Software = 1
}

// --- AUTH ENTITIES (Better-Auth) ---

/// <summary>
/// Represents a user entity managed by the Better-Auth system.
/// Mapped to the "user" table in the "heimdall_dev_db" schema.
/// </summary>
[Table("user", Schema = "heimdall_dev_db")]
public class AuthUser
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the display name of the user.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the email address of the user.
    /// </summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets a value indicating whether the user's email address has been verified.
    /// </summary>
    public bool EmailVerified { get; set; }
    /// <summary>
    /// Gets or sets the URL to the user's profile image.
    /// </summary>
    public string? Image { get; set; }
    /// <summary>
    /// Gets or sets the timestamp when the user account was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// Gets or sets the timestamp when the user account was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>
    /// Gets or sets the role of the user within the system.
    /// </summary>
    public string? Role { get; set; }
    /// <summary>
    /// Gets or sets the username of the user.
    /// </summary>
    public string? Username { get; set; }
    /// <summary>
    /// Gets or sets a value indicating whether the user is banned.
    /// </summary>
    public bool? Banned { get; set; }
    /// <summary>
    /// Gets or sets the reason for the user's ban.
    /// </summary>
    public string? BanReason { get; set; }
    /// <summary>
    /// Gets or sets the timestamp when the user's ban expires.
    /// </summary>
    public DateTimeOffset? BanExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the list of active authentication sessions for this user.
    /// </summary>
    public List<AuthSession> Sessions { get; set; } = new();
}

/// <summary>
/// Represents an authentication session for a user, managed by the Better-Auth system.
/// Mapped to the "session" table in the "heimdall_dev_db" schema.
/// </summary>
[Table("session", Schema = "heimdall_dev_db")]
public class AuthSession
{
    /// <summary>
    /// Gets or sets the unique identifier for the session.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the timestamp when the session expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>
    /// Gets or sets the authentication token associated with this session.
    /// </summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the timestamp when the session was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>
    /// Gets or sets the timestamp when the session was last updated.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
    /// <summary>
    /// Gets or sets the IP address from which the session originated.
    /// </summary>
    public string? IpAddress { get; set; }
    /// <summary>
    /// Gets or sets the User-Agent string of the client that created the session.
    /// </summary>
    public string? UserAgent { get; set; }
    /// <summary>
    /// Gets or sets the foreign key to the <see cref="AuthUser"/> associated with this session.
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the <see cref="AuthUser"/> associated with this session.
    /// </summary>
    public AuthUser User { get; set; } = null!;
    /// <summary>
    /// Gets or sets the ID of the active organization for this session.
    /// </summary>
    public string? ActiveOrganizationId { get; set; }
}

// --- JSONB POCOs ---

/// <summary>
/// Represents the hardware configuration of a Client PC, stored as a JSONB object.
/// </summary>
public class HardwareConfig
{
    /// <summary>
    /// Gets or sets the CPU model of the client PC.
    /// </summary>
    public string Cpu { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the RAM size of the client PC.
    /// </summary>
    public string Ram { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the storage capacity of the client PC.
    /// </summary>
    public string Storage { get; set; } = string.Empty;
    // Add other predictable HW properties here
}

/// <summary>
/// Represents the software configuration of a Client PC, stored as a JSONB object.
/// </summary>
public class SoftwareConfig
{
    /// <summary>
    /// Gets or sets the operating system version of the client PC.
    /// </summary>
    public string OsVersion { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets a list of installed software packages on the client PC.
    /// </summary>
    public List<string> InstalledPackages { get; set; } = new();
    // Add other predictable SW properties here
}

/// <summary>
/// Represents a predecessor Client PC, used for tracking changes or replacements.
/// Stored as an item in a JSONB array within <see cref="ClientPc.Predecessors"/>.
/// </summary>
public class PcPredecessor
{
    /// <summary>
    /// Gets or sets the hostname of the predecessor PC.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;
    /// <summary>
    /// Gets or sets the serial number of the predecessor PC.
    /// </summary>
    public string SerialNumber { get; set; } = string.Empty;
}
