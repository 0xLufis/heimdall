namespace App.Backend.Api.Services.Plugins;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Shared.Plugins;

public interface IPluginService
{
    Task<PluginManifest> RegisterAndSignPluginAsync(PluginManifest manifest, Dictionary<string, string> files, bool sign = true);
    Task<PluginPackageBundle?> GetPluginBundleAsync(string pluginId);
    Task<List<PluginManifest>> ListPluginsAsync();
    Task<bool> PushPluginToAgentAsync(string pluginId, Guid clientPcId);
    Task<bool> PushPluginToMachineAsync(string pluginId, Guid machineId);
    Task<bool> DeletePluginAsync(string pluginId);
    string GetMasterPublicKeyPem();
    bool VerifyPluginSignature(PluginManifest manifest);
}
