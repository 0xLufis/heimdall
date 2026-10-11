namespace App.Agent.Daemon.Runtime.Evaluation;

using System;
using System.Collections;
using System.Collections.Generic;
using App.Shared.Protos.Telemetry;
using Google.Protobuf;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using StructValue = App.Shared.Protos.Telemetry.StructValue;
using ListValue = App.Shared.Protos.Telemetry.ListValue;

/// <summary>
/// Converts arbitrary C# runtime objects into strongly-typed Protobuf TelemetryValue messages.
/// </summary>
public static class TypedTelemetryConverter
{
    public static TelemetryValue ToTelemetryValue(object? value)
    {
        if (value == null)
        {
            return new TelemetryValue();
        }

        switch (value)
        {
            case bool b:
                return new TelemetryValue { BoolValue = b };

            case sbyte sb:
                return new TelemetryValue { Int32Value = sb };

            case byte ub:
                return new TelemetryValue { Uint32Value = ub };

            case short s:
                return new TelemetryValue { Int32Value = s };

            case ushort us:
                return new TelemetryValue { Uint32Value = us };

            case int i:
                return new TelemetryValue { Int32Value = i };

            case uint ui:
                return new TelemetryValue { Uint32Value = ui };

            case long l:
                return new TelemetryValue { Int64Value = l };

            case ulong ul:
                return new TelemetryValue { Uint64Value = ul };

            case float f:
                return new TelemetryValue { FloatValue = f };

            case double d:
                return new TelemetryValue { DoubleValue = d };

            case string str:
                return new TelemetryValue { StringValue = str };

            case byte[] bytes:
                return new TelemetryValue { BytesValue = ByteString.CopyFrom(bytes) };

            case DateTime dt:
                return new TelemetryValue { TimestampValue = Timestamp.FromDateTime(dt.ToUniversalTime()) };

            case DateTimeOffset dto:
                return new TelemetryValue { TimestampValue = Timestamp.FromDateTimeOffset(dto) };

            case IDictionary<string, object> dict:
                var structVal = new StructValue();
                foreach (var kvp in dict)
                {
                    structVal.Fields[kvp.Key] = ToTelemetryValue(kvp.Value);
                }
                return new TelemetryValue { StructValue = structVal };

            case IEnumerable list when value is not string:
                var listVal = new ListValue();
                foreach (var item in list)
                {
                    listVal.Elements.Add(ToTelemetryValue(item));
                }
                return new TelemetryValue { ListValue = listVal };

            default:
                // Fallback to string representation for unrecognized types
                return new TelemetryValue { StringValue = value.ToString() ?? string.Empty };
        }
    }
}
