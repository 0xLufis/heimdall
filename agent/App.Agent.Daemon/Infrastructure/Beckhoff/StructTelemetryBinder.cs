namespace App.Agent.Daemon.Infrastructure.Beckhoff;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using App.Shared.Protos.Telemetry;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using StructValue = App.Shared.Protos.Telemetry.StructValue;

/// <summary>
/// High-performance zero-allocation struct unmarshaller converting raw ADS byte memory buffers into TelemetryDataPoint messages.
/// </summary>
public static class StructTelemetryBinder
{
    public static TelemetryDataPoint BindStructToDataPoint<T>(
        string pointId,
        string canonicalKey,
        ReadOnlySpan<byte> rawBuffer,
        Func<T, StructValue> structConverter,
        string plcTypeName) where T : struct
    {
        if (rawBuffer.Length < Unsafe.SizeOf<T>())
        {
            return new TelemetryDataPoint
            {
                PointId = pointId,
                CanonicalKey = canonicalKey,
                Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                Quality = QualityCode.QualityBad,
                TypeDescriptor = new TypeDescriptor
                {
                    Classifier = DataTypeClassifier.TypeStruct,
                    OriginalPlcType = plcTypeName
                }
            };
        }

        T instance = MemoryMarshal.Read<T>(rawBuffer);
        StructValue structValue = structConverter(instance);

        return new TelemetryDataPoint
        {
            PointId = pointId,
            CanonicalKey = canonicalKey,
            Timestamp = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Quality = QualityCode.QualityGood,
            Value = new TelemetryValue { StructValue = structValue },
            TypeDescriptor = new TypeDescriptor
            {
                Classifier = DataTypeClassifier.TypeStruct,
                OriginalPlcType = plcTypeName
            },
            IsDelta = false
        };
    }
}
