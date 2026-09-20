namespace App.Contracts.Mqtt;

/// <summary>
/// Canonical MQTT topics for industrial telemetry, PLC memory streaming, eventing, and command dispatch.
/// Uses a structured hierarchical namespace: heimdall/devices/{machineIdentifier}/{channel}.
/// </summary>
public static class MqttTopics
{
    public const string RootPrefix = "heimdall/devices";

    // Channels
    public const string SystemInfoChannel = "system_info";
    public const string PlcMemoryChannel = "plc_memory";
    public const string TelemetryChannel = "telemetry";
    public const string EventsChannel = "events";
    public const string CommandsChannel = "commands";
    public const string RecipeRequestChannel = "recipes/request";
    public const string RecipeResponseChannel = "recipes/response";

    // Topic formatters for specific machine identifiers
    public static string SystemInfo(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{SystemInfoChannel}";
    public static string PlcMemory(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{PlcMemoryChannel}";
    public static string Telemetry(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{TelemetryChannel}";
    public static string Events(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{EventsChannel}";
    public static string Commands(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{CommandsChannel}";
    public static string RecipeRequest(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{RecipeRequestChannel}";
    public static string RecipeResponse(string machineIdentifier) => $"{RootPrefix}/{Sanitize(machineIdentifier)}/{RecipeResponseChannel}";

    // Wildcard subscription patterns for backend ingestion
    public const string AllDevicesWildcard = "heimdall/devices/#";
    public const string SystemInfoWildcard = "heimdall/devices/+/system_info";
    public const string PlcMemoryWildcard = "heimdall/devices/+/plc_memory";
    public const string TelemetryWildcard = "heimdall/devices/+/telemetry";
    public const string EventsWildcard = "heimdall/devices/+/events";
    public const string CommandsWildcard = "heimdall/devices/+/commands";
    public const string RecipeRequestWildcard = "heimdall/devices/+/recipes/request";

    /// <summary>
    /// Extracts the machine identifier from a device topic.
    /// Example: "heimdall/devices/HW-IPC-01/system_info" -> "HW-IPC-01"
    /// </summary>
    public static string? ExtractMachineIdentifier(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return null;

        var parts = topic.Split('/');
        if (parts.Length >= 3 && parts[0] == "heimdall" && parts[1] == "devices")
        {
            return parts[2];
        }

        return null;
    }

    /// <summary>
    /// Extracts the subchannel from a device topic.
    /// Example: "heimdall/devices/HW-IPC-01/system_info" -> "system_info"
    /// </summary>
    public static string? ExtractChannel(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic)) return null;

        var parts = topic.Split('/');
        if (parts.Length >= 4 && parts[0] == "heimdall" && parts[1] == "devices")
        {
            return string.Join('/', parts.Skip(3));
        }

        return null;
    }

    private static string Sanitize(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return "unknown";
        return identifier.Trim().Replace('/', '_').Replace('+', '_').Replace('#', '_');
    }
}
