namespace App.Contracts.Machine;

using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Contracts.Enums;

/// <summary>
/// Universal cross-reference binding a Machine Component to electrical schematics, CAD layouts, and BOMs.
/// </summary>
public class EplanCadReference
{
    /// <summary>EPLAN Device Tag / BMK (Betriebsmittelkennzeichnung), e.g. "=LINE1+OP20-KF1", "-10K1".</summary>
    public string? DeviceTag { get; set; }

    /// <summary>EPLAN Location designation, e.g. "+OP20", "+CABINET-01".</summary>
    public string? LocationAspect { get; set; }

    /// <summary>Schematic drawing sheet and path column, e.g. "Page 14, Column 3.2" (/14.3).</summary>
    public string? DrawingPage { get; set; }

    /// <summary>EPLAN Function text describing operational circuit role, e.g. "Emergency Stop Safety Relay".</summary>
    public string? FunctionText { get; set; }

    /// <summary>Mechanical CAD DXF/DWG object handle, e.g. "3B4F".</summary>
    public string? CadHandle { get; set; }

    /// <summary>CAD layer or 3D block identifier, e.g. "ELEC_CABINET_RACK".</summary>
    public string? CadBlockName { get; set; }

    /// <summary>Bill of Materials (BOM) position index, e.g. "Pos 10.4".</summary>
    public string? BomPosition { get; set; }

    /// <summary>Reference Designator in circuit diagram, e.g. "KF1", "B1", "M3".</summary>
    public string? ReferenceDesignator { get; set; }

    /// <summary>Direct coordinate link inside an exported PDF schematic or plant drawing.</summary>
    public PdfNodeLocation? PdfLocation { get; set; }
}

/// <summary>
/// Bounding box coordinates on an exported schematic or layout PDF.
/// </summary>
public class PdfNodeLocation
{
    /// <summary>Reference to the uploaded schematic MachineDocument.</summary>
    public Guid DocumentId { get; set; }

    /// <summary>1-based page number in the PDF drawing.</summary>
    public int PageNumber { get; set; }

    /// <summary>Bounding box coordinates in PDF points for highlighting the schematic symbol.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

/// <summary>
/// Reusable blueprint recipe for instantiating machine components with inheritance and auto-tagging.
/// </summary>
public class ComponentRecipeDto
{
    public Guid Id { get; set; }
    public string RecipeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ComponentCategory Category { get; set; } = ComponentCategory.Hardware;
    public Guid? ExtendsRecipeId { get; set; }
    public string? Manufacturer { get; set; }
    public string? ModelNumber { get; set; }
    public decimal? BaseCostEur { get; set; }
    public JsonDocument? SpecificationSchema { get; set; }
    public List<string> AutoTags { get; set; } = new();
    public EplanCadReference? EplanCadDefaults { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Operational component slot installed on a machine.
/// </summary>
public class MachineComponentDto
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FunctionalSlotIdentifier { get; set; } = string.Empty;
    public ComponentCategory Category { get; set; } = ComponentCategory.Hardware;
    public Guid? ComponentRecipeId { get; set; }
    public string? ComponentRecipeCode { get; set; }
    public Guid? CurrentPartId { get; set; }
    public string? CurrentPartSerialNumber { get; set; }
    public List<string> Tags { get; set; } = new();
    public JsonDocument? Attributes { get; set; }
    public EplanCadReference? EplanCadRef { get; set; }
    public List<ComponentPartHistoryDto> PartHistory { get; set; } = new();
    public DateTimeOffset InstalledAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Historical record of a part swap on a component slot.
/// </summary>
public class ComponentPartHistoryDto
{
    public Guid Id { get; set; }
    public Guid ComponentId { get; set; }
    public Guid MachineId { get; set; }
    public DateTimeOffset SwappedAt { get; set; }
    public string SwappedByUserId { get; set; } = string.Empty;
    public string? SwappedByUserName { get; set; }
    public Guid? PreviousPartId { get; set; }
    public string? PreviousPartSerialNumber { get; set; }
    public Guid? NewPartId { get; set; }
    public string? NewPartSerialNumber { get; set; }
    public PartSwapReason Reason { get; set; }
    public PartDisposition PreviousPartDisposition { get; set; }
    public Guid? AssociatedTicketId { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Cost center accounting allocation when using an inventory part.
/// </summary>
public class PartUsageCostCenterDto
{
    public string? ProdLine { get; set; }
    public string? Project { get; set; }
    public string? Department { get; set; }
}

/// <summary>
/// Result of an inventory-to-component assignment operation.
/// </summary>
public class ComponentPartAssignmentResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid ComponentId { get; set; }
    public Guid? PartId { get; set; }
    public Guid? HistoryRecordId { get; set; }
    public bool IsNewComponentCreated { get; set; }
}

/// <summary>
/// Request to assign an inventory part to an existing component slot or create a new slot.
/// </summary>
public class AssignPartToComponentRequestDto
{
    public Guid MachineId { get; set; }
    public Guid? ExistingComponentId { get; set; }
    public string? NewComponentSlotIdentifier { get; set; }
    public string? NewComponentName { get; set; }
    public ComponentCategory Category { get; set; } = ComponentCategory.Hardware;
    public Guid? ComponentRecipeId { get; set; }
    public Guid PartId { get; set; }
    public string SwappedByUserId { get; set; } = string.Empty;
    public string? SwappedByUserName { get; set; }
    public PartSwapReason Reason { get; set; } = PartSwapReason.ScheduledReplacement;
    public PartDisposition PreviousPartDisposition { get; set; } = PartDisposition.SentForRepair;
    public Guid? AssociatedTicketId { get; set; }
    public string? Notes { get; set; }
    public PartUsageCostCenterDto? CostCenter { get; set; }
    public EplanCadReference? EplanCadRef { get; set; }
    public List<string>? AdditionalTags { get; set; }
}
