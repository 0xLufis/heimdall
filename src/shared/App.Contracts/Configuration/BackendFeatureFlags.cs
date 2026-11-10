namespace App.Contracts.Configuration;

using System;

/// <summary>
/// Strongly-typed feature flags governing backend API behavior, development tools,
/// OpenAPI/Swagger documentation, permissive CORS policies, simulated Active Directory preview imports,
/// and verbose diagnostic telemetry dumps.
/// </summary>
public sealed class BackendFeatureFlags
{
    public const string EnableDevEnvVar = "HEIMDALL_ENABLE_DEV";
    public const string EnableDebugEnvVar = "HEIMDALL_ENABLE_DEBUG";
    public const string LegacyDebugEnvVar = "HEIMDALL_DEBUG";
    public const string AspNetCoreEnvVar = "ASPNETCORE_ENVIRONMENT";
    public const string DotNetEnvVar = "DOTNET_ENVIRONMENT";

    public const string ModuleTicketingEnvVar = "HEIMDALL_MODULE_TICKETING";
    public const string ModuleInventoryEnvVar = "HEIMDALL_MODULE_INVENTORY";
    public const string ModuleTelemetryEnvVar = "HEIMDALL_MODULE_TELEMETRY";
    public const string ModulePresetEnvVar = "HEIMDALL_MODULE_PRESET";
    public const string ModulesListEnvVar = "HEIMDALL_MODULES";
    public const string DisabledModulesListEnvVar = "HEIMDALL_DISABLED_MODULES";

    /// <summary>
    /// Governs development features, developer exception pages, Swagger/OpenAPI endpoints,
    /// permissive dev CORS, mock AD simulation endpoints, and dev overrides/seeding.
    /// Defaults to false in production deployments for strict security;
    /// enabled if HEIMDALL_ENABLE_DEV is truthy or environment is "Development".
    /// </summary>
    public bool EnableDevFeatures { get; set; }

    /// <summary>
    /// Governs verbose diagnostic dumps, raw telemetry debug exports, and internal state inspection.
    /// Defaults to false in production deployments; enabled if HEIMDALL_ENABLE_DEBUG or HEIMDALL_DEBUG is truthy.
    /// </summary>
    public bool EnableDebugFeatures { get; set; }

    /// <summary>
    /// Governs maintenance work orders, technician dispatch, and ticketing endpoints.
    /// Enabled by default; can be disabled via HEIMDALL_MODULE_TICKETING, HEIMDALL_ENABLE_TICKETING, or ENABLE_TICKETING.
    /// </summary>
    public bool EnableTicketing { get; set; } = true;

    /// <summary>
    /// Governs inventory, spare parts catalog, and storage bin allocation endpoints.
    /// Enabled by default; can be disabled via HEIMDALL_MODULE_INVENTORY, HEIMDALL_ENABLE_INVENTORY, or ENABLE_INVENTORY.
    /// </summary>
    public bool EnableInventory { get; set; } = true;

    /// <summary>
    /// Governs industrial telemetry trends, edge signal processing, and KPI forecasting endpoints.
    /// Enabled by default; can be disabled via HEIMDALL_MODULE_TELEMETRY, HEIMDALL_ENABLE_TELEMETRY, or ENABLE_TELEMETRY.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// Factory creating feature flags from environment variables.
    /// </summary>
    public static BackendFeatureFlags FromEnvironment(string? environmentName = null)
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

        // 3. Module Flags (Presets, Lists, Individual env vars)
        bool ticketing = true;
        bool inventory = true;
        bool telemetry = true;

        var preset = Environment.GetEnvironmentVariable(ModulePresetEnvVar)
                     ?? Environment.GetEnvironmentVariable("HEIMDALL_PRESET");
        if (!string.IsNullOrWhiteSpace(preset))
        {
            var p = preset.Trim().ToLowerInvariant();
            if (p == "telemetry-ticketing")
            {
                inventory = false;
            }
            else if (p == "ticketing-only" || p == "dispatch")
            {
                inventory = false;
                telemetry = false;
            }
            else if (p == "telemetry-only" || p == "edge")
            {
                ticketing = false;
                inventory = false;
            }
            else if (p == "core-only" || p == "lean" || p == "minimal")
            {
                ticketing = false;
                inventory = false;
                telemetry = false;
            }
            else if (p == "all" || p == "full" || p == "monolith")
            {
                ticketing = true;
                inventory = true;
                telemetry = true;
            }
        }

        var modulesList = Environment.GetEnvironmentVariable(ModulesListEnvVar)
                          ?? Environment.GetEnvironmentVariable("HEIMDALL_ENABLED_MODULES");
        if (!string.IsNullOrWhiteSpace(modulesList))
        {
            var parts = modulesList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var set = new System.Collections.Generic.HashSet<string>(parts, StringComparer.OrdinalIgnoreCase);
            ticketing = set.Contains("ticketing") || set.Contains("tickets") || set.Contains("maintenance-ticketing");
            inventory = set.Contains("inventory") || set.Contains("parts") || set.Contains("inventory-assets");
            telemetry = set.Contains("telemetry") || set.Contains("industrial-telemetry");
        }

