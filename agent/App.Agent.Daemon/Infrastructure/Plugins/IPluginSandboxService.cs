namespace App.Agent.Daemon.Infrastructure.Plugins;

using System.Threading.Tasks;
using App.Shared.Plugins;

/// <summary>
/// Service contract for executing plugins within an isolated sandbox boundary.
/// Enforces development-only execution for unsigned plugins while strictly disallowing
/// unsigned code in production.
/// </summary>
public interface IPluginSandboxService
{
    /// <summary>
    /// Executes a plugin inside an isolated development sandbox.
    /// Throws or fails if current environment is Production and the plugin is unsigned.
    /// </summary>
    Task<PluginExecutionResult> ExecuteSandboxedAsync(PluginPackageBundle bundle, string[]? arguments = null);

    /// <summary>
    /// Cleans up and purges sandbox files for a given plugin identifier.
    /// </summary>
    void PurgeSandbox(string pluginId);

    /// <summary>
    /// Returns the absolute sandbox directory path for a plugin.
    /// </summary>
    string GetSandboxDirectory(string pluginId);
}
