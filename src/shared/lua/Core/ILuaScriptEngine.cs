namespace App.Shared.Lua.Core;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Lua.Apis;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Models;

/// <summary>
/// Core contract for the Heimdall sandboxed Lua scripting platform.
/// Exposes safe execution, timeout enforcement, domain API injection,
/// and dynamic UI extensions.
/// </summary>
public interface ILuaScriptEngine
{
    ILuaPlantDataProvider DataProvider { get; }
    MachinesLuaApi MachinesApi { get; }
    GroupingsLuaApi GroupingsApi { get; }
    TelemetryLuaApi TelemetryApi { get; }
    TicketsLuaApi TicketsApi { get; }
    UiLuaApi UiApi { get; }
    EventsLuaApi EventsApi { get; }

    /// <summary>
    /// Configures a clean sandboxed MoonSharp script environment with domain APIs and log capture.
    /// </summary>
    MoonSharp.Interpreter.Script CreateSandboxedScript(List<string> logs);

    /// <summary>
    /// Executes arbitrary Lua source code inside the isolated sandbox.
    /// </summary>
    Task<LuaExecutionResult> ExecuteAsync(
        string code,
        Dictionary<string, object?>? globalVariables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a registered LuaScriptRecord and records execution telemetry.
    /// </summary>
    Task<LuaExecutionResult> ExecuteScriptRecordAsync(
        LuaScriptRecord scriptRecord,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns dynamic UI extensions registered by plant Lua scripts.
    /// </summary>
    IReadOnlyCollection<LuaUiExtension> GetUiExtensions(LuaUiTargetZone? zone = null);

    /// <summary>
    /// Invokes the registered Lua callback for a dynamic UI action.
    /// </summary>
    Task<LuaExecutionResult> InvokeUiActionAsync(
        string extensionId,
        Dictionary<string, object?>? context = null,
        CancellationToken cancellationToken = default);
}
