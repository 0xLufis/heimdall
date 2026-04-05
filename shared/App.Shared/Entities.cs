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
/// Represents a physical production machine.
/// </summary>
public class Machine
{
    /// <summary>
    /// Gets or sets the unique identifier for the machine.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the organization that owns this machine.
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets a custom identifier for the machine, e.g., "Assembly Line 1".
    /// </summary>
    [Required, MaxLength(255)]
    public string CustomIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a handle to an object on a floor plan (e.g., DXF handle).
    /// </summary>
    public string? PinnedObjectHandle { get; set; }

    /// <summary>
    /// Gets or sets the list of <see cref="ClientPc"/> entities that control this machine.
    /// </summary>
    public List<ClientPc> ClientPcs { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of top-level hardware components associated with this machine.
    /// </summary>
    public List<InventoryComponent> Components { get; set; } = new();
}

/// <summary>
/// Represents flags for top-level inventory components, stored as JSONB.
/// </summary>
public class ComponentTopLevelFlags
{
    /// <summary>
    /// Gets or sets the type of the component (e.g., "controlling", "sensor", "vision").
    /// </summary>
    public string? Type { get; set; } // controlling, sensor, vision, screwing, coating, dispensing
    
    /// <summary>
    /// Gets or sets the owner of the component (e.g., "in-house", "outsourced", "mixed").
    /// </summary>
    public string? Owner { get; set; } // in-house, outsourced, mixed
    
    /// <summary>
    /// Gets or sets a dictionary for any additional custom flags.
    /// </summary>
    public Dictionary<string, object>? CustomFlags { get; set; }
}

/// <summary>
/// Represents a unified inventory component (Hardware, Software, or Peripheral).
/// Components form a recursive tree structure and can be linked laterally.
/// </summary>
public class InventoryComponent
{
    /// <summary>
    /// Gets or sets the unique identifier for the inventory component.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the full searchable name of the component.
    /// </summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user-friendly display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the quantity of this component.
    /// </summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the identifier of the entity that created this record.
    /// </summary>
    public string? EntityCreator { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the entity that last updated this record.
    /// </summary>
    public string? EntityUpdater { get; set; }

    /// <summary>
    /// Gets or sets the cost center associated with this component.
    /// </summary>
    public string? CostCenter { get; set; }

    /// <summary>
    /// Gets or sets the Organizational Unit (OU) for the cost center (e.g., logistics, engineering).
    /// </summary>
    public string? CostCenterOU { get; set; }

    /// <summary>
    /// Gets or sets the team responsible for this component.
    /// </summary>
    public string? Technology { get; set; }

    /// <summary>
    /// Gets or sets top-level flags for searching and categorization, stored as JSONB.
    /// </summary>
    public ComponentTopLevelFlags? TopLevelFlags { get; set; }

    /// <summary>
    /// Gets or sets a generic JSONB object for flexible component data.
    /// </summary>
    public JsonDocument? Data { get; set; }

    /// <summary>
    /// Gets or sets the manufacturer of the component.
    /// </summary>
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }

    /// <summary>
    /// Gets or sets the supplier of the component.
    /// </summary>
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    // --- Tree Structure ---
    /// <summary>
    /// Gets or sets the parent component ID.
    /// </summary>
    public Guid? ParentId { get; set; }
    public InventoryComponent? Parent { get; set; }
    public List<InventoryComponent> Children { get; set; } = new();

    // --- Lateral Links ---
    /// <summary>
    /// Gets or sets an optional link to another component at the same or different level.
    /// </summary>
    public Guid? LateralLinkId { get; set; }
    public InventoryComponent? LateralLink { get; set; }

    // --- Associations ---
    /// <summary>
    /// Gets or sets the ID of the Machine this component belongs to (optional).
    /// </summary>
    public Guid? MachineId { get; set; }
    public Machine? Machine { get; set; }

