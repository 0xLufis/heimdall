namespace App.Shared.TypeSystem;

using System;
using System.Text.RegularExpressions;
using App.Shared.Protos.Telemetry;

/// <summary>
/// Deterministic sanitizer for PLC symbol names, member variables, and IEC 61131-3 type mapper.
/// </summary>
public static partial class PlcTypeSanitizer
{
    [GeneratedRegex(@"[^a-zA-Z0-9_]")]
    private static partial Regex InvalidIdentifierCharRegex();

    [GeneratedRegex(@"^(POINTER\s+TO|REFERENCE\s+TO|INTERFACE)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex PointerOrRefRegex();

    [GeneratedRegex(@"_{2,}")]
    private static partial Regex MultiUnderscoreRegex();

    /// <summary>
    /// Sanitizes raw PLC symbol paths and member names to safe, deterministic identifiers for C#/TS.
    /// Example: "MAIN.Station1.Telemetry._data[1].fActualSpeed#1" -> "Main_Station1_Telemetry_data_1_fActualSpeed_1"
    /// </summary>
    public static string SanitizeIdentifier(string plcIdentifier)
    {
        if (string.IsNullOrWhiteSpace(plcIdentifier)) return "Unknown";

        // Remove pointer/reference/interface prefix
        string cleaned = PointerOrRefRegex().Replace(plcIdentifier, string.Empty).Trim();

        // Replace dots, array brackets, and hashes with underscores
        cleaned = cleaned.Replace('.', '_').Replace('[', '_').Replace(']', '_').Replace('#', '_');

        // Strip illegal characters
        cleaned = InvalidIdentifierCharRegex().Replace(cleaned, "_");

        // Collapse multiple consecutive underscores into a single underscore
        cleaned = MultiUnderscoreRegex().Replace(cleaned, "_");

        // Prefix with underscore if starting with a digit
        if (char.IsDigit(cleaned[0]))
        {
            cleaned = "_" + cleaned;
        }

        return cleaned;
    }

    /// <summary>
    /// Maps IEC 61131-3 PLC data type strings to canonical Protobuf DataTypeClassifier.
    /// </summary>
    public static DataTypeClassifier MapPlcTypeToClassifier(string plcType)
    {
        if (string.IsNullOrWhiteSpace(plcType)) return DataTypeClassifier.TypeUnspecified;

        var upper = plcType.Trim().ToUpperInvariant();

        if (upper.StartsWith("ARRAY")) return DataTypeClassifier.TypeArray;
        if (upper.StartsWith("STRING") || upper.StartsWith("WSTRING")) return DataTypeClassifier.TypeString;

        return upper switch
        {
            "BOOL" => DataTypeClassifier.TypeBool,
            "SINT" or "BYTE" => DataTypeClassifier.TypeInt8,
            "USINT" => DataTypeClassifier.TypeUint8,
            "INT" => DataTypeClassifier.TypeInt16,
            "UINT" or "WORD" => DataTypeClassifier.TypeUint16,
            "DINT" => DataTypeClassifier.TypeInt32,
            "UDINT" or "DWORD" => DataTypeClassifier.TypeUint32,
            "LINT" => DataTypeClassifier.TypeInt64,
            "ULINT" or "LWORD" => DataTypeClassifier.TypeUint64,
            "REAL" => DataTypeClassifier.TypeFloat32,
            "LREAL" => DataTypeClassifier.TypeFloat64,
            "TIME" or "LTIME" or "TIME_OF_DAY" or "TOD" => DataTypeClassifier.TypeDuration,
            "DATE" or "DATE_AND_TIME" or "DT" => DataTypeClassifier.TypeDatetime,
            _ => DataTypeClassifier.TypeStruct
        };
    }
}
