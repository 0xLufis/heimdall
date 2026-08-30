namespace App.Agent.Daemon.Runtime.Merger;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using App.Agent.Daemon.Domain.Recipes;
using Microsoft.Extensions.Logging;

public record MergedDataReading(
    string CanonicalKey,
    object? RawValue,
    DateTimeOffset Timestamp,
    Type ValueType
);

public record RecipeSubscription(
    Guid RecipeId,
    string PointId,
    int ConfiguredIntervalMs,
    PollingStrategyConfig Strategy,
    SourceConfigBase SourceConfig,
    EgressPolicyConfig EgressPolicy,
    Action<MergedDataReading> DispatchCallback
);

public class MergedProbeNode
{
    public string CanonicalKey { get; }
    public RecipeSourceType SourceType { get; }
    public SourceConfigBase MergedConfig { get; private set; }
    public int EffectiveIntervalMs { get; private set; }
    public InspectionDepth MaxInspectionDepth { get; private set; }
    public HashSet<string> MergedCimProperties { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<RecipeSubscription> _subscriptions = new();
    private readonly object _syncLock = new();

    public MergedProbeNode(string canonicalKey, RecipeSourceType sourceType, SourceConfigBase initialConfig)
    {
        CanonicalKey = canonicalKey;
        SourceType = sourceType;
        MergedConfig = initialConfig;
        EffectiveIntervalMs = int.MaxValue;
    }

    public void AddSubscription(RecipeSubscription subscription, SourceConfigBase config)
    {
        lock (_syncLock)
        {
            _subscriptions.Add(subscription);
            RecomputeMergedParameters(config, subscription.ConfiguredIntervalMs);
        }
    }

    public bool RemoveSubscription(Guid recipeId, string pointId)
    {
        lock (_syncLock)
        {
            _subscriptions.RemoveAll(s => s.RecipeId == recipeId && s.PointId == pointId);
            if (_subscriptions.Count == 0) return true; // Probe is now orphan

            RecalculateAll();
            return false;
        }
    }

    public IReadOnlyList<RecipeSubscription> GetSubscriptions()
    {
        lock (_syncLock)
        {
            return _subscriptions.ToList();
        }
    }

    private void RecomputeMergedParameters(SourceConfigBase incomingConfig, int incomingInterval)
    {
        // 1. Strictest Scheduling (Min interval)
        if (incomingInterval > 0 && incomingInterval < EffectiveIntervalMs)
        {
            EffectiveIntervalMs = incomingInterval;
        }

        // 2. Deepest Inspection Scope
        if (incomingConfig is ProcessSourceConfig procConfig)
        {
            if (procConfig.Depth > MaxInspectionDepth)
            {
                MaxInspectionDepth = procConfig.Depth;
                MergedConfig = procConfig with { Depth = MaxInspectionDepth };
            }
        }

        // 3. WQL Property Union
        if (incomingConfig is CimSourceConfig cimConfig)
        {
            foreach (var prop in cimConfig.ProjectedProperties)
            {
                MergedCimProperties.Add(prop);
            }
            MergedConfig = cimConfig with { ProjectedProperties = MergedCimProperties.ToList() };
        }
    }

    private void RecalculateAll()
    {
        EffectiveIntervalMs = _subscriptions.Count > 0 ? _subscriptions.Min(s => s.ConfiguredIntervalMs) : 1000;
        
        var maxDepth = _subscriptions
            .Select(s => s.SourceConfig)
            .OfType<ProcessSourceConfig>()
            .Select(p => p.Depth)
            .DefaultIfEmpty(InspectionDepth.Basic)
            .Max();
            
        MaxInspectionDepth = maxDepth;
    }
}

/// <summary>
/// Merges multiple overlapping recipes into an optimized unified execution DAG.
/// </summary>
public class MultiRecipeRuntimeMerger
{
    private readonly ILogger<MultiRecipeRuntimeMerger> _logger;
    private readonly ConcurrentDictionary<string, MergedProbeNode> _activeProbes = new();
    private readonly ConcurrentDictionary<Guid, RecipeDocument> _loadedRecipes = new();

    public MultiRecipeRuntimeMerger(ILogger<MultiRecipeRuntimeMerger> logger)
    {
        _logger = logger;
    }

    public void RegisterRecipe(RecipeDocument recipe, Action<MergedDataReading> dispatchTarget)
    {
        _logger.LogInformation("Compiling and merging Recipe: {Name} (ID: {Id})", recipe.Name, recipe.RecipeId);
        _loadedRecipes[recipe.RecipeId] = recipe;

        foreach (var dataPoint in recipe.DataPoints)
        {
            string canonicalKey = dataPoint.SourceConfig.GetCanonicalKey();

            var probeNode = _activeProbes.GetOrAdd(canonicalKey, key => 
                new MergedProbeNode(key, dataPoint.SourceType, dataPoint.SourceConfig));

            var subscription = new RecipeSubscription(
                recipe.RecipeId,
                dataPoint.PointId,
                dataPoint.PollingStrategy.IntervalMs,
                dataPoint.PollingStrategy,
                dataPoint.SourceConfig,
                dataPoint.EgressPolicy,
                dispatchTarget
            );

            probeNode.AddSubscription(subscription, dataPoint.SourceConfig);
            _logger.LogDebug("Bound point '{PointId}' on key '{Key}'. Effective interval: {Interval}ms",
                dataPoint.PointId, canonicalKey, probeNode.EffectiveIntervalMs);
        }
    }

    public void UnregisterRecipe(Guid recipeId)
    {
        if (!_loadedRecipes.TryRemove(recipeId, out var recipe)) return;

        _logger.LogInformation("Unregistering Recipe ID: {Id}", recipeId);
        foreach (var point in recipe.DataPoints)
        {
            string canonicalKey = point.SourceConfig.GetCanonicalKey();
            if (_activeProbes.TryGetValue(canonicalKey, out var probeNode))
            {
                bool isOrphan = probeNode.RemoveSubscription(recipeId, point.PointId);
                if (isOrphan)
                {
                    _activeProbes.TryRemove(canonicalKey, out _);
                    _logger.LogInformation("Disposed orphan probe for canonical key: {Key}", canonicalKey);
                }
            }
        }
    }

    public IReadOnlyCollection<MergedProbeNode> GetExecutionPlan() => _activeProbes.Values.ToList();
    
    public int ActiveProbeCount => _activeProbes.Count;
    public int LoadedRecipeCount => _loadedRecipes.Count;
}