    /// <summary>
    /// Gets or sets the ID of the ClientPc this component belongs to (optional).
    /// </summary>
    public Guid? ClientPcId { get; set; }
    public ClientPc? ClientPc { get; set; }
}

// --- LEGACY ENTITIES (Satisfy Migrations) ---

public class ComponentTechnicalSpecs
{
    public List<string> Categories { get; set; } = new();
    public string? Resolution { get; set; }
    public string? FrameRate { get; set; }
    public string? InterfaceType { get; set; }
    public string? SensingDistance { get; set; }
    public string? OutputType { get; set; }
    public string? ConnectionType { get; set; }
    public double? TorqueMin { get; set; }
    public double? TorqueMax { get; set; }
    public int? MaxSpeed { get; set; }
    public string? FirmwareVersion { get; set; }
    public List<string>? SupportedProfiles { get; set; }
    public Dictionary<string, object>? ExtraAttributes { get; set; }
}

public class HardwareComponent
{
    public Guid Id { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ModelNumber { get; set; }
    public string? Revision { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal? CostInHUF { get; set; }
    public ComponentTechnicalSpecs? TechnicalSpecs { get; set; }
    public Guid? ParentId { get; set; }
    public HardwareComponent? Parent { get; set; }
    public List<HardwareComponent> Children { get; set; } = new();
}

public class SoftwareComponent
{
    public Guid Id { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Manufacturer? Manufacturer { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public decimal? CostInHUF { get; set; }
    public Guid? ParentId { get; set; }
    public SoftwareComponent? Parent { get; set; }
    public List<SoftwareComponent> Children { get; set; } = new();
}

public class HardwareConfig
{
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
}

public class SoftwareConfig
{
    public string OsVersion { get; set; } = string.Empty;
    public List<string> InstalledPackages { get; set; } = new();
}

public class Component
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ComponentType Type { get; set; }
    public string? Description { get; set; }
    public JsonDocument? AdminManagedFields { get; set; }
}

public enum ComponentType
{
    Hardware = 0,
    Software = 1
}

/// <summary>
/// Represents a Client PC that reports system data and controls production machines.
/// </summary>
public class ClientPc
{
    public Guid Id { get; set; }
    public string? OrganizationId { get; set; }

    [Required, MaxLength(255)]
    public string Hostname { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string MachineIdentifier { get; set; } = string.Empty;

    [Required, MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    public DateTimeOffset? LastOnline { get; set; }

    /// <summary>
    /// Gets or sets the list of machines controlled by this Client PC.
    /// </summary>
    public List<Machine> Machines { get; set; } = new();

    /// <summary>
    /// Gets or sets top-level inventory components (Hardware, Software, Peripherals).
    /// </summary>
    public List<InventoryComponent> Components { get; set; } = new();

    public JsonDocument? CustomDataPoints { get; set; }

    /// <summary>
    /// Gets or sets the free disk space information.
    /// </summary>
    public DiskSpaceInfo? FreeDiskSpace { get; set; }

    /// <summary>
    /// Gets or sets the resource monitoring configuration (sampling, retention).
    /// </summary>
    public ResourceMonitoringConfig? MonitoringConfig { get; set; }

    /// <summary>
    /// Gets or sets the calculated running averages for resources.
    /// </summary>
    public ResourceAverages? ResourceAverages { get; set; }

    /// <summary>
    /// Gets or sets custom alerting limits for the PC.
    /// </summary>
    public AlertingLimits? AlertingLimits { get; set; }

    public List<PcPredecessor> Predecessors { get; set; } = new();

    public Guid? FloorPlanId { get; set; }
    public string? PinnedObjectHandle { get; set; }
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

// --- AUTH ENTITIES (Better-Auth) ---

/// <summary>
/// Represents a user entity managed by the Better-Auth system.
/// Mapped to the "user" table in the "auth" schema.
/// </summary>
[Table("user", Schema = "auth")]
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
/// Mapped to the "session" table in the "auth" schema.
/// </summary>
[Table("session", Schema = "auth")]
public class AuthSession
{
    /// <summary>
    /// Gets or sets the unique identifier for the session.
    /// </summary>
    [Key]
    public string Id { get; set; } = string.Empty;    /// <summary>
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

    /// <summary>
    /// Represents an organization managed by the Better-Auth system.
    /// Mapped to the "organization" table in the "auth" schema.
    /// </summary>
    [Table("organization", Schema = "auth")]
    public class AuthOrganization
    {
        /// <summary>
        /// Gets or sets the unique identifier for the organization.
        /// </summary>
        [Key]
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the name of the organization.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the slug of the organization.
        /// </summary>
        public string? Slug { get; set; }
        /// <summary>
        /// Gets or sets the logo URL of the organization.
        /// </summary>
        public string? Logo { get; set; }
        /// <summary>
        /// Gets or sets the timestamp when the organization was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
        /// <summary>
        /// Gets or sets metadata associated with the organization.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// Gets or sets the list of members in this organization.
        /// </summary>
        public List<AuthMember> Members { get; set; } = new();
    }

    /// <summary>
    /// Represents a member of an organization managed by the Better-Auth system.
    /// Mapped to the "member" table in the "auth" schema.
    /// </summary>
    [Table("member", Schema = "auth")]
    public class AuthMember
    {
        /// <summary>
        /// Gets or sets the unique identifier for the membership.
        /// </summary>
        [Key]
        public string Id { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the foreign key to the <see cref="AuthOrganization"/> associated with this membership.
        /// </summary>
        public string OrganizationId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the <see cref="AuthOrganization"/> associated with this membership.
        /// </summary>
        public AuthOrganization Organization { get; set; } = null!;
        /// <summary>
        /// Gets or sets the foreign key to the <see cref="AuthUser"/> associated with this membership.
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the <see cref="AuthUser"/> associated with this membership.
        /// </summary>
        public AuthUser User { get; set; } = null!;
        /// <summary>
        /// Gets or sets the role of the user within the organization.
        /// </summary>
        public string Role { get; set; } = string.Empty;
        /// <summary>
        /// Gets or sets the timestamp when the membership was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
    }

    // --- JSONB POCOs ---

/// <summary>
/// Represents the hardware configuration of a Client PC, stored as a JSONB object.
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

public class DiskSpaceInfo
{
    public double TotalFreeGB { get; set; }
    public double OsDriveFreeGB { get; set; }
    public Dictionary<string, double> Drives { get; set; } = new();
}

public class ResourceMonitoringConfig
{
    public int SamplingIntervalSeconds { get; set; } = 60;
    public int RetentionDays { get; set; } = 30;
}

public class ResourceAverages
{
    public double CpuUsageAverage { get; set; }
    public double RamUsageAverage { get; set; }
    public double DiskIoAverage { get; set; }
    public DateTimeOffset LastCalculated { get; set; }
}

public class AlertingLimits
{
    public double CpuThreshold { get; set; } = 90.0;
    public double RamThreshold { get; set; } = 90.0;
    public double DiskFreeSpaceMinGB { get; set; } = 10.0;
}
