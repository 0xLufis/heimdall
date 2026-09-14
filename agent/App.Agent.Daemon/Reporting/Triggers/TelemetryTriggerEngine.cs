namespace App.Agent.Daemon.Reporting.Triggers;

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using App.Agent.Daemon.Domain.Recipes;
using App.Agent.Daemon.Infrastructure.Beckhoff;

public class HeartbeatTrigger : ITelemetryTrigger
{
    public string Name => "HeartbeatTimer";
    public TriggerType Type => TriggerType.Heartbeat;
    public TimeSpan Interval { get; set; }

    public HeartbeatTrigger(TimeSpan? interval = null)
    {
        Interval = interval ?? TimeSpan.FromSeconds(60);
    }

    public TriggerEvaluation Evaluate(TriggerContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (context.LastReportedTime == DateTimeOffset.MinValue || (now - context.LastReportedTime) >= Interval)
        {
            return new TriggerEvaluation
            {
                Triggered = true,
                Type = TriggerType.Heartbeat,
                Reason = $"Periodic heartbeat expired ({Interval.TotalSeconds}s interval)",
                Priority = EgressPriority.P2_MediumMetrics,
                SlicesToReport = context.LastReportedTime == DateTimeOffset.MinValue 
                    ? DataSliceSelection.FullSnapshot 
                    : DataSliceSelection.HeartbeatOnly
            };
        }
        return TriggerEvaluation.NotTriggered(TriggerType.Heartbeat);
    }
}

public class ThresholdTrigger : ITelemetryTrigger
{
    public string Name => "MetricThresholdBreach";
    public TriggerType Type => TriggerType.ThresholdBreach;
    public double OsDriveFreeGbWarningThreshold { get; set; } = 15.0;
    public double OsDriveFreeGbCriticalThreshold { get; set; } = 5.0;
    public double PlcTemperatureCriticalThreshold { get; set; } = 75.0;

    public TriggerEvaluation Evaluate(TriggerContext context)
    {
        // 1. Check OS Drive free space
        if (context.CurrentSystemInfo?.Disk != null)
        {
            double osFree = context.CurrentSystemInfo.Disk.OsDriveFreeGB;
            if (osFree > 0)
            {
                if (osFree <= OsDriveFreeGbCriticalThreshold)
                {
                    return new TriggerEvaluation
                    {
                        Triggered = true,
                        Type = TriggerType.ThresholdBreach,
                        Reason = $"CRITICAL: OS Drive free space depleted ({osFree} GB remaining <= {OsDriveFreeGbCriticalThreshold} GB)",
                        Priority = EgressPriority.P0_CriticalAlarm,
                        SlicesToReport = new DataSliceSelection { IncludeDisk = true, IncludeEvents = true }
                    };
                }
                if (osFree <= OsDriveFreeGbWarningThreshold)
                {
                    return new TriggerEvaluation
                    {
                        Triggered = true,
                        Type = TriggerType.ThresholdBreach,
                        Reason = $"WARNING: OS Drive free space below limit ({osFree} GB remaining <= {OsDriveFreeGbWarningThreshold} GB)",
                        Priority = EgressPriority.P1_HighOperational,
                        SlicesToReport = new DataSliceSelection { IncludeDisk = true }
                    };
                }
            }
        }

        // 2. Check PLC Temperature threshold
        if (context.PlcVariables != null && context.PlcVariables.TryGetValue("MAIN.TemperatureDegC", out var tempVal))
        {
            if (tempVal is double temp && temp >= PlcTemperatureCriticalThreshold)
            {
                return new TriggerEvaluation
                {
                    Triggered = true,
                    Type = TriggerType.ThresholdBreach,
                    Reason = $"CRITICAL: TwinCAT PLC temperature breached threshold ({temp:F1} °C >= {PlcTemperatureCriticalThreshold:F1} °C)",
                    Priority = EgressPriority.P0_CriticalAlarm,
                    SlicesToReport = new DataSliceSelection { IncludePlcTelemetry = true, IncludeEvents = true }
                };
            }
        }

        return TriggerEvaluation.NotTriggered(TriggerType.ThresholdBreach);
    }
}

public class StateChangeTrigger : ITelemetryTrigger
{
    public string Name => "OperationalStateTransition";
    public TriggerType Type => TriggerType.StateChange;

