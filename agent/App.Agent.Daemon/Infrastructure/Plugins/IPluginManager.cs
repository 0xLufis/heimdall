namespace App.Agent.Daemon.Infrastructure.Plugins;

using System.Collections.Generic;
using System.Threading.Tasks;
using App.Shared.Plugins;

/// <summary>
/// Service contract for managing, verifying, installing, and executing agent plugins.
/// </summary>
public interface IPluginManager
{
    /// <summary>
    /// Installs a plugin bundle after verifying its cryptographic signature or routing to development sandbox.
    /// </summary>
    Task<PluginInstallResult> InstallPluginAsync(PluginPackageBundle bundle);

    /// <summary>
    /// Executes an installed plugin.
    /// </summary>
    Task<PluginExecutionResult> ExecutePluginAsync(string pluginId, string[]? arguments = null);

    /// <summary>
    /// Uninstalls a plugin and purges any installed files or sandbox data.
    /// </summary>
    Task<bool> UninstallPluginAsync(string pluginId);

    /// <summary>
    /// Returns the manifests of all currently installed plugins.
    /// </summary>
    IReadOnlyList<PluginManifest> GetInstalledPlugins();

    /// <summary>
    /// Verifies the cryptographic RSA signature of a plugin manifest against the trusted master public key.
    /// </summary>
    bool VerifySignature(PluginManifest manifest);
}