        var disabledList = Environment.GetEnvironmentVariable(DisabledModulesListEnvVar)
                           ?? Environment.GetEnvironmentVariable("HEIMDALL_DISABLE_MODULES");
        if (!string.IsNullOrWhiteSpace(disabledList))
        {
            var parts = disabledList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var set = new System.Collections.Generic.HashSet<string>(parts, StringComparer.OrdinalIgnoreCase);
            if (set.Contains("ticketing") || set.Contains("tickets") || set.Contains("maintenance-ticketing"))
                ticketing = false;
            if (set.Contains("inventory") || set.Contains("parts") || set.Contains("inventory-assets"))
                inventory = false;
            if (set.Contains("telemetry") || set.Contains("industrial-telemetry"))
                telemetry = false;
        }

        var ticketingEnv = Environment.GetEnvironmentVariable(ModuleTicketingEnvVar)
                           ?? Environment.GetEnvironmentVariable("HEIMDALL_ENABLE_TICKETING")
                           ?? Environment.GetEnvironmentVariable("ENABLE_TICKETING");
        if (!string.IsNullOrWhiteSpace(ticketingEnv))
        {
            if (IsFalsy(ticketingEnv)) ticketing = false;
            else if (IsTruthy(ticketingEnv)) ticketing = true;
        }

        var inventoryEnv = Environment.GetEnvironmentVariable(ModuleInventoryEnvVar)
                           ?? Environment.GetEnvironmentVariable("HEIMDALL_ENABLE_INVENTORY")
                           ?? Environment.GetEnvironmentVariable("ENABLE_INVENTORY");
        if (!string.IsNullOrWhiteSpace(inventoryEnv))
        {
            if (IsFalsy(inventoryEnv)) inventory = false;
            else if (IsTruthy(inventoryEnv)) inventory = true;
        }

        var telemetryEnv = Environment.GetEnvironmentVariable(ModuleTelemetryEnvVar)
                           ?? Environment.GetEnvironmentVariable("HEIMDALL_ENABLE_TELEMETRY")
                           ?? Environment.GetEnvironmentVariable("ENABLE_TELEMETRY");
        if (!string.IsNullOrWhiteSpace(telemetryEnv))
        {
            if (IsFalsy(telemetryEnv)) telemetry = false;
            else if (IsTruthy(telemetryEnv)) telemetry = true;
        }

        return new BackendFeatureFlags
        {
            EnableDevFeatures = devFeatures,
            EnableDebugFeatures = debugFeatures,
            EnableTicketing = ticketing,
            EnableInventory = inventory,
            EnableTelemetry = telemetry
        };
    }

    /// <summary>
    /// Factory creating feature flags by evaluating custom value provider delegates (e.g., config[key]) and environment variables.
    /// </summary>
    public static BackendFeatureFlags FromProvider(Func<string, string?>? valueProvider, string? environmentName = null)
    {
        var flags = FromEnvironment(environmentName);

        if (valueProvider == null)
            return flags;

        var devVal = valueProvider("BackendFeatureFlags:EnableDevFeatures")
                     ?? valueProvider("EnableDevFeatures")
                     ?? valueProvider(EnableDevEnvVar);

        if (!string.IsNullOrWhiteSpace(devVal) && bool.TryParse(devVal, out var devParsed))
        {
            flags.EnableDevFeatures = devParsed;
        }

        var debugVal = valueProvider("BackendFeatureFlags:EnableDebugFeatures")
                       ?? valueProvider("EnableDebugFeatures")
                       ?? valueProvider(EnableDebugEnvVar)
                       ?? valueProvider(LegacyDebugEnvVar);

        if (!string.IsNullOrWhiteSpace(debugVal) && bool.TryParse(debugVal, out var debugParsed))
        {
            flags.EnableDebugFeatures = debugParsed;
        }

        var tickVal = valueProvider("BackendFeatureFlags:EnableTicketing")
                      ?? valueProvider("EnableTicketing")
                      ?? valueProvider(ModuleTicketingEnvVar)
                      ?? valueProvider("HEIMDALL_ENABLE_TICKETING");
        if (!string.IsNullOrWhiteSpace(tickVal))
        {
            if (IsFalsy(tickVal)) flags.EnableTicketing = false;
            else if (IsTruthy(tickVal)) flags.EnableTicketing = true;
        }

        var invVal = valueProvider("BackendFeatureFlags:EnableInventory")
                     ?? valueProvider("EnableInventory")
                     ?? valueProvider(ModuleInventoryEnvVar)
                     ?? valueProvider("HEIMDALL_ENABLE_INVENTORY");
        if (!string.IsNullOrWhiteSpace(invVal))
        {
            if (IsFalsy(invVal)) flags.EnableInventory = false;
            else if (IsTruthy(invVal)) flags.EnableInventory = true;
        }

        var telVal = valueProvider("BackendFeatureFlags:EnableTelemetry")
                     ?? valueProvider("EnableTelemetry")
                     ?? valueProvider(ModuleTelemetryEnvVar)
                     ?? valueProvider("HEIMDALL_ENABLE_TELEMETRY");
        if (!string.IsNullOrWhiteSpace(telVal))
        {
            if (IsFalsy(telVal)) flags.EnableTelemetry = false;
            else if (IsTruthy(telVal)) flags.EnableTelemetry = true;
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
               string.Equals(trimmed, "on", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "enabled", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFalsy(string value)
    {
        if (bool.TryParse(value, out var b))
            return !b;

        var trimmed = value.Trim();
        return trimmed == "0" ||
               string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "off", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "disabled", StringComparison.OrdinalIgnoreCase);
    }
}
