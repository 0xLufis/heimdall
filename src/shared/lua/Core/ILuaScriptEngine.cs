namespace App.Shared.Lua.Core;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;

/// <summary>
/// Core contract for the Heimdall sandboxed Lua scripting platform.
/// Exposes safe execution with time/memory/re-entrancy budgets and domain API injection.
/// </summary>
public interface ILuaScriptEngine
{
    ILuaPlantDataProvider DataProvider { get; }

    /// <summary>
    /// Executes arbitrary Lua source code inside a fresh isolated sandbox.
    /// </summary>
    Task<LuaExecutionResult> ExecuteAsync(
        string code,
        Dictionary<string, object?>? globalVariables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a stored script and records last-execution telemetry on the record (caller persists it).
    /// </summary>
    Task<LuaExecutionResult> ExecuteScriptRecordAsync(
        LuaScriptRecord scriptRecord,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-executes <paramref name="ownerScript"/> in a fresh sandbox and invokes the handler it registers
    /// for <paramref name="extensionId"/> with the given context, within the same execution budget.
    /// </summary>
    Task<LuaExecutionResult> InvokeUiActionAsync(
        LuaScriptRecord ownerScript,
        string extensionId,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default);
}
