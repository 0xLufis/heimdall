namespace App.Backend.Api.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Lua.Bridge;
using App.Shared.Lua.Core;
using App.Shared.Lua.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Singleton coordinator for plant Lua automation. Scripts are persisted in <see cref="AppDbContext.LuaScripts"/>;
/// every operation runs in its own DI scope so the scoped <see cref="AppDbContext"/> and
/// <see cref="ILuaPlantDataProvider"/> are created and disposed per call.
///
/// The only in-process state is the UI extension registry (extension metadata → owning script id),
/// which is rebuilt lazily from the database. Handler closures are never cached; invoking an action
/// re-executes the owning script in a fresh sandbox.
/// </summary>
public class LuaAutomationService : ILuaAutomationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LuaAutomationService> _logger;
    private readonly LuaSandboxOptions _options;
    private readonly ConcurrentDictionary<string, (LuaUiExtension Extension, Guid ScriptId)> _uiRegistry = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _uiInitLock = new(1, 1);
    private volatile bool _uiInitialized;

    public LuaAutomationService(
        IServiceScopeFactory scopeFactory,
        ILogger<LuaAutomationService> logger,
        LuaSandboxOptions? options = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options ?? new LuaSandboxOptions();
    }

    private LuaScriptEngine CreateEngine(IServiceProvider services) =>
        new(services.GetRequiredService<ILuaPlantDataProvider>(), _options, services.GetService<ILogger<LuaScriptEngine>>());

    public async Task<IReadOnlyList<LuaScriptRecord>> ListScriptsAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.LuaScripts.AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<LuaScriptRecord?> GetScriptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.LuaScripts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<LuaScriptRecord> CreateScriptAsync(LuaScriptRecord script, string? createdBy = null, CancellationToken cancellationToken = default)
    {
        // Only user-editable fields are taken from the payload; identity, audit and execution fields are server-owned.
        var now = DateTimeOffset.UtcNow;
        var entity = new LuaScriptRecord
        {
            Id = Guid.NewGuid(),
            Name = script.Name,
            Description = script.Description ?? string.Empty,
            TriggerType = script.TriggerType,
            Code = script.Code,
            IsActive = script.IsActive,
            Metadata = script.Metadata,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.LuaScripts.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }

        await RefreshUiRegistrationsAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<LuaScriptRecord?> UpdateScriptAsync(Guid id, LuaScriptRecord script, CancellationToken cancellationToken = default)
    {
        LuaScriptRecord? existing;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            existing = await db.LuaScripts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (existing == null) return null;

            existing.Name = string.IsNullOrWhiteSpace(script.Name) ? existing.Name : script.Name;
            existing.Description = script.Description ?? string.Empty;
            existing.TriggerType = script.TriggerType;
            existing.Code = script.Code;
            existing.IsActive = script.IsActive;
            existing.Metadata = script.Metadata;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        await RefreshUiRegistrationsAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteScriptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var existing = await db.LuaScripts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (existing == null) return false;

        db.LuaScripts.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        RemoveUiRegistrations(id);
        return true;
    }

    public async Task<LuaExecutionResult?> RunScriptAsync(Guid id, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var script = await db.LuaScripts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (script == null) return null;

        return await ExecuteAndRecordAsync(scope.ServiceProvider, db, script, context, cancellationToken);
    }

    public async Task<LuaExecutionResult> TestExecuteAsync(string code, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        return await CreateEngine(scope.ServiceProvider).ExecuteAsync(code, context, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LuaUiExtension>> GetUiExtensionsAsync(LuaUiTargetZone? zone = null, CancellationToken cancellationToken = default)
    {
        await EnsureUiRegistryInitializedAsync(cancellationToken);
        return _uiRegistry.Values
            .Select(v => v.Extension)
            .Where(e => !zone.HasValue || e.TargetZone == zone.Value)
            .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<LuaExecutionResult?> InvokeUiActionAsync(string extensionId, Dictionary<string, object?>? context = null, CancellationToken cancellationToken = default)
    {
        await EnsureUiRegistryInitializedAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(extensionId) || !_uiRegistry.TryGetValue(extensionId, out var entry) ||
            entry.Extension.ExtensionType != LuaUiExtensionType.ActionButton)
        {
            return null;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var script = await db.LuaScripts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == entry.ScriptId, cancellationToken);
        if (script == null || !script.IsActive || script.TriggerType != LuaScriptTriggerType.UiExtension)
        {
            RemoveUiRegistrations(entry.ScriptId);
            return null;
        }

        return await CreateEngine(scope.ServiceProvider).InvokeUiActionAsync(script, extensionId, context, cancellationToken);
    }

    public async Task<IReadOnlyList<LuaExecutionResult>> EvaluateTelemetryChangeAsync(string machineId, string metric, double value, CancellationToken cancellationToken = default)
    {
        var context = new Dictionary<string, object?>
        {
            ["context_machine_id"] = machineId,
            ["context_metric"] = metric,
            ["context_value"] = value
        };

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            // Record the incoming point so telemetry.get_latest/get_history observe it.
            await scope.ServiceProvider.GetRequiredService<ILuaPlantDataProvider>()
                .EmitTelemetryAsync(machineId, metric, value, null, cancellationToken);
        }

        return await RunActiveScriptsAsync(LuaScriptTriggerType.TelemetryChange, context, cancellationToken);
    }

    public Task<IReadOnlyList<LuaExecutionResult>> EvaluateSlaPreventiveRulesAsync(CancellationToken cancellationToken = default)
        => RunActiveScriptsAsync(LuaScriptTriggerType.SlaPreventiveCheck, null, cancellationToken);

    private async Task<IReadOnlyList<LuaExecutionResult>> RunActiveScriptsAsync(
        LuaScriptTriggerType trigger,
        Dictionary<string, object?>? context,
        CancellationToken cancellationToken)
    {
        List<Guid> ids;
        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ids = await db.LuaScripts.AsNoTracking()
                .Where(s => s.IsActive && s.TriggerType == trigger)
                .OrderBy(s => s.CreatedAt)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
        }

        var results = new List<LuaExecutionResult>();
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // One scope per script: an abandoned (timed-out) run can never share a DbContext with the next one.
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var script = await db.LuaScripts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
            if (script == null) continue;
            results.Add(await ExecuteAndRecordAsync(scope.ServiceProvider, db, script, context, cancellationToken));
        }
        return results;
    }

    private async Task<LuaExecutionResult> ExecuteAndRecordAsync(
        IServiceProvider services,
        AppDbContext db,
        LuaScriptRecord script,
        Dictionary<string, object?>? context,
        CancellationToken cancellationToken)
    {
        var result = await CreateEngine(services).ExecuteScriptRecordAsync(script, context, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning("Lua script '{Name}' ({Id}) failed: {Error}", script.Name, script.Id, result.Error);
        }

        try
        {
            // Persist LastExecutedAt / LastExecutionSuccess / LastExecutionError set by the engine.
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist execution status for Lua script {Id}", script.Id);
        }

        return result;
    }

    private async Task EnsureUiRegistryInitializedAsync(CancellationToken cancellationToken)
    {
        if (_uiInitialized) return;

        await _uiInitLock.WaitAsync(cancellationToken);
        try
        {
            if (_uiInitialized) return;

            List<LuaScriptRecord> uiScripts;
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                uiScripts = await db.LuaScripts.AsNoTracking()
                    .Where(s => s.IsActive && s.TriggerType == LuaScriptTriggerType.UiExtension)
                    .ToListAsync(cancellationToken);
            }

            foreach (var script in uiScripts)
            {
                await RegisterUiExtensionsAsync(script, cancellationToken);
            }
            _uiInitialized = true;
        }
        finally
        {
            _uiInitLock.Release();
        }
    }

    private async Task RefreshUiRegistrationsAsync(LuaScriptRecord script, CancellationToken cancellationToken)
    {
        RemoveUiRegistrations(script.Id);
        if (_uiInitialized && script.IsActive && script.TriggerType == LuaScriptTriggerType.UiExtension)
        {
            await RegisterUiExtensionsAsync(script, cancellationToken);
        }
    }

    private async Task RegisterUiExtensionsAsync(LuaScriptRecord script, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var result = await CreateEngine(scope.ServiceProvider).ExecuteAsync(script.Code, null, cancellationToken);
        if (!result.Success)
        {
            _logger.LogWarning("UI extension script '{Name}' ({Id}) failed to register: {Error}", script.Name, script.Id, result.Error);
            return;
        }

        foreach (var ext in result.UiExtensions)
        {
            _uiRegistry[ext.Id] = (ext, script.Id);
        }
    }

    private void RemoveUiRegistrations(Guid scriptId)
    {
        foreach (var kv in _uiRegistry.Where(kv => kv.Value.ScriptId == scriptId).ToList())
        {
            _uiRegistry.TryRemove(kv);
        }
    }
}
