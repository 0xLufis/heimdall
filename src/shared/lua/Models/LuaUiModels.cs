namespace App.Shared.Lua.Models;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Target UI zone where dynamic extensions registered from Lua can be rendered.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LuaUiTargetZone>))]
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
[JsonConverter(typeof(JsonStringEnumConverter<LuaUiExtensionType>))]
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
    public Dictionary<string, object?> Properties { get; set; } = new();
}
