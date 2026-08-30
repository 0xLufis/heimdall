using System;
using System.Collections.Generic;
using App.Agent.Daemon.Domain.Recipes;
using App.Agent.Daemon.Infrastructure.Modbus;
using App.Agent.Daemon.Runtime.Evaluation;
using App.Agent.Daemon.Runtime.Merger;
using Xunit;

namespace App.Backend.Tests;

public class DeadbandAndThrottlingTests
{
    [Fact]
    public void DeadbandEvaluator_AbsoluteThreshold_FiltersSmallChanges()
    {
        var evaluator = new DeadbandEvaluator();
        var strategy = new PollingStrategyConfig(
            StrategyType: PollingStrategyType.Periodic,
            IntervalMs: 1000,
            Deadband: new DeadbandConfig(DeadbandType.Absolute, 2.0),
            MaxQuietPeriodMs: 60000
        );

        var t0 = DateTimeOffset.UtcNow;
        var r1 = new MergedDataReading("temp:1", 50.0, t0, typeof(double));
        var r2 = new MergedDataReading("temp:1", 51.0, t0.AddSeconds(1), typeof(double)); // Diff 1.0 < 2.0 -> Discard
        var r3 = new MergedDataReading("temp:1", 52.5, t0.AddSeconds(2), typeof(double)); // Diff 2.5 >= 2.0 -> Emit

        // 1st reading always emits
        Assert.True(evaluator.ShouldEmit("temp:1", r1, strategy, out _));

        // 2nd reading suppressed (delta = 1.0 < 2.0)
        Assert.False(evaluator.ShouldEmit("temp:1", r2, strategy, out _));

        // 3rd reading emits (delta = 2.5 >= 2.0)
        Assert.True(evaluator.ShouldEmit("temp:1", r3, strategy, out _));
    }

    [Fact]
    public void DeadbandEvaluator_HeartbeatTTL_ForcesEmissionWhenUnchanged()
    {
        var evaluator = new DeadbandEvaluator();
        var strategy = new PollingStrategyConfig(
            StrategyType: PollingStrategyType.Periodic,
            IntervalMs: 1000,
            Deadband: new DeadbandConfig(DeadbandType.Absolute, 5.0),
            MaxQuietPeriodMs: 10000 // 10s max quiet period
        );

        var t0 = DateTimeOffset.UtcNow;
        var r1 = new MergedDataReading("spindle:rpm", 1500.0, t0, typeof(double));
        var r2 = new MergedDataReading("spindle:rpm", 1501.0, t0.AddSeconds(5), typeof(double));
        var r3 = new MergedDataReading("spindle:rpm", 1501.0, t0.AddSeconds(11), typeof(double)); // 11s > 10s TTL

        Assert.True(evaluator.ShouldEmit("spindle:rpm", r1, strategy, out _));
        Assert.False(evaluator.ShouldEmit("spindle:rpm", r2, strategy, out _));

        // Forced heartbeat emission after quiet TTL
        bool emitted = evaluator.ShouldEmit("spindle:rpm", r3, strategy, out bool isHeartbeat);
        Assert.True(emitted);
        Assert.True(isHeartbeat);
    }

    [Fact]
    public void ModbusEndiannessConverter_DecodesCdabFloatAndBatchesRegisters()
    {
        // 123.456f in IEEE 754 Big-Endian is: 0x42, 0xF6, 0xE9, 0x79
        // In CDAB (word-swapped) representation: Reg0 = 0xE979 (59769), Reg1 = 0x42F6 (17142)
        ushort reg0 = 0xE979;
        ushort reg1 = 0x42F6;

        float result = ModbusEndiannessConverter.RegistersToFloat32(reg0, reg1, EndiannessFormat.CDAB_MidBigEndian_WordSwap);
        Assert.InRange(result, 123.455f, 123.457f);

        // Test contiguous register block optimizer
        var tagRequests = new List<ModbusTagRequest>
        {
            new("Speed", 100, 2),
            new("Temp", 102, 2),
            new("Pressure", 105, 2), // Small gap of 1 register
            new("HighVoltage", 200, 2) // Large gap -> separate block
        };

        var blocks = ModbusEndiannessConverter.OptimizeRegisterBlocks(tagRequests, maxBlockSize: 50, maxGapAllowance: 5);

        // Should produce 2 optimized blocks
        Assert.Equal(2, blocks.Count);
        Assert.Equal(100, blocks[0].StartAddress);
        Assert.Equal(7, blocks[0].TotalLength); // Address 100 to 107
        Assert.Equal(3, blocks[0].Tags.Count);

        Assert.Equal(200, blocks[1].StartAddress);
        Assert.Equal(2, blocks[1].TotalLength);
        Assert.Single(blocks[1].Tags);
    }
}
