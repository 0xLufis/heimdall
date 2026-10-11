namespace App.Agent.Daemon.Extensions;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using App.Agent.Daemon.Interfaces;
using App.Shared.Extensions;
using App.Shared.Sanitization;
using Microsoft.Extensions.Logging;

public class ExtensionRegistry : IExtensionRegistry
{
    private record RegisteredEntry(CustomComponentSubmission Submission, DateTimeOffset ExpiresAt, long MonotonicExpiryTimestamp);

    private readonly IConfigurationService _configService;
    private readonly ILogger<ExtensionRegistry> _logger;
    private readonly ConcurrentDictionary<string, RegisteredEntry> _components = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<CustomTelemetrySubmission> _telemetryQueue = new();
    private readonly ConcurrentQueue<CustomEventSubmission> _eventQueue = new();

    public ExtensionRegistry(IConfigurationService configService, ILogger<ExtensionRegistry> logger)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool RegisterOrUpdateComponent(CustomComponentSubmission submission, out string? error)
    {
        error = null;
        if (submission == null)
        {
            error = "Submission cannot be null.";
            _logger.LogWarning("Component registration rejected: submission payload is null.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(submission.ComponentName))
        {
            error = "ComponentName is required.";
            _logger.LogWarning("Component registration rejected: ComponentName is required.");
            return false;
        }

        // Sanitize component name to safe token
        string safeName = StringSanitizer.ToSafeToken(submission.ComponentName);
        submission.ComponentName = safeName;
        submission.Technology = StringSanitizer.ToSafeToken(submission.Technology, fallback: "CustomPlugin");
        submission.ComponentType = StringSanitizer.ToSafeToken(submission.ComponentType, fallback: "sensor");

        if (!string.IsNullOrEmpty(submission.ParentComponentName))
        {
            submission.ParentComponentName = StringSanitizer.StripCrlf(submission.ParentComponentName);
        }

        // Serialize Data if DataJson not provided
        if (string.IsNullOrEmpty(submission.DataJson) && submission.Data != null)
        {
            try
            {
                submission.DataJson = JsonSerializer.Serialize(submission.Data);
            }
            catch (Exception ex)
            {
                error = $"Failed to serialize component data: {ex.Message}";
                _logger.LogWarning(ex, "Component registration rejected for '{ComponentName}': data serialization failure.", safeName);
                return false;
            }
        }

        // Enforce maximum payload size
        int maxBytes = _configService.Config.ExtensionPayloadMaxBytes;
        if (!string.IsNullOrEmpty(submission.DataJson) && submission.DataJson.Length > maxBytes)
        {
            error = $"Payload size ({submission.DataJson.Length} bytes) exceeds configured limit ({maxBytes} bytes).";
            _logger.LogWarning("Component registration rejected for '{ComponentName}': payload size {Size} exceeds limit {Limit}.", safeName, submission.DataJson.Length, maxBytes);
            return false;
        }

        // Monotonic timer guarding against system clock manipulation / time drift
        int ttlSeconds = submission.TtlSeconds ?? _configService.Config.DefaultExtensionTtlSeconds;
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);
        long monotonicExpiry = System.Diagnostics.Stopwatch.GetTimestamp() + (long)(ttlSeconds * (double)System.Diagnostics.Stopwatch.Frequency);

        var entry = new RegisteredEntry(submission, expiresAt, monotonicExpiry);
        _components[safeName] = entry;

        _logger.LogInformation("Registered custom component '{ComponentName}' (Type: {Type}, Technology: {Tech}, TTL: {Ttl}s)",
            safeName, submission.ComponentType, submission.Technology, ttlSeconds);

        return true;
    }

    public bool RemoveComponent(string componentName)
    {
        string safeName = StringSanitizer.ToSafeToken(componentName);
        bool removed = _components.TryRemove(safeName, out _);
        if (removed)
        {
            _logger.LogInformation("Removed custom component '{ComponentName}'", safeName);
        }
        return removed;
    }

    public IReadOnlyList<CustomComponentSubmission> GetActiveComponents()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        long nowTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        var active = new List<CustomComponentSubmission>();

        foreach (var kvp in _components)
        {
            // Verify expiry against both UTC clock and monotonic hardware timestamp
            if (nowUtc < kvp.Value.ExpiresAt && nowTimestamp < kvp.Value.MonotonicExpiryTimestamp)
            {
                active.Add(kvp.Value.Submission);
            }
            else
            {
                // Expired: prune lazily
                _components.TryRemove(kvp.Key, out _);
            }
        }

        return active;
    }

    public void EnqueueTelemetry(CustomTelemetrySubmission telemetry)
    {
        if (telemetry != null && telemetry.Metrics.Any())
        {
            _telemetryQueue.Enqueue(telemetry);
        }
    }

    public void EnqueueEvent(CustomEventSubmission customEvent)
    {
        if (customEvent != null && !string.IsNullOrWhiteSpace(customEvent.Message))
        {
            _eventQueue.Enqueue(customEvent);
        }
    }

    public IReadOnlyList<CustomTelemetrySubmission> DrainTelemetry()
    {
        var list = new List<CustomTelemetrySubmission>();
        while (_telemetryQueue.TryDequeue(out var item))
        {
            list.Add(item);
        }
        return list;
    }

    public IReadOnlyList<CustomEventSubmission> DrainEvents()
    {
        var list = new List<CustomEventSubmission>();
        while (_eventQueue.TryDequeue(out var item))
        {
            list.Add(item);
        }
        return list;
    }
}
