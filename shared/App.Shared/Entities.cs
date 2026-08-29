using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace App.Shared.Entities;

// --- SHARED RESPONSIBILITY ---

/// <summary>
/// Represents an engineering team responsible for certain assets.
/// </summary>
public partial class ResponsibleTeam
{
    public Guid Id { get; set; }
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty; // Mechanical, Controls, Vision, Dispensing
    public string? Description { get; set; }
    
    public List<BaseInventoryItem> ManagedItems { get; set; } = new();
}

// --- CORE ABSTRACTION (The "Abstract Class") ---

/// <summary>
/// The base class for all inventory items, following an Object-Oriented Programming (OOP) approach.
/// Provides common attributes such as identity, financial data, and hierarchical relationships.
/// </summary>
public abstract partial class BaseInventoryItem
{
    /// <summary>Unique identifier for the inventory item.</summary>
    public Guid Id { get; set; }

    /// <summary>The name of the asset.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional user-friendly display name.</summary>
    public string? DisplayName { get; set; }
    
    /// <summary>Identifier for the organization unit that owns this asset.</summary>
    public string? OrganizationId { get; set; }
    
    /// <summary>The financial cost of the asset in Hungarian Forint (HUF).</summary>
    public decimal? CostInHUF { get; set; }

    /// <summary>The date when the asset was purchased.</summary>
    public DateTimeOffset? PurchaseDate { get; set; }

    /// <summary>The hardware or license serial number.</summary>
    public string? SerialNumber { get; set; }

    /// <summary>The ID of the manufacturer of the asset.</summary>
    public Guid? ManufacturerId { get; set; }
    /// <summary>Navigation property for the manufacturer.</summary>
    public Manufacturer? Manufacturer { get; set; }

    /// <summary>The ID of the supplier of the asset.</summary>
    public Guid? SupplierId { get; set; }
    /// <summary>Navigation property for the supplier.</summary>
    public Supplier? Supplier { get; set; }

    /// <summary>The ID of the Client PC this asset is associated with (e.g., if it's a component of a PC).</summary>
    public Guid? ClientPcId { get; set; }
    /// <summary>Navigation property for the associated Client PC.</summary>
    public ClientPc? ClientPc { get; set; }

    /// <summary>List of engineering teams responsible for this specific asset.</summary>
    public List<ResponsibleTeam> ResponsibleTeams { get; set; } = new();

    /// <summary>The ID of the parent inventory item (for hierarchical tree structures).</summary>
    public Guid? ParentId { get; set; }
    /// <summary>Navigation property for the parent item.</summary>
    public BaseInventoryItem? Parent { get; set; }
    /// <summary>List of child items nested under this asset.</summary>
    public List<BaseInventoryItem> Children { get; set; } = new();

    /// <summary>Flexible JSONB metadata for storing domain-specific attributes (e.g., CPU, RAM, Version).</summary>
    public JsonDocument? Metadata { get; set; }

    /// <summary>Helper property for identifying the concrete class name in UI layers.</summary>
    [NotMapped]
    public virtual string ItemType => this.GetType().Name;
}

// --- CONCRETE IMPLEMENTATIONS (The "Different Models") ---

/// <summary>
/// Represents a Production Station or Process Node on the factory floor.
/// </summary>
[Table("stations")]
public partial class Machine : BaseInventoryItem
{
    /// <summary>A custom, user-defined identifier for the station (e.g., "LINE-A-OP10").</summary>
    [Required, MaxLength(255)]
    public string CustomIdentifier { get; set; } = string.Empty;

    /// <summary>The CAD/DXF object handle for mapping this station to a spatial layout.</summary>
    public string? PinnedObjectHandle { get; set; }
    
    /// <summary>List of Client PCs that control or monitor this station.</summary>
    public List<ClientPc> Controllers { get; set; } = new();
}

/// <summary>
/// Represents a physical PC/Terminal on the factory floor.
/// Acts as a standalone entity that can control machines and contain inventory items.
/// </summary>
[Table("client_pcs")]
public partial class ClientPc
{
    /// <summary>Unique identifier for the Client PC.</summary>
    public Guid Id { get; set; }
    
    /// <summary>The name given to the Client PC.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>The physical MAC address of the network interface.</summary>
    [Required, MaxLength(17)]
    public string MacAddress { get; set; } = string.Empty;

    /// <summary>The last reported IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>A unique hardware-based machine identifier.</summary>
    public string? MachineIdentifier { get; set; }

    /// <summary>The network hostname of the device.</summary>
    public string? Hostname { get; set; } 

    /// <summary>Timestamp of the last successful heartbeat communication.</summary>
    public DateTimeOffset? LastOnline { get; set; }

    /// <summary>The CAD/DXF object handle for mapping this PC to a spatial layout.</summary>
    public string? PinnedObjectHandle { get; set; }

    /// <summary>List of production stations controlled by this PC.</summary>
    public List<Machine> ControlledMachines { get; set; } = new();

    /// <summary>List of internal hardware and software components assigned to this PC.</summary>
    public List<BaseInventoryItem> InventoryItems { get; set; } = new();

