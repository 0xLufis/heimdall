namespace App.Agent.Daemon.Runtime.Evaluation;

using System;
using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Text.Json;
using App.Agent.Daemon.Domain.Recipes;
using App.Agent.Daemon.Runtime.Merger;

/// <summary>
/// Evaluates incoming probe readings against deadband rules and max quiet period TTLs.
/// </summary>
public class DeadbandEvaluator
{
    private class PreviousState
    {
        public object? Value { get; set; }
        public ulong ObjectHash { get; set; }
        public DateTimeOffset LastReportedTime { get; set; }
    }

    private readonly ConcurrentDictionary<string, PreviousState> _stateCache = new();

    public bool ShouldEmit(
        string evaluationKey, 
        MergedDataReading reading, 
        PollingStrategyConfig strategy,
        out bool isHeartbeatForced)
    {
        isHeartbeatForced = false;
        var now = reading.Timestamp;

        var state = _stateCache.GetOrAdd(evaluationKey, _ => new PreviousState
        {
            Value = null,
            ObjectHash = 0,
            LastReportedTime = DateTimeOffset.MinValue
        });

        // Check Heartbeat TTL
        if (state.LastReportedTime != DateTimeOffset.MinValue &&
            (now - state.LastReportedTime).TotalMilliseconds >= strategy.MaxQuietPeriodMs)
        {
            isHeartbeatForced = true;
            UpdateState(state, reading);
            return true;
        }

        // First time evaluation always emits
        if (state.Value == null && state.ObjectHash == 0)
        {
            UpdateState(state, reading);
            return true;
        }

        var deadband = strategy.Deadband ?? new DeadbandConfig(DeadbandType.StateChangeOnly, 0);
        bool isSignificantChange = false;

        switch (deadband.DeadbandType)
        {
            case DeadbandType.None:
                isSignificantChange = true;
                break;

            case DeadbandType.Absolute:
                if (TryGetNumericDifference(state.Value, reading.RawValue, out double absDiff))
                {
                    isSignificantChange = absDiff >= deadband.Threshold;
                }
                else
                {
                    isSignificantChange = !Equals(state.Value, reading.RawValue);
                }
                break;

            case DeadbandType.Percentage:
                if (TryGetPercentageDifference(state.Value, reading.RawValue, out double pctDiff))
                {
                    isSignificantChange = pctDiff >= deadband.Threshold;
                }
                else
                {
                    isSignificantChange = !Equals(state.Value, reading.RawValue);
                }
                break;

            case DeadbandType.StateChangeOnly:
            default:
                if (reading.RawValue is string or bool or Enum or ValueType)
                {
                    isSignificantChange = !Equals(state.Value, reading.RawValue);
                }
                else
                {
                    ulong currentHash = ComputeObjectHash(reading.RawValue);
                    isSignificantChange = currentHash != state.ObjectHash;
                }
                break;
        }

        if (isSignificantChange)
        {
            UpdateState(state, reading);
            return true;
        }

        return false;
    }

    private void UpdateState(PreviousState state, MergedDataReading reading)
    {
        state.Value = reading.RawValue;
        state.ObjectHash = ComputeObjectHash(reading.RawValue);
        state.LastReportedTime = reading.Timestamp;
    }

    public static ulong ComputeObjectHash(object? obj)
    {
        if (obj == null) return 0;
        byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(obj);
        return XxHash64.HashToUInt64(utf8Bytes);
    }

    private static bool TryGetNumericDifference(object? oldVal, object? newVal, out double diff)
    {
        diff = 0;
        if (oldVal == null || newVal == null) return false;
        try
        {
            double v1 = Convert.ToDouble(oldVal);
            double v2 = Convert.ToDouble(newVal);
            diff = Math.Abs(v2 - v1);
            return true;
        }
        catch { return false; }
    }

    private static bool TryGetPercentageDifference(object? oldVal, object? newVal, out double pct)
    {
        pct = 0;
        if (oldVal == null || newVal == null) return false;
        try
        {
            double v1 = Convert.ToDouble(oldVal);
            double v2 = Convert.ToDouble(newVal);
            if (Math.Abs(v1) < 0.000001) { pct = Math.Abs(v2) > 0 ? 100.0 : 0.0; return true; }
            pct = Math.Abs((v2 - v1) / v1) * 100.0;
            return true;
        }
        catch { return false; }
    }
}
