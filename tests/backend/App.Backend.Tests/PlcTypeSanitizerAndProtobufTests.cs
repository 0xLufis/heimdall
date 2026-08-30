using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using App.Agent.Daemon.Infrastructure.Beckhoff;
using App.Agent.Daemon.Runtime.Evaluation;
using App.Shared.Protos.Telemetry;
using App.Shared.TypeSystem;
using StructValue = App.Shared.Protos.Telemetry.StructValue;
using ListValue = App.Shared.Protos.Telemetry.ListValue;
using Xunit;

namespace App.Backend.Tests;

public class PlcTypeSanitizerAndProtobufTests
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TestPlcSpindleStruct
    {
        public ulong SequenceId;
        public double Velocity;
        public float Temp;
        public byte State;
        public bool IsRunning;
    }

    [Theory]
    [InlineData("MAIN.Station1.Telemetry._data", "MAIN_Station1_Telemetry_data")]
    [InlineData("POINTER TO ST_Spindle[0].fVelocity#1", "ST_Spindle_0_fVelocity_1")]
    [InlineData("REFERENCE TO ST_Motor", "ST_Motor")]
    [InlineData("1stSpindle.Speed", "_1stSpindle_Speed")]
    [InlineData("MAIN.Line[2].Sensor#Active", "MAIN_Line_2_Sensor_Active")]
    public void Sanitizer_CleansPlcSymbolPathsAndIdentifiers(string input, string expected)
    {
        string actual = PlcTypeSanitizer.SanitizeIdentifier(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("BOOL", DataTypeClassifier.TypeBool)]
    [InlineData("SINT", DataTypeClassifier.TypeInt8)]
    [InlineData("USINT", DataTypeClassifier.TypeUint8)]
    [InlineData("INT", DataTypeClassifier.TypeInt16)]
    [InlineData("UINT", DataTypeClassifier.TypeUint16)]
    [InlineData("DINT", DataTypeClassifier.TypeInt32)]
    [InlineData("UDINT", DataTypeClassifier.TypeUint32)]
    [InlineData("LINT", DataTypeClassifier.TypeInt64)]
    [InlineData("ULINT", DataTypeClassifier.TypeUint64)]
    [InlineData("REAL", DataTypeClassifier.TypeFloat32)]
    [InlineData("LREAL", DataTypeClassifier.TypeFloat64)]
    [InlineData("STRING(80)", DataTypeClassifier.TypeString)]
    [InlineData("TIME", DataTypeClassifier.TypeDuration)]
    [InlineData("DATE_AND_TIME", DataTypeClassifier.TypeDatetime)]
    [InlineData("ARRAY [1..10] OF INT", DataTypeClassifier.TypeArray)]
    [InlineData("ST_Station1TelemetryData", DataTypeClassifier.TypeStruct)]
    public void Sanitizer_MapsIecTypesToProtobufClassifier(string plcType, DataTypeClassifier expected)
    {
        var actual = PlcTypeSanitizer.MapPlcTypeToClassifier(plcType);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TypedTelemetryConverter_SerializesScalarsAndCollections()
    {
        // 1. Primitive scalar
        var scalarVal = TypedTelemetryConverter.ToTelemetryValue(12345L);
        Assert.Equal(12345L, scalarVal.Int64Value);

        // 2. Dictionary / Struct Value
        var dict = new Dictionary<string, object>
        {
            { "spindleSpeed", 1500.5 },
            { "alarmActive", false }
        };

        var structVal = TypedTelemetryConverter.ToTelemetryValue(dict);
        Assert.NotNull(structVal.StructValue);
        Assert.Equal(1500.5, structVal.StructValue.Fields["spindleSpeed"].DoubleValue);
        Assert.False(structVal.StructValue.Fields["alarmActive"].BoolValue);

        // 3. List Value
        var list = new List<int> { 10, 20, 30 };
        var listVal = TypedTelemetryConverter.ToTelemetryValue(list);
        Assert.NotNull(listVal.ListValue);
        Assert.Equal(3, listVal.ListValue.Elements.Count);
        Assert.Equal(20, listVal.ListValue.Elements[1].Int32Value);
    }

    [Fact]
    public void StructTelemetryBinder_UnmarshalsRawMemoryIntoProtobufDataPoint()
    {
        var rawStruct = new TestPlcSpindleStruct
        {
            SequenceId = 42,
            Velocity = 2500.75,
            Temp = 45.2f,
            State = 2,
            IsRunning = true
        };

        // Serialize struct to raw byte buffer (simulating TwinCAT ADS binary read)
        byte[] buffer = new byte[Marshal.SizeOf<TestPlcSpindleStruct>()];
        MemoryMarshal.Write(buffer, in rawStruct);

        // Unmarshal via StructTelemetryBinder
        var dataPoint = StructTelemetryBinder.BindStructToDataPoint<TestPlcSpindleStruct>(
            pointId: "spindle_diag",
            canonicalKey: "Beckhoff.Ads:192.168.1.100.1.1:851:MAIN.Station1.Telemetry._data",
            rawBuffer: buffer,
            structConverter: s =>
            {
                var fields = new StructValue();
                fields.Fields["sequenceId"] = new TelemetryValue { Uint64Value = s.SequenceId };
                fields.Fields["velocity"] = new TelemetryValue { DoubleValue = s.Velocity };
                fields.Fields["temp"] = new TelemetryValue { FloatValue = s.Temp };
                fields.Fields["state"] = new TelemetryValue { Uint32Value = s.State };
                fields.Fields["isRunning"] = new TelemetryValue { BoolValue = s.IsRunning };
                return fields;
            },
            plcTypeName: "ST_SpindleTelemetry"
        );

        Assert.Equal(QualityCode.QualityGood, dataPoint.Quality);
        Assert.Equal("spindle_diag", dataPoint.PointId);
        Assert.NotNull(dataPoint.Value.StructValue);
        Assert.Equal(42UL, dataPoint.Value.StructValue.Fields["sequenceId"].Uint64Value);
        Assert.Equal(2500.75, dataPoint.Value.StructValue.Fields["velocity"].DoubleValue);
        Assert.InRange(dataPoint.Value.StructValue.Fields["temp"].FloatValue, 45.19f, 45.21f);
        Assert.True(dataPoint.Value.StructValue.Fields["isRunning"].BoolValue);
    }
}
