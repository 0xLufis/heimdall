namespace App.Agent.Daemon.Reporting;

using System.Collections.Generic;
using App.Shared.Protos;

/// <summary>
/// Contributor that constructs an InventoryComponent for an endpoint subsystem.
/// </summary>
public interface IComponentContributor
{
    /// <summary>
    /// Constructs an inventory component from gathered system info. Returns null if data is absent.
    /// </summary>
    InventoryComponent? CreateComponent(SystemInfoData data);
}

/// <summary>
/// Contributor that can yield multiple inventory components from custom sources, plugins, or extensions.
/// </summary>
public interface IMultiComponentContributor : IComponentContributor
{
    /// <summary>
    /// Constructs zero or more inventory components.
    /// </summary>
    IEnumerable<InventoryComponent> CreateComponents(SystemInfoData data);
}
