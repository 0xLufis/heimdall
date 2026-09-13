namespace App.Shared.Sanitization;

using System;
using System.Linq;
using App.Shared.Errors;

/// <summary>
/// Command input sanitizer protecting against shell injection, process argument tampering,
/// and unescaped command chaining.
/// </summary>
public static class CommandSanitizer
{
    private static readonly char[] ShellMetacharacters =
    {
        '&', '|', ';', '`', '$', '>', '<', '\n', '\r', '(', ')'
    };

    private static readonly string[] DangerousPayloadKeywords =
    {
        "rm -rf", "cmd.exe", "powershell -enc", "powershell -e", "sh -c", "bash -c",
        "curl http", "wget http", "nc -e", "netcat", "eval(", "exec("
    };

    /// <summary>
    /// Validates an argument string or payload for shell injection hazards.
    /// </summary>
    /// <param name="commandString">The input string to validate.</param>
    /// <param name="errorCode">The failure code if invalid; otherwise ErrorCode.None.</param>
    /// <returns>True if safe from injection characters; false otherwise.</returns>
    public static bool IsSafeCommandString(string? commandString, out ErrorCode errorCode)
    {
        if (string.IsNullOrWhiteSpace(commandString))
        {
            errorCode = ErrorCode.MissingRequiredField;
            return false;
        }

        // Check for shell chaining characters
        if (commandString.IndexOfAny(ShellMetacharacters) >= 0)
        {
            errorCode = ErrorCode.UnsafeCommandDetected;
            return false;
        }

        // Check for dangerous subshell/eval keywords
        string lower = commandString.ToLowerInvariant();
        if (DangerousPayloadKeywords.Any(kw => lower.Contains(kw)))
        {
            errorCode = ErrorCode.UnsafeCommandDetected;
            return false;
        }

        errorCode = ErrorCode.None;
        return true;
    }
}
