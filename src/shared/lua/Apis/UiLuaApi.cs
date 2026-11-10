namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using App.Shared.Lua.Core;
using App.Shared.Lua.Models;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'ui' table.
/// Fulfills requirement: "UI should not be replaced by lua only extended. If a feature can be made dynamic it should be."
/// Allows plant scripts to declare dynamic action buttons, contextual badges, metric widgets,
/// and banners without changing core frontend Vue templates.
///
/// One instance collects the declarations of a single script run. Handler closures never outlive
/// that run: the automation service re-executes the owning script to invoke an action, so handlers
/// always run under a fresh sandbox budget and a live data scope.
/// </summary>
public partial class UiLuaApi
{
    public const int MaxExtensionsPerScript = 50;
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 1000;
    private const int MaxProperties = 32;

    private readonly Dictionary<string, (LuaUiExtension Extension, Closure? Handler)> _extensions = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("^[A-Za-z0-9_.:-]{1,64}$")]
    private static partial Regex SafeIdRegex();

    [GeneratedRegex("^i-[a-z0-9]+(?:[-:][a-z0-9]+)*$")]
    private static partial Regex SafeIconRegex();

    [GeneratedRegex("^[a-z]{1,20}(?:-[0-9]{2,3})?$")]
    private static partial Regex SafeColorRegex();

    public IReadOnlyList<LuaUiExtension> GetAllExtensions() =>
        _extensions.Values.Select(v => v.Extension).ToList();

    public IReadOnlyList<LuaUiExtension> GetExtensionsForZone(LuaUiTargetZone zone) =>
        _extensions.Values.Where(v => v.Extension.TargetZone == zone).Select(v => v.Extension).ToList();

    /// <summary>Invokes a handler registered during this run. Must be called on the run's script.</summary>
    public DynValue InvokeAction(Script script, string id, Dictionary<string, object?>? context)
    {
        if (string.IsNullOrWhiteSpace(id) || !_extensions.TryGetValue(id, out var item) || item.Handler == null)
        {
            throw new ScriptRuntimeException($"UI action '{id}' is not registered by its owning script.");
        }

        var contextTable = new Table(script);
        if (context != null)
        {
            foreach (var (k, v) in context)
            {
                contextTable[k] = LuaScriptEngine.ToDynValue(script, v);
            }
        }

        return LuaSandboxGuard.Call(item.Handler, DynValue.NewTable(contextTable));
    }

    public void Register(Script script)
    {
        var table = new Table(script);

        table["register_action"] = (Func<Table, bool>)((def) =>
        {
            if (def == null) return false;
            var handlerClosure = def.Get("handler").Function;
            var ext = BuildExtension(def, LuaUiExtensionType.ActionButton, "Custom Action", "machine", defaultColor: null);
            ext.Description = Truncate(def.Get("description").String, MaxDescriptionLength);
            ext.HandlerFunctionName = handlerClosure != null ? ext.Id : null;

            var props = def.Get("props");
            if (props.Type == DataType.Table)
            {
                foreach (var pair in props.Table.Pairs.Take(MaxProperties))
                {
                    if (pair.Key.Type != DataType.String) continue;
                    ext.Properties[pair.Key.String] = LuaScriptEngine.ConvertDynValue(pair.Value, maxNodes: 256);
                }
            }

            return Add(ext, handlerClosure);
        });

        table["register_badge"] = (Func<Table, bool>)((def) =>
        {
            if (def == null) return false;
            var ext = BuildExtension(def, LuaUiExtensionType.Badge, "Status", "machine", defaultColor: "blue");
            return Add(ext, null);
        });

        table["register_banner"] = (Func<Table, bool>)((def) =>
        {
            if (def == null) return false;
            var ext = BuildExtension(def, LuaUiExtensionType.BannerAlert, "Notice", "dashboard", defaultColor: "amber");
            ext.Title = Truncate(def.Get("message").String, MaxTitleLength) ?? ext.Title;
            ext.Color = SanitizeColor(def.Get("level").String) ?? "amber";
            return Add(ext, null);
        });

        table["get_extensions"] = (Func<string, DynValue>)((targetStr) =>
        {
            var zone = ParseZone(targetStr);
            var result = new Table(script);
            int idx = 1;
            foreach (var e in GetExtensionsForZone(zone))
            {
                var t = new Table(script);
                t["id"] = e.Id;
                t["title"] = e.Title;
                t["type"] = e.ExtensionType.ToString();
                t["icon"] = e.Icon;
                t["color"] = e.Color ?? string.Empty;
                result[idx++] = t;
            }
            return DynValue.NewTable(result);
        });

        table["invoke_action"] = (Func<string, Table, DynValue>)((id, context) =>
        {
            if (string.IsNullOrWhiteSpace(id) || !_extensions.TryGetValue(id, out var item) || item.Handler == null)
            {
                return DynValue.Nil;
            }
            return LuaSandboxGuard.Call(item.Handler, context != null ? DynValue.NewTable(context) : DynValue.Nil);
        });

        script.Globals["ui"] = table;
    }

    private bool Add(LuaUiExtension ext, Closure? handler)
    {
        if (!_extensions.ContainsKey(ext.Id) && _extensions.Count >= MaxExtensionsPerScript)
        {
            throw new ScriptRuntimeException($"A script may register at most {MaxExtensionsPerScript} UI extensions.");
        }
        _extensions[ext.Id] = (ext, handler);
        return true;
    }

    private static LuaUiExtension BuildExtension(Table def, LuaUiExtensionType type, string defaultTitle, string defaultTarget, string? defaultColor)
    {
        var rawId = def.Get("id").String;
        if (rawId != null && !SafeIdRegex().IsMatch(rawId))
        {
            throw new ScriptRuntimeException("UI extension id must be 1-64 characters of [A-Za-z0-9_.:-].");
        }

        var icon = def.Get("icon").String;
        return new LuaUiExtension
        {
            Id = rawId ?? Guid.NewGuid().ToString("N"),
            Title = Truncate(def.Get("title").String, MaxTitleLength) ?? defaultTitle,
            TargetZone = ParseZone(def.Get("target").String ?? defaultTarget),
            ExtensionType = type,
            Icon = icon != null && icon.Length <= 64 && SafeIconRegex().IsMatch(icon) ? icon : "i-lucide-sparkles",
            Color = SanitizeColor(def.Get("color").String) ?? defaultColor
        };
    }

    private static string? SanitizeColor(string? color) =>
        color != null && SafeColorRegex().IsMatch(color) ? color : null;

    private static string? Truncate(string? value, int max) =>
        value == null ? null : value.Length <= max ? value : value[..max];

    private static LuaUiTargetZone ParseZone(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return LuaUiTargetZone.MachineDetails;
        return target.ToLowerInvariant() switch
        {
            "machine" or "machines" => LuaUiTargetZone.MachineDetails,
            "ticket" or "tickets" => LuaUiTargetZone.TicketDetails,
            "telemetry" => LuaUiTargetZone.LiveTelemetry,
            "dashboard" or "overview" => LuaUiTargetZone.DashboardOverview,
            "sidebar" or "nav" => LuaUiTargetZone.SidebarMenu,
            _ => LuaUiTargetZone.GeneralWorkbench
        };
    }
}
