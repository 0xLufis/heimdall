namespace App.Backend.Api.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Lua.Core;
using App.Shared.Lua.Models;

/// <summary>
/// Service coordinating sandboxed Lua script execution, plant rule lifecycle,
/// telemetry hooks, SLA evaluations, and dynamic UI extensions.
/// </summary>
public interface ILuaAutomationService
{
    Task<IReadOnlyList<LuaScriptRecord>> ListScriptsAsync(CancellationToken cancellationToken = default);
    Task<LuaScriptRecord?> GetScriptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LuaScriptRecord> CreateScriptAsync(LuaScriptRecord script, CancellationToken cancellationToken = default);
    Task<LuaScriptRecord?> UpdateScriptAsync(Guid id, LuaScriptRecord script, CancellationToken cancellationToken = default);
    Task<bool> DeleteScriptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LuaExecutionResult> RunScriptAsync(Guid id, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default);
    Task<LuaExecutionResult> TestExecuteAsync(string code, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default);
    IReadOnlyCollection<LuaUiExtension> GetUiExtensions(LuaUiTargetZone? zone = null);
    Task<LuaExecutionResult> InvokeUiActionAsync(string extensionId, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LuaExecutionResult>> EvaluateTelemetryChangeAsync(string machineId, string metric, double value, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LuaExecutionResult>> EvaluateSlaPreventiveRulesAsync(CancellationToken cancellationToken = default);
}
