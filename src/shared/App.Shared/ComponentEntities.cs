namespace App.Shared.Entities;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using App.Contracts.Enums;

/// <summary>
/// Reusable OOP blueprint recipe for instantiating machine components with inheritance and auto-tagging.
/// </summary>
[Table("component_recipes", Schema = "backend")]
public class ComponentRecipe
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string RecipeCode { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ComponentCategory Category { get; set; } = ComponentCategory.Hardware;

    public Guid? ExtendsRecipeId { get; set; }
    public ComponentRecipe? ExtendsRecipe { get; set; }

    [MaxLength(255)]
    public string? Manufacturer { get; set; }

    [MaxLength(255)]
    public string? ModelNumber { get; set; }

    public decimal? BaseCostEur { get; set; }

    /// <summary>JSON Schema string validating instance-specific attributes.</summary>
    public string? SpecificationSchemaJson { get; set; }

    /// <summary>Standalone semantic tags automatically applied to components instantiated from this recipe.</summary>
    public List<string> AutoTags { get; set; } = new();

    /// <summary>Default EPLAN device tag prefix, symbol macro, and CAD layer settings (JSON).</summary>
    public string? EplanCadDefaultsJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<MachineComponent> InstantiatedComponents { get; set; } = new();
}

/// <summary>
/// Represents an operational component slot installed on a machine.
/// </summary>
[Table("machine_components", Schema = "backend")]
public class MachineComponent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MachineId { get; set; }
    public Machine? Machine { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Functional slot identifier or bus path (e.g. "AXIS-X", "SLOT-01", "CAMERA-TOP").</summary>
    [Required, MaxLength(128)]
    public string FunctionalSlotIdentifier { get; set; } = string.Empty;

    public ComponentCategory Category { get; set; } = ComponentCategory.Hardware;

    public Guid? ComponentRecipeId { get; set; }
    public ComponentRecipe? ComponentRecipe { get; set; }


    /// <summary>Foreign key referencing the currently installed physical inventory item.</summary>
    public Guid? CurrentPartId { get; set; }
    public BaseInventoryItem? CurrentPart { get; set; }

    /// <summary>Standalone semantic classification tags (recipe auto-tags + user tags).</summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>Structured key-value properties matching the recipe's specification schema (JSON).</summary>
    public string? AttributesJson { get; set; }

    /// <summary>Serialized EplanCadReference containing BMK, sheet, CAD handle, BOM position, and PDF coords.</summary>
    public string? EplanCadRefJson { get; set; }

    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Immutable replacement lineage ledger.</summary>
    public List<ComponentPartHistory> PartHistory { get; set; } = new();
}

/// <summary>
/// Immutable audit ledger entry tracking a physical or logical part swap on a component slot.
/// </summary>
[Table("component_part_histories", Schema = "backend")]
public class ComponentPartHistory
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ComponentId { get; set; }
    public MachineComponent? Component { get; set; }

    [Required]
    public Guid MachineId { get; set; }

    [Required]
    public DateTimeOffset SwappedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required, MaxLength(128)]
    public string SwappedByUserId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? SwappedByUserName { get; set; }

    public Guid? PreviousPartId { get; set; }

    [MaxLength(255)]
    public string? PreviousPartSerialNumber { get; set; }

    public Guid? NewPartId { get; set; }

    [MaxLength(255)]
    public string? NewPartSerialNumber { get; set; }

    public PartSwapReason Reason { get; set; } = PartSwapReason.FailureBreakdown;

    public PartDisposition PreviousPartDisposition { get; set; } = PartDisposition.Scrapped;

    public Guid? AssociatedTicketId { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>JSON snapshot of previous component state/parameters prior to swap.</summary>
    public string? PreviousStateSnapshotJson { get; set; }
}

/// <summary>
/// Operational journal log entry for a machine.
/// </summary>
[Table("machine_service_logs", Schema = "backend")]
public class MachineServiceLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MachineId { get; set; }
    public Machine? Machine { get; set; }

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    [Required, MaxLength(128)]
    public string Author { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string EntryType { get; set; } = "RoutineService"; // RoutineService, ShiftHandover, PartReplacement, Calibration, EmergencyRepair, Note

    [Required, MaxLength(255)]
    public string Summary { get; set; } = string.Empty;

    public string? Details { get; set; }

    public Guid? LinkedTicketId { get; set; }
}

/// <summary>
/// Software or feature license bound to a machine or component.
/// </summary>
[Table("license_assets", Schema = "backend")]
public class LicenseAsset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MachineId { get; set; }
    public Machine? Machine { get; set; }

    [Required, MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string LicenseType { get; set; } = "NodeLocked"; // Dongle_HardwareLocked, NodeLocked_MAC, TwinCAT_SystemId, FloatingServer

    /// <summary>AES-256-GCM encrypted license activation key.</summary>
    public string? LicenseKeyEncrypted { get; set; }

    [MaxLength(255)]
    public string? Vendor { get; set; }

    public int? SeatsCount { get; set; }

    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsPerpetual { get; set; } = true;

    [MaxLength(255)]
    public string? HardwareLockIdentifier { get; set; }

    [MaxLength(64)]
    public string Status { get; set; } = "Active"; // Active, Expired, GracePeriod, Revoked

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Technical documentation, schematic PDF, or SOP bound to a machine.
/// </summary>
[Table("machine_documents", Schema = "backend")]
public class MachineDocument
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid MachineId { get; set; }
    public Machine? Machine { get; set; }

    [Required, MaxLength(255)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string DocumentType { get; set; } = "ElectricalSchematic"; // ElectricalSchematic, PneumaticDiagram, UserManual, CeDeclaration, RiskAssessment, SOP, CalibrationCertificate

    [MaxLength(128)]
    public string? DocumentNumber { get; set; }

    [MaxLength(32)]
    public string? Revision { get; set; }

    [Required, MaxLength(1024)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(128)]
    public string MimeType { get; set; } = "application/pdf";

    public long FileSizeBytes { get; set; }

    [MaxLength(16)]
    public string Language { get; set; } = "en";

    [MaxLength(128)]
    public string UploadedBy { get; set; } = "system";

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
