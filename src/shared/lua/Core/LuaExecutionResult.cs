namespace App.Shared.Lua.Core;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using App.Shared.Lua.Models;

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

    /// <summary>UI extensions declared by the script during this run (consumed by the automation service).</summary>
    [JsonIgnore]
    public IReadOnlyList<LuaUiExtension> UiExtensions { get; init; } = Array.Empty<LuaUiExtension>();

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

    /// <summary>Maximum number of captured print lines.</summary>
    public int MaxOutputLines { get; init; } = 500;

    /// <summary>Maximum allowed script length in characters.</summary>
    public int MaxScriptLength { get; init; } = 65_536;

    /// <summary>
    /// Maximum bytes the script may allocate on its executing thread (cumulative). Bounds memory
    /// bombs such as repeated string doubling or unbounded table growth.
    /// </summary>
    public long MaxAllocatedBytes { get; init; } = 128L * 1024 * 1024;

    /// <summary>Maximum nesting of CLR→Lua callbacks (sort comparators, gsub functions, API handlers).</summary>
    public int MaxCallbackDepth { get; init; } = 8;

    /// <summary>Maximum length of a string produced by string.rep.</summary>
    public int MaxStringLength { get; init; } = 1_000_000;

    /// <summary>Maximum number of values (table entries) converted when returning results to the host.</summary>
    public int MaxReturnValueNodes { get; init; } = 10_000;

    /// <summary>Extra time the host waits beyond <see cref="MaxExecutionTime"/> before abandoning a run
    /// that is stuck inside a single CLR call the instruction guard cannot interrupt.</summary>
    public TimeSpan AbandonGracePeriod { get; init; } = TimeSpan.FromSeconds(2);
}
