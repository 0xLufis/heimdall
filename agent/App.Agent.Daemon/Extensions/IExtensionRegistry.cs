namespace App.Agent.Daemon.Extensions;

using System.Collections.Generic;
using App.Shared.Extensions;

/// <summary>
/// Thread-safe registry for custom reporting components, telemetry metrics, and events.
/// </summary>
public interface IExtensionRegistry
{
    /// <summary>
    /// Registers or updates a custom component submission. Returns false if validation fails.
    /// </summary>
    bool RegisterOrUpdateComponent(CustomComponentSubmission submission, out string? error);

    /// <summary>
    /// Removes a custom component from reporting by its name.
    /// </summary>
    bool RemoveComponent(string componentName);

    /// <summary>
    /// Returns all currently unexpired, active custom components.
    /// </summary>
    IReadOnlyList<CustomComponentSubmission> GetActiveComponents();

    /// <summary>
    /// Enqueues custom telemetry metrics from outside sources.
    /// </summary>
    void EnqueueTelemetry(CustomTelemetrySubmission telemetry);

    /// <summary>
    /// Enqueues an operational event or alarm.
    /// </summary>
    void EnqueueEvent(CustomEventSubmission customEvent);

    /// <summary>
    /// Atomically drains all pending telemetry submissions.
    /// </summary>
    IReadOnlyList<CustomTelemetrySubmission> DrainTelemetry();

    /// <summary>
    /// Atomically drains all pending custom events.
    /// </summary>
    IReadOnlyList<CustomEventSubmission> DrainEvents();
}
