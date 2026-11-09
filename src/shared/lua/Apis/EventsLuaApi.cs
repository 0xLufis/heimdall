namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'events' table.
/// Provides an event bus for plant-level hooks, reactive automation rules,
/// and decoupled communication between scripts.
/// </summary>
public class EventsLuaApi
{
    private readonly ConcurrentDictionary<string, List<Closure>> _listeners = new(StringComparer.OrdinalIgnoreCase);

    public void Register(Script script)
    {
        var table = new Table(script);

        table["on"] = (Func<string, Closure, bool>)((eventName, handler) =>
        {
            if (string.IsNullOrWhiteSpace(eventName) || handler == null) return false;
            var list = _listeners.GetOrAdd(eventName, _ => new List<Closure>());
            lock (list)
            {
                list.Add(handler);
            }
            return true;
        });

        table["trigger"] = (Func<string, Table, int>)((eventName, payload) =>
        {
            if (string.IsNullOrWhiteSpace(eventName)) return 0;

            int invokedCount = 0;
            if (_listeners.TryGetValue(eventName, out var directListeners))
            {
                invokedCount += InvokeList(directListeners, payload);
            }

            if (_listeners.TryGetValue("*", out var wildcardListeners))
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
        lock (listeners)
        {
            foreach (var l in listeners)
            {
                try
                {
                    l.Call(payload != null ? DynValue.NewTable(payload) : DynValue.Nil);
                    count++;
                }
                catch
                {
                    // Prevent single listener crash from breaking event dispatch
                }
            }
        }
        return count;
    }
}
