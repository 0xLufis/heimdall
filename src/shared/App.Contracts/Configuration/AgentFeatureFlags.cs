namespace App.Contracts.Configuration;

using System;

/// <summary>
/// Strongly-typed feature flags governing edge agent behavior, development tools,
/// mock simulation servers (Beckhoff TwinCAT ADS, Minimal OPC UA), and diagnostic telemetry routines.
/// </summary>
public sealed class AgentFeatureFlags
{
    public const string EnableDevEnvVar = "HEIMDALL_ENABLE_DEV";
    public const string EnableDebugEnvVar = "HEIMDALL_ENABLE_DEBUG";
    public const string LegacyDebugEnvVar = "HEIMDALL_DEBUG";
    public const string AspNetCoreEnvVar = "ASPNETCORE_ENVIRONMENT";
    public const string DotNetEnvVar = "DOTNET_ENVIRONMENT";

    /// <summary>
    /// Governs mock industrial simulation servers (AdsSimulationServer, MinimalOpcServer).
    /// Defaults to false in production deployments for strict security;
    /// enabled if HEIMDALL_ENABLE_DEV is truthy or environment is "Development".
    /// </summary>
    public bool EnableDevFeatures { get; set; }

    /// <summary>
    /// Governs verbose diagnostic dumps, memory symbol inspections, and debug endpoints.
    /// Defaults to false in production deployments; enabled if HEIMDALL_ENABLE_DEBUG or HEIMDALL_DEBUG is truthy.
    /// </summary>
    public bool EnableDebugFeatures { get; set; }

    /// <summary>
    /// Factory creating feature flags from environment variables.
    /// </summary>
    public static AgentFeatureFlags FromEnvironment(string? environmentName = null)
    {
        // 1. EnableDevFeatures
        bool devFeatures = false;
        var devEnv = Environment.GetEnvironmentVariable(EnableDevEnvVar);
        if (!string.IsNullOrWhiteSpace(devEnv))
        {
            devFeatures = IsTruthy(devEnv);
        }
        else
        {
            var env = environmentName
                      ?? Environment.GetEnvironmentVariable(AspNetCoreEnvVar)
                      ?? Environment.GetEnvironmentVariable(DotNetEnvVar);
            devFeatures = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase);
        }

        // 2. EnableDebugFeatures
        bool debugFeatures = false;
        var debugEnv = Environment.GetEnvironmentVariable(EnableDebugEnvVar)
                       ?? Environment.GetEnvironmentVariable(LegacyDebugEnvVar);
        if (!string.IsNullOrWhiteSpace(debugEnv))
        {
            debugFeatures = IsTruthy(debugEnv);
        }

        return new AgentFeatureFlags
        {
            EnableDevFeatures = devFeatures,
            EnableDebugFeatures = debugFeatures
        };
    }

    /// <summary>
    /// Factory creating feature flags by evaluating custom value provider delegates (e.g., config[key]) and environment variables.
    /// </summary>
    public static AgentFeatureFlags FromProvider(Func<string, string?>? valueProvider, string? environmentName = null)
    {
        var flags = FromEnvironment(environmentName);

        if (valueProvider == null)
            return flags;

        var devVal = valueProvider("AgentFeatureFlags:EnableDevFeatures")
                     ?? valueProvider("EnableDevFeatures")
                     ?? valueProvider(EnableDevEnvVar);

        if (!string.IsNullOrWhiteSpace(devVal) && bool.TryParse(devVal, out var devParsed))
        {
            flags.EnableDevFeatures = devParsed;
        }

        var debugVal = valueProvider("AgentFeatureFlags:EnableDebugFeatures")
                       ?? valueProvider("EnableDebugFeatures")
                       ?? valueProvider(EnableDebugEnvVar)
                       ?? valueProvider(LegacyDebugEnvVar);

        if (!string.IsNullOrWhiteSpace(debugVal) && bool.TryParse(debugVal, out var debugParsed))
        {
            flags.EnableDebugFeatures = debugParsed;
        }

        return flags;
    }

    public static bool IsTruthy(string value)
    {
        if (bool.TryParse(value, out var b))
            return b;

        var trimmed = value.Trim();
        return trimmed == "1" ||
               string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "on", StringComparison.OrdinalIgnoreCase);
    }
}
