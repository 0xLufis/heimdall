namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'telemetry' table.
/// Exposes historical telemetry queries, statistical filtering (moving averages, spike detection),
/// derived metric emission, and change event handlers.
/// </summary>
public class TelemetryLuaApi
{
    private const int MaxChangeHandlers = 100;
    private const int MaxHistoryPoints = 1000;
    private readonly ILuaPlantDataProvider _provider;

    public TelemetryLuaApi(ILuaPlantDataProvider provider)
    {
        _provider = provider;
    }

    public void Register(Script script)
    {
        var table = new Table(script);
        // Handlers are scoped to this script run (see EventsLuaApi for rationale).
        var changeHandlers = new Dictionary<string, List<Closure>>(StringComparer.OrdinalIgnoreCase);
        int handlerCount = 0;

        table["get_latest"] = (Func<string, string, DynValue>)((machineId, metric) =>
        {
            if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(metric)) return DynValue.Nil;
            var val = _provider.GetLatestTelemetryValueAsync(machineId, metric).GetAwaiter().GetResult();
            return val.HasValue ? DynValue.NewNumber(val.Value) : DynValue.Nil;
        });

        table["get_history"] = (Func<string, string, int, DynValue>)((machineId, metric, count) =>
        {
            if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(metric))
                return DynValue.NewTable(new Table(script));

            int limit = count <= 0 ? 20 : Math.Min(count, MaxHistoryPoints);
            var history = _provider.GetTelemetryHistoryAsync(machineId, metric, limit).GetAwaiter().GetResult();

            var result = new Table(script);
            int idx = 1;
            foreach (var item in history)
            {
                var point = new Table(script);
                point["value"] = item.Value;
                point["timestamp"] = item.Timestamp.ToString("o");
                result[idx++] = point;
            }
            return DynValue.NewTable(result);
        });

        table["emit"] = (Func<string, string, double, Table, bool>)((machineId, metric, value, metadata) =>
        {
            if (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(metric)) return false;

            Dictionary<string, object?>? metaDict = null;
            if (metadata != null)
            {
                metaDict = new Dictionary<string, object?>();
                foreach (var pair in metadata.Pairs)
                {
                    if (pair.Key.Type != DataType.String) continue;
                    metaDict[pair.Key.String] = LuaScriptEngine.ConvertDynValue(pair.Value, maxNodes: 256);
                }
            }

            _provider.EmitTelemetryAsync(machineId, metric, value, metaDict).GetAwaiter().GetResult();
            return true;
        });

        // Math helper: Moving average of numbers
        table["moving_average"] = (Func<Table, int, DynValue>)((valuesTable, windowSize) =>
        {
            if (valuesTable == null || valuesTable.Length == 0) return DynValue.Nil;

            int window = windowSize <= 0 ? valuesTable.Length : Math.Min(windowSize, valuesTable.Length);
            double sum = 0;
            int count = 0;

            for (int i = valuesTable.Length - window + 1; i <= valuesTable.Length; i++)
            {
                var v = valuesTable.Get(i);
                if (v.Type == DataType.Number)
                {
                    sum += v.Number;
                    count++;
                }
                else if (v.Type == DataType.Table && v.Table["value"] != null)
                {
                    sum += v.Table.Get("value").Number;
                    count++;
                }
            }

            return count > 0 ? DynValue.NewNumber(sum / count) : DynValue.Nil;
        });

        // Statistical anomaly / spike detector
        table["detect_spike"] = (Func<double, Table, double, DynValue>)((currentVal, historyTable, sensitivityStdDev) =>
        {
            var res = new Table(script);
            res["is_spike"] = false;
            res["deviation"] = 0.0;
            res["mean"] = currentVal;

            if (historyTable == null || historyTable.Length < 3)
            {
                return DynValue.NewTable(res);
            }

            var nums = new List<double>();
            for (int i = 1; i <= historyTable.Length; i++)
            {
                var v = historyTable.Get(i);
                if (v.Type == DataType.Number) nums.Add(v.Number);
                else if (v.Type == DataType.Table && v.Table["value"] != null) nums.Add(v.Table.Get("value").Number);
            }

            if (nums.Count < 3) return DynValue.NewTable(res);

            double mean = nums.Average();
            double variance = nums.Sum(x => Math.Pow(x - mean, 2)) / nums.Count;
            double stdDev = Math.Sqrt(variance);

            double threshold = sensitivityStdDev <= 0 ? 2.5 : sensitivityStdDev;
            double diff = Math.Abs(currentVal - mean);
            double deviation = stdDev > 0.0001 ? diff / stdDev : 0.0;

            res["is_spike"] = deviation >= threshold;
            res["deviation"] = deviation;
            res["mean"] = mean;
            res["std_dev"] = stdDev;

            return DynValue.NewTable(res);
        });

        // Register custom telemetry change listener in Lua
        table["on_change"] = (Func<string, Closure, bool>)((metric, handler) =>
        {
            if (string.IsNullOrWhiteSpace(metric) || handler == null) return false;
            if (handlerCount >= MaxChangeHandlers) return false;
            if (!changeHandlers.TryGetValue(metric, out var list))
            {
                changeHandlers[metric] = list = new List<Closure>();
            }
            list.Add(handler);
            handlerCount++;
            return true;
        });

        // Dispatch a telemetry change event to registered handlers
        table["process_change"] = (Func<string, string, double, double, DynValue>)((machineId, metric, oldVal, newVal) =>
        {
            if (string.IsNullOrWhiteSpace(metric) ||
                (!changeHandlers.TryGetValue(metric, out var handlers) &&
                 !changeHandlers.TryGetValue("*", out handlers)))
            {
                return DynValue.Nil;
            }

            var changeEvent = new Table(script);
            changeEvent["machine_id"] = machineId;
            changeEvent["metric"] = metric;
            changeEvent["old_value"] = oldVal;
            changeEvent["new_value"] = newVal;
            changeEvent["delta"] = newVal - oldVal;
            changeEvent["timestamp"] = DateTimeOffset.UtcNow.ToString("o");

            foreach (var h in handlers.ToArray())
            {
                try
                {
                    LuaSandboxGuard.Call(h, DynValue.NewTable(changeEvent));
                }
                catch (ScriptRuntimeException)
                {
                    // Protect loop from single handler failure; sandbox violations still propagate.
                }
            }

            return DynValue.NewTable(changeEvent);
        });

        script.Globals["telemetry"] = table;
    }
}
