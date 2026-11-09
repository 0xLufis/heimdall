namespace App.Backend.Api.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Lua.Apis;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using App.Shared.Lua.Examples;
using App.Shared.Lua.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public class LuaAutomationService : ILuaAutomationService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ILuaPlantDataProvider? _directDataProvider;
    private readonly ILogger<LuaAutomationService> _logger;
    private readonly UiLuaApi _uiApi = new();
    private readonly EventsLuaApi _eventsApi = new();
    private readonly ConcurrentDictionary<Guid, LuaScriptRecord> _scripts = new();

    public LuaAutomationService(
        IServiceScopeFactory scopeFactory,
        ILogger<LuaAutomationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        foreach (var script in DefaultPlantScripts.GetInitialScripts())
        {
            _scripts[script.Id] = script;
        }

        _ = InitializeUiExtensionsAsync();
    }

    public LuaAutomationService(
        ILuaPlantDataProvider dataProvider,
        ILogger<LuaAutomationService> logger)
    {
        _directDataProvider = dataProvider;
        _logger = logger;

        foreach (var script in DefaultPlantScripts.GetInitialScripts())
        {
            _scripts[script.Id] = script;
        }

        _ = InitializeUiExtensionsAsync();
    }

    private ILuaScriptEngine GetEngine()
    {
        if (_directDataProvider != null)
        {
            return new LuaScriptEngine(_directDataProvider, logger: null, uiApi: _uiApi, eventsApi: _eventsApi);
        }

        if (_scopeFactory != null)
        {
            var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetService(typeof(ILuaPlantDataProvider)) as ILuaPlantDataProvider
                ?? new InMemoryPlantDataProvider();
            return new LuaScriptEngine(provider, logger: null, uiApi: _uiApi, eventsApi: _eventsApi);
        }

        return new LuaScriptEngine(new InMemoryPlantDataProvider(), logger: null, uiApi: _uiApi, eventsApi: _eventsApi);
    }

    private async Task InitializeUiExtensionsAsync()
    {
        try
        {
            var uiScripts = _scripts.Values.Where(s => s.IsActive && s.TriggerType == LuaScriptTriggerType.UiExtension);
            var engine = GetEngine();
            foreach (var s in uiScripts)
            {
                await engine.ExecuteScriptRecordAsync(s);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Lua UI extensions on startup");
        }
    }

    public Task<IReadOnlyList<LuaScriptRecord>> ListScriptsAsync(CancellationToken cancellationToken = default)
    {
        var list = _scripts.Values.OrderByDescending(s => s.UpdatedAt).ToList();
        return Task.FromResult<IReadOnlyList<LuaScriptRecord>>(list);
    }

    public Task<LuaScriptRecord?> GetScriptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _scripts.TryGetValue(id, out var script);
        return Task.FromResult(script);
    }

    public Task<LuaScriptRecord> CreateScriptAsync(LuaScriptRecord script, CancellationToken cancellationToken = default)
    {
        if (script.Id == Guid.Empty)
            script.Id = Guid.NewGuid();

        script.CreatedAt = DateTimeOffset.UtcNow;
        script.UpdatedAt = DateTimeOffset.UtcNow;
        _scripts[script.Id] = script;

        if (script.IsActive && script.TriggerType == LuaScriptTriggerType.UiExtension)
        {
            _ = GetEngine().ExecuteScriptRecordAsync(script);
        }

        return Task.FromResult(script);
    }

    public Task<LuaScriptRecord?> UpdateScriptAsync(Guid id, LuaScriptRecord script, CancellationToken cancellationToken = default)
    {
        if (_scripts.TryGetValue(id, out var existing))
        {
            existing.Name = script.Name;
            existing.Description = script.Description;
            existing.TriggerType = script.TriggerType;
            existing.Code = script.Code;
            existing.IsActive = script.IsActive;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.Metadata = script.Metadata;

            if (existing.IsActive && existing.TriggerType == LuaScriptTriggerType.UiExtension)
            {
                _ = GetEngine().ExecuteScriptRecordAsync(existing);
            }

            return Task.FromResult<LuaScriptRecord?>(existing);
        }
        return Task.FromResult<LuaScriptRecord?>(null);
    }

    public Task<bool> DeleteScriptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_scripts.TryRemove(id, out _));
    }

    public async Task<LuaExecutionResult> RunScriptAsync(Guid id, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default)
    {
        if (!_scripts.TryGetValue(id, out var script))
        {
            return new LuaExecutionResult
            {
                Success = false,
                Error = $"Script with ID '{id}' was not found."
            };
        }

        return await GetEngine().ExecuteScriptRecordAsync(script, context, cancellationToken);
    }

    public async Task<LuaExecutionResult> TestExecuteAsync(string code, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default)
    {
        return await GetEngine().ExecuteAsync(code, context, cancellationToken);
    }

    public IReadOnlyCollection<LuaUiExtension> GetUiExtensions(LuaUiTargetZone? zone = null)
    {
        return zone.HasValue ? _uiApi.GetExtensionsForZone(zone.Value) : _uiApi.GetAllExtensions();
    }

    public async Task<LuaExecutionResult> InvokeUiActionAsync(string extensionId, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default)
    {
        return await GetEngine().InvokeUiActionAsync(extensionId, context, cancellationToken);
    }

    public async Task<IReadOnlyList<LuaExecutionResult>> EvaluateTelemetryChangeAsync(string machineId, string metric, double value, CancellationToken cancellationToken = default)
    {
        var activeTelemetryScripts = _scripts.Values
            .Where(s => s.IsActive && s.TriggerType == LuaScriptTriggerType.TelemetryChange)
            .ToList();

        var results = new List<LuaExecutionResult>();
        var context = new Dictionary<string, object?>
        {
            ["context_machine_id"] = machineId,
            ["context_metric"] = metric,
            ["context_value"] = value
        };

        var engine = GetEngine();
        foreach (var script in activeTelemetryScripts)
        {
            try
            {
                var res = await engine.ExecuteScriptRecordAsync(script, context, cancellationToken);
                results.Add(res);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating telemetry script '{Name}'", script.Name);
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<LuaExecutionResult>> EvaluateSlaPreventiveRulesAsync(CancellationToken cancellationToken = default)
    {
        var activeSlaScripts = _scripts.Values
            .Where(s => s.IsActive && s.TriggerType == LuaScriptTriggerType.SlaPreventiveCheck)
            .ToList();

        var results = new List<LuaExecutionResult>();
        var engine = GetEngine();
        foreach (var script in activeSlaScripts)
        {
            try
            {
                var res = await engine.ExecuteScriptRecordAsync(script, null, cancellationToken);
                results.Add(res);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluating SLA preventive script '{Name}'", script.Name);
            }
        }

        return results;
    }
}
