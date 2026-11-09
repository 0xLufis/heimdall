namespace App.Shared.Lua.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Trigger category for a plant-level Lua script.
/// </summary>
public enum LuaScriptTriggerType
{
    Manual = 0,
    TelemetryChange = 1,
    MachineStateChange = 2,
    SlaPreventiveCheck = 3,
    UiExtension = 4,
    Scheduled = 5
}

/// <summary>
/// Stored plant-specific Lua script definition.
/// </summary>
public class LuaScriptRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human-readable name of the script / automation rule.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of the business purpose or plant workflow automated by this script.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Trigger type for automated execution.</summary>
    public LuaScriptTriggerType TriggerType { get; set; } = LuaScriptTriggerType.Manual;

    /// <summary>Raw Lua source code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Whether this script is active and eligible for event hooks.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Author or user who created this script.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Timestamp when the script was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp when the script was last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Timestamp of the last execution.</summary>
    public DateTimeOffset? LastExecutedAt { get; set; }

    /// <summary>Flag indicating if last execution succeeded.</summary>
    public bool? LastExecutionSuccess { get; set; }

    /// <summary>Last error message, if failed.</summary>
    public string? LastExecutionError { get; set; }

    /// <summary>Custom metadata dictionary (e.g., target machines, metric patterns).</summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Target UI zone where dynamic extensions registered from Lua can be rendered.
/// </summary>
public enum LuaUiTargetZone
{
    MachineDetails = 0,
    TicketDetails = 1,
    LiveTelemetry = 2,
    DashboardOverview = 3,
    SidebarMenu = 4,
    GeneralWorkbench = 5
}

/// <summary>
/// Display type of the dynamic UI extension.
/// </summary>
public enum LuaUiExtensionType
{
    ActionButton = 0,
    Badge = 1,
    MetricCard = 2,
    BannerAlert = 3
}

/// <summary>
/// Represents a dynamic UI extension registered by a Lua script.
/// Spec note: "UI should not be replaced by lua only extended. If a feature can be made dynamic it should be"
/// </summary>
public class LuaUiExtension
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public LuaUiTargetZone TargetZone { get; set; } = LuaUiTargetZone.MachineDetails;
    public LuaUiExtensionType ExtensionType { get; set; } = LuaUiExtensionType.ActionButton;
    public string Icon { get; set; } = "i-lucide-sparkles";
    public string? Color { get; set; }
    public string? HandlerFunctionName { get; set; }
    public string? ConditionExpression { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = new();
}