    /// <summary>List of engineering teams responsible for the maintenance of this PC.</summary>
    public List<ResponsibleTeam> ResponsibleTeams { get; set; } = new();

    /// <summary>Commands queued to be processed by the Agent on this PC.</summary>
    public List<QueuedAgentCommand> PendingCommands { get; set; } = new();

    /// <summary>Audit events reported by the Agent running on this PC.</summary>
    public List<AgentEvent> Events { get; set; } = new();

    /// <summary>Real-time disk space telemetry (JSONB).</summary>
    public DiskSpaceInfo? FreeDiskSpace { get; set; }

    /// <summary>Abstract system metadata (OS, IP, Security Level) that are properties of the node, not physical assets.</summary>
    public JsonDocument? SystemMetadata { get; set; }

    /// <summary>Configuration settings for resource monitoring on this node (JSONB).</summary>
    public ResourceMonitoringConfig? MonitoringConfig { get; set; }

    /// <summary>Aggregated CPU and RAM usage averages (JSONB).</summary>
    public ResourceAverages? ResourceAverages { get; set; }

    /// <summary>Thresholds for system health alerting (JSONB).</summary>
    public AlertingLimits? AlertingLimits { get; set; }
}

/// <summary>
/// Represents a security or system event reported by an Agent.
/// </summary>
[Table("agent_events")]
public class AgentEvent
{
    /// <summary>Unique identifier for the event.</summary>
    public Guid Id { get; set; }
    /// <summary>The ID of the Client PC that reported the event.</summary>
    public Guid ClientPcId { get; set; }
    /// <summary>The source of the event (e.g., "System", "Agent", "Security").</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>The detailed event message.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>The severity level (Information, Warning, Error, Critical).</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>The UTC timestamp when the event occurred.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a command queued to be sent to a specific Agent.
/// </summary>
[Table("queued_agent_commands")]
public class QueuedAgentCommand
{
    /// <summary>Unique identifier for the command.</summary>
    public Guid Id { get; set; }
    /// <summary>The target Client PC for the command.</summary>
    public Guid ClientPcId { get; set; }
    /// <summary>The type of command (e.g., "UPDATE_CONFIG", "RESTART").</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>The command payload (usually a JSON string).</summary>
    public string Payload { get; set; } = string.Empty;
    /// <summary>Optional cryptographic signature for command validation.</summary>
    public string? Signature { get; set; }
    /// <summary>Timestamp when the command was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>Whether the command has been retrieved and processed by the Agent.</summary>
    public bool IsProcessed { get; set; }
}

/// <summary>
/// Represents a maintenance ticket for equipment, machines, or client PCs.
/// </summary>
[Table("maintenance_tickets")]
public class MaintenanceTicket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Required, MaxLength(50)]
    public string Status { get; set; } = "Open";
    [Required, MaxLength(50)]
    public string Priority { get; set; } = "Medium";
    public Guid? MachineId { get; set; }
    public Machine? Machine { get; set; }
    public Guid? ClientPcId { get; set; }
    public ClientPc? ClientPc { get; set; }
    public Guid? AssetId { get; set; }
    public BaseInventoryItem? Asset { get; set; }
    public string? AssignedTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

/// <summary>
/// Represents station-level physical equipment (e.g., Valves, Sensors, Motors).
/// </summary>
[Table("hardware_assets")]
public partial class HardwareComponent : BaseInventoryItem
{
    /// <summary>The hardware revision version.</summary>
    public string? Revision { get; set; }
    /// <summary>The specific manufacturer model number.</summary>
    public string? ModelNumber { get; set; }

    /// <summary>List of software/firmware components associated with this hardware.</summary>
    public List<SoftwareComponent> Firmware { get; set; } = new();
}

/// <summary>
/// Represents logical assets such as PLC Programs, Software Licenses, or Firmware.
/// </summary>
[Table("software_assets")]
public partial class SoftwareComponent : BaseInventoryItem
{
    /// <summary>The software version string.</summary>
    public string? Version { get; set; }
    /// <summary>The license key or activation code.</summary>
    public string? LicenseKey { get; set; }
}

/// <summary>
/// Represents internal components of a Client PC (e.g., RAM sticks, CPUs, Storage Drives).
/// </summary>
[Table("pc_hardware")]
public partial class PcHardware : BaseInventoryItem
{
    /// <summary>The capacity of the component (e.g., "16GB", "1TB").</summary>
    public string? Capacity { get; set; }
    /// <summary>The specific type/standard (e.g., "DDR4", "NVMe").</summary>
    public string? Type { get; set; }
}

// --- REFERENCE ENTITIES ---

/// <summary>
/// Represents an Original Equipment Manufacturer (OEM).
/// </summary>
public partial class Manufacturer
{
    /// <summary>Unique identifier for the manufacturer.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the company.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>Official website URL.</summary>
    public string? Website { get; set; }
    /// <summary>Contact information for technical support.</summary>
    public string? SupportContact { get; set; }
}

