using System;
using System.Collections.Generic;
using App.Agent.Daemon.Domain.Recipes;
using App.Agent.Daemon.Runtime.Merger;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class AgentRecipeMergerTests
{
    private RecipeDocument CreateTestRecipe(Guid id, string name, string symbolName, int intervalMs, InspectionDepth depth = InspectionDepth.Basic)
    {
        return new RecipeDocument(
            RecipeId: id,
            Version: "1.0.0",
            Name: name,
            Description: "Test Recipe",
            TargetSelector: new TargetSelector("Any", new List<string> { "PLC" }, new Dictionary<string, string>()),
            Security: new RecipeSecurity("key-1", "RSA-SHA256", "sig", "hash"),
            DataPoints: new List<DataPointDefinition>
            {
                new DataPointDefinition(
                    PointId: "pt-1",
                    Name: "Spindle Velocity",
                    Category: DataCategory.Metric,
                    SourceType: RecipeSourceType.BeckhoffAds,
                    SourceConfig: new AdsSourceConfig("192.168.1.100.1.1", 851, symbolName),
                    PollingStrategy: new PollingStrategyConfig(PollingStrategyType.Periodic, intervalMs),
                    EgressPolicy: new EgressPolicyConfig(EgressPriority.P2_MediumMetrics, true)
                ),
                new DataPointDefinition(
                    PointId: "pt-2",
                    Name: "Process Check",
                    Category: DataCategory.List,
                    SourceType: RecipeSourceType.SystemProcess,
                    SourceConfig: new ProcessSourceConfig("TcXaeShell*", depth),
                    PollingStrategy: new PollingStrategyConfig(PollingStrategyType.Periodic, intervalMs * 2),
                    EgressPolicy: new EgressPolicyConfig(EgressPriority.P3_LowInventory, false)
                )
            }
        );
    }

    [Fact]
    public void Merger_DeduplicatesOverlappingProbes_AndSelectsStrictestInterval()
    {
        var merger = new MultiRecipeRuntimeMerger(NullLogger<MultiRecipeRuntimeMerger>.Instance);

        var recipeA = CreateTestRecipe(Guid.NewGuid(), "IT Health", "MAIN.fSpeed", 5000, InspectionDepth.Basic);
        var recipeB = CreateTestRecipe(Guid.NewGuid(), "Line 1 Diagnostics", "MAIN.fSpeed", 500, InspectionDepth.ModulesAndThreads);

        var readingsReceived = new List<MergedDataReading>();
        void DispatchCallback(MergedDataReading r) => readingsReceived.Add(r);

        // Register both recipes
        merger.RegisterRecipe(recipeA, DispatchCallback);
        merger.RegisterRecipe(recipeB, DispatchCallback);

        var plan = merger.GetExecutionPlan();

        // There should only be 2 active merged probes: 1 for Ads (MAIN.fSpeed) and 1 for Process (TcXaeShell*)
        Assert.Equal(2, merger.ActiveProbeCount);

        // Find the ADS probe
        var adsProbe = Assert.Single(plan, p => p.SourceType == RecipeSourceType.BeckhoffAds);
        
        // Strictest interval should be 500ms (min of 5000ms and 500ms)
        Assert.Equal(500, adsProbe.EffectiveIntervalMs);
        Assert.Equal(2, adsProbe.GetSubscriptions().Count);

        // Find the Process probe
        var procProbe = Assert.Single(plan, p => p.SourceType == RecipeSourceType.SystemProcess);
        
        // Strictest interval should be 1000ms (min of 10000ms and 1000ms)
        Assert.Equal(1000, procProbe.EffectiveIntervalMs);
        // Deepest inspection depth should be ModulesAndThreads
        Assert.Equal(InspectionDepth.ModulesAndThreads, procProbe.MaxInspectionDepth);
    }

    [Fact]
    public void Merger_UnregistersRecipe_AndRemovesOrphanProbes()
    {
        var merger = new MultiRecipeRuntimeMerger(NullLogger<MultiRecipeRuntimeMerger>.Instance);
        var recipeIdA = Guid.NewGuid();
        var recipeIdB = Guid.NewGuid();

        var recipeA = CreateTestRecipe(recipeIdA, "Recipe A", "MAIN.fSpeed", 2000);
        var recipeB = CreateTestRecipe(recipeIdB, "Recipe B", "MAIN.fSpeed", 500);

        merger.RegisterRecipe(recipeA, _ => { });
        merger.RegisterRecipe(recipeB, _ => { });

        Assert.Equal(2, merger.ActiveProbeCount);

        // Unregister Recipe B
        merger.UnregisterRecipe(recipeIdB);

        var plan = merger.GetExecutionPlan();
        var adsProbe = Assert.Single(plan, p => p.SourceType == RecipeSourceType.BeckhoffAds);
        
        // Effective interval should now revert back to Recipe A's 2000ms
        Assert.Equal(2000, adsProbe.EffectiveIntervalMs);
        Assert.Single(adsProbe.GetSubscriptions());

        // Unregister Recipe A
        merger.UnregisterRecipe(recipeIdA);

        // All probes should now be completely cleaned up
        Assert.Equal(0, merger.ActiveProbeCount);
    }
}
