namespace App.Agent.Daemon.Interfaces;

using System;
using System.Threading.Tasks;

/// <summary>
/// Service contract for resilient offline telemetry buffering and spool draining.
/// </summary>
public interface ITelemetrySpooler
{
    /// <summary>
    /// Buffers a JSON payload to local encrypted/isolated disk storage.
    /// </summary>
    Task SpoolPayloadAsync(string payloadJson);

    /// <summary>
    /// Returns the number of spooled payloads waiting to be dispatched.
    /// </summary>
    int PendingCount { get; }

    /// <summary>
    /// Drains queued payloads sequentially, removing items only upon confirmed delivery. Returns number of drained items.
    /// </summary>
    Task<int> DrainSpoolAsync(Func<string, Task<bool>> sendDelegate);
}