    public TriggerEvaluation Evaluate(TriggerContext context)
    {
        // Detect TwinCAT ADS runtime state change (e.g. RUN -> STOP or RUN -> ERROR)
        if (context.PreviousAdsState != 0 && context.CurrentAdsState != 0 &&
            context.PreviousAdsState != context.CurrentAdsState)
        {
            string oldStateName = GetAdsStateName(context.PreviousAdsState);
            string newStateName = GetAdsStateName(context.CurrentAdsState);

            var priority = (context.CurrentAdsState == AdsSimulationServer.ADSSTATE_ERROR || 
                            context.CurrentAdsState == AdsSimulationServer.ADSSTATE_STOP)
                ? EgressPriority.P0_CriticalAlarm
                : EgressPriority.P1_HighOperational;

            return new TriggerEvaluation
            {
                Triggered = true,
                Type = TriggerType.StateChange,
                Reason = $"TwinCAT ADS state transitioned from {oldStateName} ({context.PreviousAdsState}) to {newStateName} ({context.CurrentAdsState})",
                Priority = priority,
                SlicesToReport = new DataSliceSelection { IncludePlcTelemetry = true, IncludeEvents = true, IncludeDisk = true }
            };
        }

        return TriggerEvaluation.NotTriggered(TriggerType.StateChange);
    }

    private static string GetAdsStateName(ushort state) => state switch
    {
        AdsSimulationServer.ADSSTATE_RUN => "RUN",
        AdsSimulationServer.ADSSTATE_STOP => "STOP",
        AdsSimulationServer.ADSSTATE_RESET => "RESET",
        AdsSimulationServer.ADSSTATE_ERROR => "ERROR",
        AdsSimulationServer.ADSSTATE_CONFIG => "CONFIG",
        _ => $"State_{state}"
    };
}

public class OnDemandTrigger : ITelemetryTrigger
{
    public string Name => "ManualOnDemandRequest";
    public TriggerType Type => TriggerType.OnDemand;

    public TriggerEvaluation Evaluate(TriggerContext context)
    {
        if (context.ForceReport)
        {
            return new TriggerEvaluation
            {
                Triggered = true,
                Type = TriggerType.OnDemand,
                Reason = context.ForceReason ?? "On-demand telemetry requested by user/system",
                Priority = EgressPriority.P1_HighOperational,
                SlicesToReport = DataSliceSelection.FullSnapshot
            };
        }
        return TriggerEvaluation.NotTriggered(TriggerType.OnDemand);
    }
}

/// <summary>
/// Central evaluation engine coordinating industrial telemetry reporting triggers.
/// </summary>
public class TelemetryTriggerEngine
{
    private readonly ILogger<TelemetryTriggerEngine>? _logger;
    private readonly List<ITelemetryTrigger> _triggers = new();

    public IReadOnlyList<ITelemetryTrigger> Triggers => _triggers;
    public long TotalEvaluations { get; private set; }
    public long TotalTriggeredReports { get; private set; }
    public DateTimeOffset LastEvaluationTime { get; private set; } = DateTimeOffset.MinValue;
    public TriggerEvaluation? LastEvaluationResult { get; private set; }

    public TelemetryTriggerEngine(ILogger<TelemetryTriggerEngine>? logger = null)
    {
        _logger = logger;

        // Register default triggers
        _triggers.Add(new OnDemandTrigger());
        _triggers.Add(new StateChangeTrigger());
        _triggers.Add(new ThresholdTrigger());
        _triggers.Add(new HeartbeatTrigger(TimeSpan.FromSeconds(60)));
    }

    public void RegisterTrigger(ITelemetryTrigger trigger)
    {
        _triggers.Add(trigger);
    }

    public TriggerEvaluation Evaluate(TriggerContext context)
    {
        TotalEvaluations++;
        LastEvaluationTime = DateTimeOffset.UtcNow;

        var triggeredResults = new List<TriggerEvaluation>();

        foreach (var trigger in _triggers)
        {
            try
            {
                var result = trigger.Evaluate(context);
                if (result.Triggered)
                {
                    triggeredResults.Add(result);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error evaluating trigger {TriggerName}", trigger.Name);
            }
        }

        if (triggeredResults.Count == 0)
        {
            var none = new TriggerEvaluation
            {
                Triggered = false,
                Reason = "No trigger conditions met. Telemetry payload omitted.",
                Priority = EgressPriority.P3_LowInventory
            };
            LastEvaluationResult = none;
            return none;
        }

        TotalTriggeredReports++;

        // Combine all triggered slices and select highest priority (lower numeric value = higher priority)
        var highestPriority = triggeredResults.Min(r => r.Priority);
        var combinedSlices = new DataSliceSelection();
        var reasons = new List<string>();

        foreach (var r in triggeredResults)
        {
            combinedSlices.Merge(r.SlicesToReport);
            if (!string.IsNullOrEmpty(r.Reason))
            {
                reasons.Add($"[{r.Type}] {r.Reason}");
            }
        }

        var combined = new TriggerEvaluation
        {
            Triggered = true,
            Type = triggeredResults.First().Type,
            Priority = highestPriority,
            Reason = string.Join(" | ", reasons),
            SlicesToReport = combinedSlices
        };

        LastEvaluationResult = combined;
        _logger?.LogInformation("Telemetry Trigger fired: Priority={Priority}, Reason={Reason}",
            combined.Priority, combined.Reason);

        return combined;
    }
}
