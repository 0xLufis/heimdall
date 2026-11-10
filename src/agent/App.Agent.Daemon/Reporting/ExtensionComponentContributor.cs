namespace App.Agent.Daemon.Reporting;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.Agent.Daemon.Extensions;
using App.Shared.Extensions;
using App.Shared.Protos;

/// <summary>
/// Contributes custom components registered via the agent extension API or installed plugins
/// directly into the agent's scheduled gRPC reporting payload.
/// </summary>
public class ExtensionComponentContributor : IMultiComponentContributor
{
    private readonly IExtensionRegistry _registry;

    public ExtensionComponentContributor(IExtensionRegistry registry)
    {
        _registry = registry;
    }

    public InventoryComponent? CreateComponent(SystemInfoData data)
    {
        return CreateComponents(data).FirstOrDefault();
    }

    public IEnumerable<InventoryComponent> CreateComponents(SystemInfoData data)
    {
        var activeSubmissions = _registry.GetActiveComponents();
        var components = new List<InventoryComponent>();

        foreach (var sub in activeSubmissions)
        {
            // Build unified component payload integrating metadata and tree parent hints
            object? parsedData = null;
            if (!string.IsNullOrEmpty(sub.DataJson))
            {
                try
                {
                    parsedData = JsonSerializer.Deserialize<JsonElement>(sub.DataJson);
                }
                catch
                {
                    parsedData = sub.DataJson;
                }
            }
            else
            {
                parsedData = sub.Data;
            }

            var envelope = new
            {
                Data = parsedData,
                sub.ParentComponentName,
                sub.ParentComponentId,
                sub.MachineId,
                sub.IsSigned,
                sub.IsSandboxed,
                sub.PluginId,
                sub.SubmittedAtUtc
            };

            string envelopeJson = JsonSerializer.Serialize(envelope);

            components.Add(new InventoryComponent
            {
                Name = sub.ComponentName,
                Technology = sub.Technology,
                Type = sub.ComponentType,
                DataJson = envelopeJson
            });
        }

        return components;
    }
}
