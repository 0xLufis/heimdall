namespace App.Agent.Daemon.Reporting.Triggers;

using System;

/// <summary>
/// Abstraction for the trigger evaluation engine determining telemetry reporting egress.
/// Follows Interface-Implementation model and Dependency Inversion Principle.
/// </summary>
public interface ITelemetryTriggerEngine
{
    long TotalEvaluations { get; }
    long TotalTriggeredReports { get; }
    DateTimeOffset LastEvaluationTime { get; }
    TriggerEvaluation? LastEvaluationResult { get; }
    void RegisterTrigger(ITelemetryTrigger trigger);
    TriggerEvaluation Evaluate(TriggerContext context);
}
