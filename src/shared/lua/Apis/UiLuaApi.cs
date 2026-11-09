namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Lua.Core;
using App.Shared.Lua.Models;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'ui' table.
/// Fulfills requirement: "UI should not be replaced by lua only extended. If a feature can be made dynamic it should be."
/// Allows plant scripts to declare dynamic action buttons, contextual badges, metric widgets,
/// and banners without changing core frontend Vue templates.
/// </summary>
public class UiLuaApi
{
    private readonly ConcurrentDictionary<string, (LuaUiExtension Extension, Closure? Handler)> _extensions = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<LuaUiExtension> GetAllExtensions() =>
        _extensions.Values.Select(v => v.Extension).ToList();

    public IReadOnlyCollection<LuaUiExtension> GetExtensionsForZone(LuaUiTargetZone zone) =>
        _extensions.Values.Where(v => v.Extension.TargetZone == zone).Select(v => v.Extension).ToList();

    public DynValue InvokeAction(string id, Dictionary<string, object?>? context)
    {
        if (string.IsNullOrWhiteSpace(id) || !_extensions.TryGetValue(id, out var item) || item.Handler == null)
        {
            return DynValue.Nil;
        }

        var ownerScript = item.Handler.OwnerScript;
        var contextTable = new Table(ownerScript);
        if (context != null)
        {
            foreach (var (k, v) in context)
            {
                contextTable[k] = LuaScriptEngine.ToDynValue(ownerScript, v);
            }
        }

        return item.Handler.Call(DynValue.NewTable(contextTable));
    }

    public void Register(Script script)
    {
        var table = new Table(script);

        table["register_action"] = (Func<Table, bool>)((def) =>
        {
            if (def == null) return false;

            string id = def.Get("id").String ?? Guid.NewGuid().ToString("N");
            string title = def.Get("title").String ?? "Custom Action";
            string targetStr = def.Get("target").String ?? "machine";
            string icon = def.Get("icon").String ?? "i-lucide-wrench";
            string? color = def.Get("color").String;
            var handlerClosure = def.Get("handler").Function;

            var targetZone = ParseZone(targetStr);

            var ext = new LuaUiExtension
            {
                Id = id,
                Title = title,
                Description = def.Get("description").String,
                TargetZone = targetZone,
                ExtensionType = LuaUiExtensionType.ActionButton,
                Icon = icon,
                Color = color,
                HandlerFunctionName = handlerClosure != null ? id : null
            };

            // Store custom props
            var props = def.Get("props");
            if (props.Type == DataType.Table)
            {
                foreach (var pair in props.Table.Pairs)
                {
                    ext.Properties[pair.Key.String] = pair.Value.ToObject();
                }
            }

            _extensions[id] = (ext, handlerClosure);
            return true;
        });

        table["register_badge"] = (Func<Table, bool>)((def) =>
        {
            if (def == null) return false;

            string id = def.Get("id").String ?? Guid.NewGuid().ToString("N");
            string title = def.Get("title").String ?? "Status";
            string targetStr = def.Get("target").String ?? "machine";
            string? color = def.Get("color").String ?? "blue";

            var ext = new LuaUiExtension
            {
                Id = id,
                Title = title,
                TargetZone = ParseZone(targetStr),
                ExtensionType = LuaUiExtensionType.Badge,
                Color = color
            };

            _extensions[id] = (ext, null);
            return true;
        });

        table["register_banner"] = (Func<Table, bool>)((def) =>
        {
            if (def == null) return false;

            string id = def.Get("id").String ?? Guid.NewGuid().ToString("N");
            string title = def.Get("message").String ?? def.Get("title").String ?? "Notice";
            string targetStr = def.Get("target").String ?? "dashboard";
            string? color = def.Get("level").String ?? "amber";

            var ext = new LuaUiExtension
            {
                Id = id,
                Title = title,
                TargetZone = ParseZone(targetStr),
                ExtensionType = LuaUiExtensionType.BannerAlert,
                Color = color
            };

            _extensions[id] = (ext, null);
            return true;
        });

        table["get_extensions"] = (Func<string, DynValue>)((targetStr) =>
        {
            var zone = ParseZone(targetStr);
            var exts = GetExtensionsForZone(zone);
            var result = new Table(script);
            int idx = 1;
            foreach (var e in exts)
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

            try
            {
                return item.Handler.Call(context != null ? DynValue.NewTable(context) : DynValue.Nil);
            }
            catch (Exception ex)
            {
                return DynValue.NewString($"Error executing UI action handler '{id}': {ex.Message}");
            }
        });

        script.Globals["ui"] = table;
    }

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
