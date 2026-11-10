namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Lua.Core;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'events' table.
/// Provides an event bus for plant-level hooks, reactive automation rules,
/// and decoupled communication between the parts of a script.
/// Listeners are scoped to the registering script run: closures are never retained across runs
/// (they would otherwise leak, accumulate on every execution and run outside any sandbox budget).
/// </summary>
public class EventsLuaApi
{
    private const int MaxListeners = 100;

    public void Register(Script script)
    {
        var table = new Table(script);
        var listeners = new Dictionary<string, List<Closure>>(StringComparer.OrdinalIgnoreCase);
        int listenerCount = 0;

        table["on"] = (Func<string, Closure, bool>)((eventName, handler) =>
        {
            if (string.IsNullOrWhiteSpace(eventName) || handler == null) return false;
            if (listenerCount >= MaxListeners) return false;
            if (!listeners.TryGetValue(eventName, out var list))
            {
                listeners[eventName] = list = new List<Closure>();
            }
            list.Add(handler);
            listenerCount++;
            return true;
        });

        table["trigger"] = (Func<string, Table, int>)((eventName, payload) =>
        {
            if (string.IsNullOrWhiteSpace(eventName)) return 0;

            int invokedCount = 0;
            if (listeners.TryGetValue(eventName, out var directListeners))
            {
                invokedCount += InvokeList(directListeners, payload);
            }

            if (listeners.TryGetValue("*", out var wildcardListeners))
            {
                invokedCount += InvokeList(wildcardListeners, payload);
            }

            return invokedCount;
        });

        script.Globals["events"] = table;
    }

    private static int InvokeList(List<Closure> listeners, Table? payload)
    {
        int count = 0;
        foreach (var l in listeners.ToArray())
        {
            try
            {
                LuaSandboxGuard.Call(l, payload != null ? DynValue.NewTable(payload) : DynValue.Nil);
                count++;
            }
            catch (ScriptRuntimeException)
            {
                // A failing listener must not break dispatch to the others. Sandbox violations
                // (LuaSandboxViolationException) are intentionally not caught.
            }
        }
        return count;
    }
}
