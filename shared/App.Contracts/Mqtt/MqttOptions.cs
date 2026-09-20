namespace App.Contracts.Mqtt;

/// <summary>
/// Configuration options for MQTT broker connectivity, embedded broker hosting, and authentication.
/// </summary>
public class MqttOptions
{
    public const string SectionName = "Mqtt";

    /// <summary>
    /// Hostname or IP of the MQTT broker. Defaults to 127.0.0.1.
    /// Can be overridden via MQTT_BROKER_HOST environment variable.
    /// </summary>
    public string BrokerHost { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port of the MQTT broker. Defaults to standard 1883.
    /// Can be overridden via MQTT_BROKER_PORT environment variable.
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// Optional Client ID for the MQTT connection. If null, a deterministic or random GUID will be generated.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Optional username for MQTT connection authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional password for MQTT connection authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Shared agent authentication key (validated against HEIMDALL_AGENT_KEY or MQTT 5 user properties).
    /// </summary>
    public string? AgentKey { get; set; }

    /// <summary>
    /// Whether TLS/SSL encryption is enabled for the MQTT connection. Defaults to false.
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Path to a custom CA certificate file for TLS verification.
    /// </summary>
    public string? CaCertificatePath { get; set; }

    /// <summary>
    /// Path to client certificate (.pfx / .crt) for mTLS authentication.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Client certificate password if encrypted.
    /// </summary>
    public string? ClientCertificatePassword { get; set; }

    /// <summary>
    /// For the Backend: Whether to host an embedded MQTT broker directly inside the ASP.NET Core process.
    /// Defaults to true so Heimdall operates out-of-the-box in standalone and test environments.
    /// Can be disabled when using an external broker (e.g., Mosquitto, EMQX, HiveMQ) via MQTT_EMBEDDED_BROKER=false.
    /// </summary>
    public bool EmbeddedBrokerEnabled { get; set; } = true;

    /// <summary>
    /// Port for the embedded MQTT broker endpoint if enabled. Defaults to 1883.
    /// </summary>
    public int EmbeddedBrokerPort { get; set; } = 1883;

    /// <summary>
    /// Keep-alive period in seconds. Defaults to 30.
    /// </summary>
    public int KeepAliveSeconds { get; set; } = 30;

    /// <summary>
    /// Connection timeout in seconds. Defaults to 10.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 10;
}
