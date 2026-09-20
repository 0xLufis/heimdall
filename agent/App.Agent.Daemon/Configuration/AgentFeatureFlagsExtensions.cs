namespace App.Agent.Daemon.Configuration;

using App.Contracts.Configuration;
using Microsoft.Extensions.Configuration;

public static class AgentFeatureFlagsExtensions
{
    public static AgentFeatureFlags ToAgentFeatureFlags(this IConfiguration configuration, string? environmentName = null)
    {
        return AgentFeatureFlags.FromProvider(key => configuration[key], environmentName);
    }
}
