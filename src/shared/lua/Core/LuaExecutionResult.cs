namespace App.Shared.Lua.Core;

using System;
using System.Collections.Generic;

/// <summary>
/// Result of a Lua script execution inside the sandbox.
/// </summary>
public record LuaExecutionResult
{
    /// <summary>Whether script execution succeeded without unhandled errors.</summary>
    public bool Success { get; init; }

    /// <summary>The return value of the script (converted to a CLR object if applicable).</summary>
    public object? ReturnValue { get; init; }

    /// <summary>Captured standard output (print statements) from the script.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>Detailed error message if execution failed.</summary>
    public string? Error { get; init; }

    /// <summary>Wall-clock execution duration.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>List of logs captured during execution.</summary>
    public List<string> Logs { get; init; } = new();

    public static LuaExecutionResult FromError(string error, List<string>? logs = null)
    {
        return new LuaExecutionResult
        {
            Success = false,
            Error = error,
            Logs = logs ?? new List<string>()
        };
    }

    public static LuaExecutionResult FromSuccess(object? value, List<string>? logs = null, string output = "")
    {
        return new LuaExecutionResult
        {
            Success = true,
            ReturnValue = value,
            Output = output,
            Logs = logs ?? new List<string>()
        };
    }
}

/// <summary>
/// Configuration options for the sandboxed Lua runtime.
/// </summary>
public record LuaSandboxOptions
{
    /// <summary>Maximum wall-clock execution time allowed for a single script run before timeout.</summary>
    public TimeSpan MaxExecutionTime { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Whether to allow print statements to be captured in the execution result.</summary>
    public bool CaptureOutput { get; init; } = true;

    /// <summary>Maximum output buffer length in characters to prevent memory exhaustion.</summary>
    public int MaxOutputLength { get; init; } = 100_000;

    /// <summary>Maximum allowed script length in characters.</summary>
    public int MaxScriptLength { get; init; } = 65_536;

    /// <summary>Default plant or organization context ID.</summary>
    public string? DefaultOrganizationId { get; init; }
}