/// <summary>
/// Represents a business partner that supplies assets.
/// </summary>
public partial class Supplier
{
    /// <summary>Unique identifier for the supplier.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the supplier company.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>Official website URL.</summary>
    public string? Website { get; set; }
    /// <summary>Primary contact person.</summary>
    public string? ContactPerson { get; set; }
    /// <summary>Contact email address.</summary>
    public string? Email { get; set; }
}

// --- LAYOUT & VISUALS ---

/// <summary>
/// Represents a physical factory floor plan, stored as a DXF/SVG.
/// </summary>
public partial class FloorPlan
{
    /// <summary>Unique identifier for the floor plan.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the floor or building.</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>The SVG content for rendering the layout in the browser.</summary>
    [Required]
    public string SvgContent { get; set; } = string.Empty;
    /// <summary>List of predefined anchor points within the layout (JSONB).</summary>
    public List<FloorPlanAnchor> Anchors { get; set; } = new();
    /// <summary>Timestamp when the plan was uploaded.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Represents a coordinate-based anchor point on a floor plan.
/// </summary>
public partial class FloorPlanAnchor
{
    /// <summary>The CAD object handle associated with this anchor.</summary>
    public string Handle { get; set; } = string.Empty;
    /// <summary>A descriptive name for the anchor point.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Horizontal coordinate.</summary>
    public double? X { get; set; }
    /// <summary>Vertical coordinate.</summary>
    public double? Y { get; set; }
}

// --- SYSTEM POCOs (JSONB Mapping) ---

/// <summary>
/// Telemetry data for disk space usage on a node.
/// </summary>
public class DiskSpaceInfo
{
    /// <summary>Total free space across all drives in GB.</summary>
    public double TotalFreeGB { get; set; }
    /// <summary>Available space on the OS (primary) drive in GB.</summary>
    public double OsDriveFreeGB { get; set; }
    /// <summary>Dictionary mapping drive labels to their free space in GB.</summary>
    public Dictionary<string, double> Drives { get; set; } = new();
}

/// <summary>
/// Configuration for the monitoring agent's resource collection behavior.
/// </summary>
public class ResourceMonitoringConfig
{
    /// <summary>How often the agent should sample resource usage (in seconds).</summary>
    public int SamplingIntervalSeconds { get; set; } = 60;
}

/// <summary>
/// Aggregated resource usage telemetry.
/// </summary>
public class ResourceAverages
{
    /// <summary>Average CPU usage percentage over the last sampling period.</summary>
    public double CpuUsageAverage { get; set; }
    /// <summary>Average RAM usage percentage over the last sampling period.</summary>
    public double RamUsageAverage { get; set; }
}

/// <summary>
/// Thresholds for triggering system health alerts.
/// </summary>
public class AlertingLimits
{
    /// <summary>CPU usage percentage threshold for triggering an alert.</summary>
    public double CpuThreshold { get; set; } = 90.0;
}

// --- AUTH (Better-Auth) ---

/// <summary>
/// Represents a user role with specific access privileges.
/// </summary>
public partial class UserRole
{
    /// <summary>Unique identifier for the role.</summary>
    public Guid Id { get; set; }
    /// <summary>The name of the role (e.g., "admin", "operator").</summary>
    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    /// <summary>JSON document containing the fine-grained permissions for this role.</summary>
    public JsonDocument? Privileges { get; set; }
}

/// <summary>
/// Represents a user within the system, managed by the Better-Auth framework.
/// </summary>
[Table("user", Schema = "auth")]
public class AuthUser
{
    /// <summary>Unique identifier for the user (Better-Auth ID).</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>Full name of the user.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Email address for authentication and notifications.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>The assigned system-wide role.</summary>
    public string? Role { get; set; }
    /// <summary>List of organizations this user is a member of.</summary>
    public List<AuthMember> Members { get; set; } = new();
}

/// <summary>
/// Represents an active authentication session.
/// </summary>
[Table("session", Schema = "auth")]
public class AuthSession
{
    /// <summary>Unique identifier for the session.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>The session token used for authentication.</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>Timestamp when the session will expire.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>The ID of the user associated with this session.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Navigation property for the user.</summary>
    public AuthUser User { get; set; } = null!;
    /// <summary>The organization currently selected in this session.</summary>
    public string? ActiveOrganizationId { get; set; }
}

/// <summary>
/// Represents a tenant/organization unit within the system.
/// </summary>
[Table("organization", Schema = "auth")]
public class AuthOrganization
{
    /// <summary>Unique identifier for the organization.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>The name of the organization.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>List of members belonging to this organization.</summary>
    public List<AuthMember> Members { get; set; } = new();
}

/// <summary>
/// Represents the membership relationship between a user and an organization.
/// </summary>
[Table("member", Schema = "auth")]
public class AuthMember
{
    /// <summary>Unique identifier for the membership record.</summary>
    [Key]
    public string Id { get; set; } = string.Empty;
    /// <summary>The ID of the organization.</summary>
    public string OrganizationId { get; set; } = string.Empty;
    /// <summary>Navigation property for the organization.</summary>
    public AuthOrganization Organization { get; set; } = null!;
    /// <summary>The ID of the user.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Navigation property for the user.</summary>
    public AuthUser User { get; set; } = null!;
}
