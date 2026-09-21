using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

using App.Agent.Daemon.Interfaces;

namespace App.Agent.Daemon;


/// <summary>
/// Strong configuration model for the edge daemon agent.
/// Encapsulates master security governance, telemetry egress limits,
/// hardware polling cadence, and plugin sandbox policies.
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// Semantic version of the configuration schema manifest.
    /// </summary>
    public string ConfigSchemaVersion { get; set; } = "1.0.0";

    // Connectivity
    /// <summary>
    /// Upstream Heimdall Backend base URL (gRPC/HTTP).
    /// </summary>
    public string BackendUrl { get; set; } = "http://localhost:5001";

    /// <summary>
    /// Authentication scheme for communicating with backend ('NoAuth', 'HeimdallCert', 'UserCert').
    /// </summary>
    public string AuthType { get; set; } = "NoAuth";

    /// <summary>
    /// Optional file path to client mTLS certificate file.
    /// </summary>
    public string? ClientCertificatePath { get; set; }

    /// <summary>
    /// Server RSA public key in PEM, XML, or SubjectPublicKeyInfo format for verifying signed commands.
    /// </summary>
    public string? ServerPublicKey { get; set; }

    // Master Governance & Encryption Template Flags
    /// <summary>
    /// Enforces hardware-bound key derivation for local encrypted secrets.
    /// </summary>
    public bool EnforceHardwareBinding { get; set; } = true;

    /// <summary>
    /// Spool storage encryption cipher ('AES_256_GCM', 'DPAPI', 'Plaintext').
    /// </summary>
    public string SpoolEncryptionMode { get; set; } = "AES_256_GCM";

    /// <summary>
    /// Encrypts in-flight telemetry payloads end-to-end.
    /// </summary>
    public bool TelemetryPayloadEncryption { get; set; } = false;

    /// <summary>
    /// Master policy toggle allowing or prohibiting remote diagnostic/shell commands.
    /// </summary>
    public bool AllowRemoteExecution { get; set; } = true;

    /// <summary>
    /// Allows execution of unsigned commands in development environments; must be false in production.
    /// </summary>
    public bool AllowUnsignedCommands { get; set; } = false;

    /// <summary>
    /// Strictness level for scrubbers stripping PII and tokens from telemetry payloads ('Strict', 'Standard', 'Disabled').
    /// </summary>
    public string PiiScrubberStrictLevel { get; set; } = "Strict";

    // Performance & Limits
    /// <summary>
    /// Token bucket network egress throttle in bytes per second.
    /// </summary>
    public int MaxNetworkEgressBytesPerSec { get; set; } = 1048576; // 1 MB/s Token Bucket

    /// <summary>
    /// Algorithm for deadband delta hashing ('xxHash64', 'SHA256', 'None').
    /// </summary>
    public string DeltaEvaluationAlgorithm { get; set; } = "xxHash64";

    /// <summary>
    /// Deadband tolerance percentage below which telemetry delta updates are suppressed.
    /// </summary>
    public double DeadbandTolerancePercentage { get; set; } = 1.0;

    /// <summary>
    /// Maximum disk space allocated to local offline telemetry spooling in megabytes.
    /// </summary>
    public int MaxSpoolDiskMb { get; set; } = 500;

    /// <summary>
    /// Health check heartbeat interval in seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Polling interval for stable hardware and OS inventory metrics in seconds.
    /// </summary>
    public int HardwarePollIntervalSeconds { get; set; } = 30;

    // Plugin & Extension Architecture
    /// <summary>
    /// Runtime environment descriptor ('Production' or 'Development').
    /// </summary>
    public string Environment { get; set; } = "Production";

    /// <summary>
    /// Allows loading unsigned third-party plugins in development environments.
    /// </summary>
    public bool AllowUnsignedPlugins { get; set; } = false;

    /// <summary>
    /// API authentication key required for third-party local extension submissions.
    /// </summary>
    public string ExtensionApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Directory containing installed agent plugins.
    /// </summary>
    public string PluginsDirectory { get; set; } = "plugins";

    /// <summary>
    /// Directory containing isolated execution sandboxes for plugins.
    /// </summary>
    public string SandboxesDirectory { get; set; } = "sandboxes";

    /// <summary>
    /// Maximum execution duration in seconds permitted for an invoked plugin probe.
    /// </summary>
    public int PluginExecutionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Default time-to-live for third-party extension component data in seconds.
    /// </summary>
    public int DefaultExtensionTtlSeconds { get; set; } = 3600;

    /// <summary>
    /// Maximum payload byte length accepted by the local extension endpoint.
    /// </summary>
    public int ExtensionPayloadMaxBytes { get; set; } = 1048576; // 1 MB

    /// <summary>
    /// Requires extension API callers to originate strictly from loopback addresses (127.0.0.1 / ::1).
    /// </summary>
    public bool RequireLoopbackForExtensions { get; set; } = false;
}

