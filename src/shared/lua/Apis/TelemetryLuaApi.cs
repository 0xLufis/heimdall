namespace App.Shared.Lua.Apis;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using App.Shared.Lua.Bridge;
using MoonSharp.Interpreter;

/// <summary>
/// Lua API exposed under the global 'telemetry' table.
/// Exposes historical telemetry queries, statistical filtering (moving averages, spike detection),
/// derived metric emission, and change event handlers.
/// </summary>
public class TelemetryLuaApi
{
    private readonly ILuaPlantDataProvider _provider;
    private readonly ConcurrentDictionary<string, List<Closure>> _changeHandlers = new(StringComparer.OrdinalIgnoreCase);

    public TelemetryLuaApi(ILuaPlantDataProvider provider)
    {
        _provider = provider;
    }

    public void Register(Script script)
    {
        var table = new Table(script);

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

            int limit = count <= 0 ? 20 : count;
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
                    metaDict[pair.Key.String] = pair.Value.ToObject();
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
            var list = _changeHandlers.GetOrAdd(metric, _ => new List<Closure>());
            lock (list)
            {
                list.Add(handler);
            }
            return true;
        });

        // Dispatch a telemetry change event to registered handlers
        table["process_change"] = (Func<string, string, double, double, DynValue>)((machineId, metric, oldVal, newVal) =>
        {
            if (!_changeHandlers.TryGetValue(metric, out var handlers) &&
                !_changeHandlers.TryGetValue("*", out handlers))
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

            lock (handlers)
            {
                foreach (var h in handlers)
                {
                    try
                    {
                        h.Call(changeEvent);
                    }
                    catch
                    {
                        // Protect loop from single handler failure
                    }
                }
            }

            return DynValue.NewTable(changeEvent);
        });

        script.Globals["telemetry"] = table;
    }
}
