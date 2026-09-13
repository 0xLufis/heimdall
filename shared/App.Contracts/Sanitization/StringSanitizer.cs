namespace App.Shared.Sanitization;

using System;
using System.Text.RegularExpressions;

/// <summary>
/// General string sanitizer providing CRLF neutralization, WQL/SQL query escaping,
/// and safe identifier enforcement.
/// </summary>
public static partial class StringSanitizer
{
    [GeneratedRegex(@"[\r\n\t]")]
    private static partial Regex CrlfRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9_\-\.]")]
    private static partial Regex UnsafeCharRegex();

    /// <summary>
    /// Strips carriage returns, line feeds, and tabs to prevent log/header/gRPC injection.
    /// </summary>
    public static string StripCrlf(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return CrlfRegex().Replace(input, " ").Trim();
    }

    /// <summary>
    /// Escapes string literals for safe inclusion in WMI / WQL queries.
    /// Replaces single backslashes with double backslashes and single quotes with escaped quotes.
    /// </summary>
    public static string EscapeWqlLiteral(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Replace("\\", "\\\\").Replace("'", "\\'");
    }

    /// <summary>
    /// Normalizes a string to a safe alphanumeric, hyphen, dot, and underscore token.
    /// </summary>
    public static string ToSafeToken(string? input, string fallback = "unknown")
    {
        if (string.IsNullOrWhiteSpace(input)) return fallback;
        string cleaned = UnsafeCharRegex().Replace(input, "_").Trim('_');
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }
}