/// <summary>
/// Thread-safe configuration manager providing atomic persistence,
/// cryptographic signature verification, and environment variable fallbacks.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath;
    private readonly object _syncLock = new();
    private AgentConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="initialConfig">Optional initial configuration instance; loads from disk if null.</param>
    public ConfigurationService(ILogger<ConfigurationService> logger, AgentConfig? initialConfig = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configPath = GetDefaultConfigPath();
        _config = initialConfig ?? LoadConfig();
    }

    /// <summary>
    /// Gets the current active daemon configuration snapshot.
    /// </summary>
    public AgentConfig Config
    {
        get
        {
            lock (_syncLock)
            {
                return _config;
            }
        }
    }

    private string GetDefaultConfigPath()
    {
        string basePath;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Heimdall");
        }
        else
        {
            basePath = Path.Combine("/etc", "heimdall");
        }

        if (!Directory.Exists(basePath))
        {
            try
            {
                Directory.CreateDirectory(basePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not create config directory {Path}. Falling back to local directory.", basePath);
                basePath = AppContext.BaseDirectory;
            }
        }

        return Path.Combine(basePath, "agent.json");
    }

    private AgentConfig LoadConfig()
    {
        lock (_syncLock)
        {
            AgentConfig config;
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    config = JsonSerializer.Deserialize<AgentConfig>(json) ?? new AgentConfig();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading config from {Path}", _configPath);
                    config = new AgentConfig();
                }
            }
            else
            {
                config = new AgentConfig();
                var envBackendUrl = Environment.GetEnvironmentVariable("Backend__Url") ?? Environment.GetEnvironmentVariable("BACKEND_URL");
                if (!string.IsNullOrEmpty(envBackendUrl))
                {
                    config.BackendUrl = envBackendUrl;
                }
                SaveConfigInternal(config);
            }

            var envUrl = Environment.GetEnvironmentVariable("Backend__Url") ?? Environment.GetEnvironmentVariable("BACKEND_URL");
            if (!string.IsNullOrEmpty(envUrl) && config.BackendUrl == "http://localhost:5001")
            {
                config.BackendUrl = envUrl;
            }

            return config;
        }
    }

    /// <summary>
    /// Atomically persists the specified configuration to disk.
    /// </summary>
    /// <param name="config">The updated configuration instance.</param>
    public void SaveConfig(AgentConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_syncLock)
        {
            SaveConfigInternal(config);
        }
    }

    private void SaveConfigInternal(AgentConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            string tmpPath = _configPath + ".tmp." + Guid.NewGuid().ToString("N");
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _configPath, overwrite: true);
            _config = config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving config to {Path}", _configPath);
        }
    }

    public bool VerifyCommandSignature(App.Shared.Protos.ServerCommand command)
    {
        return VerifySignature(command.Payload, command.Signature);
    }

    public bool VerifySignature(string payload, string? signatureBase64)
    {
        if (string.IsNullOrEmpty(_config.ServerPublicKey))
        {
            if (_config.AllowUnsignedCommands)
            {
                _logger.LogWarning("SECURITY WARNING: ServerPublicKey is not configured in AgentConfig, but AllowUnsignedCommands is enabled. Insecure dev bypass accepted.");
                return true;
            }

            _logger.LogError("SECURITY REJECTION: ServerPublicKey is not configured in AgentConfig and AllowUnsignedCommands is false. Rejecting command (Fail-Secure).");
            return false;
        }

        if (string.IsNullOrEmpty(signatureBase64))
        {
            _logger.LogWarning("Command signature is empty, but ServerPublicKey is configured. Signature verification failed.");
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            string key = _config.ServerPublicKey.Trim();

            if (key.StartsWith("<"))
            {
                rsa.FromXmlString(key);
            }
            else if (key.Contains("-----BEGIN"))
            {
                rsa.ImportFromPem(key);
            }
            else
            {
                try
                {
                    var bytes = Convert.FromBase64String(key);
                    rsa.ImportSubjectPublicKeyInfo(bytes, out _);
                }
                catch
                {
                    rsa.FromXmlString(key);
                }
            }

            var signature = Convert.FromBase64String(signatureBase64);
            var data = System.Text.Encoding.UTF8.GetBytes(payload);

            bool isValid = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!isValid)
            {
                _logger.LogWarning("Command signature verification failed against configured ServerPublicKey.");
            }
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying RSA command signature.");
            return false;
        }
    }

    public bool UpdateConfigSigned(string jsonConfig, string? signatureBase64)
    {
        if (!VerifySignature(jsonConfig, signatureBase64))
        {
            _logger.LogWarning("Configuration update failed signature verification.");
            return false;
        }

        try
        {
            var newConfig = JsonSerializer.Deserialize<AgentConfig>(jsonConfig);
            if (newConfig != null)
            {
                SaveConfig(newConfig);
                _logger.LogInformation("Configuration updated and verified via RSA signature.");
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying new configuration after signature verification.");
        }

        return false;
    }
}
