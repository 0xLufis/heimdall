using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.Extensions;
using App.Agent.Daemon.Reporting;
using App.Shared.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class ExtensionApiTests
{
    private readonly AgentConfig _config;
    private readonly ConfigurationService _configService;

    public ExtensionApiTests()
    {
        _config = new AgentConfig
        {
            DefaultExtensionTtlSeconds = 300,
            ExtensionPayloadMaxBytes = 1024 // 1 KB limit for testing
        };
        _configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, _config);
    }

    [Fact]
    public void ExtensionRegistry_RegistersAndRetrievesComponent()
    {
        var registry = new ExtensionRegistry(_configService, NullLogger<ExtensionRegistry>.Instance);

        var submission = new CustomComponentSubmission
        {
            ComponentName = "Pressure-Sensor-01",
            ComponentType = "sensor",
            Technology = "Modbus-RTU",
            ParentComponentName = "PLC-Beckhoff-CX2040",
            Data = new Dictionary<string, object>
            {
                ["pressure_bar"] = 4.82,
                ["flow_rate_lpm"] = 12.5
            },
            TtlSeconds = 60
        };

        bool success = registry.RegisterOrUpdateComponent(submission, out string? error);

        Assert.True(success);
        Assert.Null(error);

        var active = registry.GetActiveComponents();
        Assert.Single(active);
        Assert.Equal("Pressure-Sensor-01", active[0].ComponentName);
        Assert.Equal("PLC-Beckhoff-CX2040", active[0].ParentComponentName);
        Assert.Equal("Modbus-RTU", active[0].Technology);
        Assert.False(string.IsNullOrEmpty(active[0].DataJson));
        Assert.Contains("pressure_bar", active[0].DataJson);
    }

    [Fact]
    public async Task ExtensionRegistry_PrunesExpiredComponents_AfterTtl()
    {
        var registry = new ExtensionRegistry(_configService, NullLogger<ExtensionRegistry>.Instance);

        var shortLived = new CustomComponentSubmission
        {
            ComponentName = "Transient-Reading",
            ComponentType = "sensor",
            TtlSeconds = 1 // 1 second TTL
        };

        bool registered = registry.RegisterOrUpdateComponent(shortLived, out _);
        Assert.True(registered);

        // Immediately available
        Assert.Single(registry.GetActiveComponents());

        // Wait for TTL expiration
        await Task.Delay(1100);

        // Should be pruned
        var activeAfterDelay = registry.GetActiveComponents();
        Assert.Empty(activeAfterDelay);
    }

    [Fact]
    public void ExtensionRegistry_SanitizesComponentName_AndRejectsOversizedPayloads()
    {
        var registry = new ExtensionRegistry(_configService, NullLogger<ExtensionRegistry>.Instance);

        // 1. Sanitize unsafe characters
        var unsafeSubmission = new CustomComponentSubmission
        {
            ComponentName = "../../../Exploit#123;rm -rf",
            ComponentType = "sensor"
        };

        bool registered = registry.RegisterOrUpdateComponent(unsafeSubmission, out _);
        Assert.True(registered);
        var active = registry.GetActiveComponents();
        Assert.Single(active);
        // Only safe characters retained
        Assert.DoesNotContain("/", active[0].ComponentName);
        Assert.DoesNotContain(";", active[0].ComponentName);

        // 2. Oversized payload rejection
        var oversizedSubmission = new CustomComponentSubmission
        {
            ComponentName = "Oversized-Sensor",
            ComponentType = "sensor",
            DataJson = new string('A', 2048) // Exceeds 1024 bytes limit
        };

        bool rejected = registry.RegisterOrUpdateComponent(oversizedSubmission, out string? error);
        Assert.False(rejected);
        Assert.NotNull(error);
        Assert.Contains("exceeds configured limit", error);
    }

    [Fact]
    public void ExtensionRegistry_BuffersAndDrainsTelemetryAndEvents()
    {
        var registry = new ExtensionRegistry(_configService, NullLogger<ExtensionRegistry>.Instance);

        var telemetry = new CustomTelemetrySubmission
        {
            Source = "custom-sensor",
            Metrics = new List<CustomMetricPoint>
            {
                new() { Key = "temperature", Value = 42.5, Unit = "celsius" },
                new() { Key = "vibration", Value = 0.03, Unit = "g" }
            }
        };

        var customEvent = new CustomEventSubmission
        {
            Source = "custom-sensor",
            Level = "Warning",
            Message = "High temperature warning threshold exceeded"
        };

        registry.EnqueueTelemetry(telemetry);
        registry.EnqueueEvent(customEvent);

        var drainedTelemetry = registry.DrainTelemetry();
        var drainedEvents = registry.DrainEvents();

        Assert.Single(drainedTelemetry);
        Assert.Equal(2, drainedTelemetry[0].Metrics.Count);
        Assert.Single(drainedEvents);
        Assert.Equal("High temperature warning threshold exceeded", drainedEvents[0].Message);

        // Second drain should be empty
        Assert.Empty(registry.DrainTelemetry());
        Assert.Empty(registry.DrainEvents());
    }

    [Fact]
    public void ExtensionComponentContributor_YieldsComponentsToSystemInfoReporter()
    {
        var registry = new ExtensionRegistry(_configService, NullLogger<ExtensionRegistry>.Instance);

        registry.RegisterOrUpdateComponent(new CustomComponentSubmission
        {
            ComponentName = "Opto-Encoder-01",
            ComponentType = "sensor",
            Technology = "IO-Link",
            ParentComponentName = "Drive-Inverter-Master",
            Data = new Dictionary<string, object> { ["rpm"] = 1750 }
        }, out _);

        var contributor = new ExtensionComponentContributor(registry);

        var dummyData = new SystemInfoData();
        var components = contributor.CreateComponents(dummyData).ToList();

        Assert.Single(components);
        var comp = components[0];
        Assert.Equal("Opto-Encoder-01", comp.Name);
        Assert.Equal("sensor", comp.Type);
        Assert.Equal("IO-Link", comp.Technology);
        Assert.Contains("Drive-Inverter-Master", comp.DataJson);
        Assert.Contains("rpm", comp.DataJson);
    }
}
