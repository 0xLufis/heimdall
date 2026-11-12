using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

using App.Agent.Daemon.Configuration;
using App.Agent.Daemon.Infrastructure.Persistence;
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
    /// Upstream Heimdall backend base URL for HTTP calls (telemetry itself goes over MQTT).
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

    /// <summary>
    /// PEM bundle of the CA certificates the agent trusts for plugin signatures (Heimdall root /
    /// intermediate and, for AD-issued user certificates, the AD CA chain). Download it from
    /// <c>GET /api/v1/certificatemanagement/trust-bundle</c>. Outside dev mode a plugin is installed
    /// only when its user signature and the server countersignature both chain to these CAs.
    /// </summary>
    public string? TrustedCaCertificatesPem { get; set; }

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
    /// Maximum allowable process memory usage in megabytes before garbage collection or alerting triggers.
    /// </summary>
    public int MaxMemoryMb { get; set; } = 512;

    /// <summary>
    /// Health check heartbeat interval in seconds.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// Polling interval for stable hardware and OS inventory metrics in seconds.
    /// </summary>
    public int HardwarePollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Enables or disables the internal supervisor watchdog monitoring service.
    /// </summary>
    public bool WatchdogEnabled { get; set; } = true;

    /// <summary>
    /// Directory paths monitored by the real-time recipe and production config watcher.
    /// </summary>
    public List<string> RecipeWatchDirectories { get; set; } = new() { "recipes", "cnc_programs" };

    /// <summary>
    /// Cryptographic hashing algorithm for recipe drift detection.
    /// </summary>
    public string RecipeChecksumAlgorithm { get; set; } = "SHA256";

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

    // Resilience
    /// <summary>
    /// Upper bound in bytes for one serialized report published to the backend. A report that is
    /// still larger after its optional inventory lists were trimmed is rejected instead of sent.
    /// </summary>
    public int MaxReportPayloadBytes { get; set; } = 1048576; // 1 MiB

    /// <summary>
    /// Interval in seconds at which the session marker ("last seen alive") is flushed to disk. After a
    /// power loss the next start can only tell when the machine went down to this precision.
    /// </summary>
    public int SessionHeartbeatSeconds { get; set; } = 60;

    /// <summary>
    /// Properties found in <c>agent.json</c> that this agent version does not know. They are kept and
    /// written back unchanged so saving the configuration never drops settings of a newer version or
    /// of deployment tooling.
    /// </summary>
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

/// <summary>
/// Thread-safe configuration manager providing durable atomic persistence with a last-known-good
/// backup, validation of loaded and updated values, cryptographic signature verification, and
/// environment variable fallbacks.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions MergeReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ILogger<ConfigurationService> _logger;
    private readonly AgentConfigStore _store;
    private readonly IRecoveryJournal? _recoveryJournal;
    private readonly object _syncLock = new();
    private AgentConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="initialConfig">Optional initial configuration instance; loads from disk if null.</param>
    /// <param name="paths">File layout; resolved for this machine when null.</param>
    /// <param name="recoveryJournal">Receives a notice when the configuration had to be recovered or repaired.</param>
    public ConfigurationService(
        ILogger<ConfigurationService> logger,
        AgentConfig? initialConfig = null,
        AgentPaths? paths = null,
        IRecoveryJournal? recoveryJournal = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _recoveryJournal = recoveryJournal;

        string configPath;
        if (paths != null)
        {
            configPath = paths.ConfigFile;
            if (paths.ConfigDirectoryWarning != null) _logger.LogWarning("{Warning}", paths.ConfigDirectoryWarning);
        }
        else
        {
            configPath = Path.Combine(AgentPaths.ResolveConfigDirectory(out var warning), AgentPaths.ConfigFileName);
            if (warning != null) _logger.LogWarning("{Warning}", warning);
        }

        _store = new AgentConfigStore(configPath);

        if (initialConfig != null)
        {
            _config = initialConfig;
        }
        else
        {
            LastLoad = _store.Load(Environment.GetEnvironmentVariable("Backend__Url") ?? Environment.GetEnvironmentVariable("BACKEND_URL"));
            _config = LastLoad.Config;
            ReportLoadOutcome(LastLoad);
        }
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

    /// <summary>Full path of <c>agent.json</c>.</summary>
    public string ConfigPath => _store.Path;

    /// <summary>
    /// How the configuration was obtained at start-up (primary file, backup, defaults, first run) and
    /// which values had to be repaired. Null when the configuration was injected.
    /// </summary>
    public ConfigLoadResult? LastLoad { get; }

    private void ReportLoadOutcome(ConfigLoadResult result)
    {
        string defect = RecoveryNotice.DefectName(result.Defect);

        switch (result.Origin)
        {
            case ConfigOrigin.Backup:
                _logger.LogWarning(
                    "Configuration file {Path} was {Defect} ({Detail}); restored the last known good copy from {Backup}. Damaged file kept as {Quarantined}.",
                    _store.Path, defect, result.Detail ?? "no detail", _store.BackupPath, result.QuarantinedCopy ?? "(not kept)");
                _recoveryJournal?.Record(new RecoveryNotice
                {
                    Component = "config",
                    File = _store.Path,
                    Defect = defect,
                    RecoveredFrom = "backup",
                    Severity = "Warning",
                    Detail = $"{result.Detail ?? "no detail"}; restored from {Path.GetFileName(_store.BackupPath)}" +
                             (result.QuarantinedCopy != null ? $"; damaged file kept as {Path.GetFileName(result.QuarantinedCopy)}" : string.Empty)
                });
                break;

            case ConfigOrigin.Defaults:
                _logger.LogCritical(
                    "Configuration file {Path} was {Defect} ({Detail}) and no usable backup exists. Running with safe defaults: " +
                    "server public key, certificates and API keys are gone, signed commands are rejected and the extension API is loopback-only " +
                    "until the configuration is restored. Damaged file kept as {Quarantined}.",
                    _store.Path, defect, result.Detail ?? "no detail", result.QuarantinedCopy ?? "(not kept)");
                _recoveryJournal?.Record(new RecoveryNotice
                {
                    Component = "config",
                    File = _store.Path,
                    Defect = defect,
                    RecoveredFrom = "defaults",
                    Severity = "Critical",
                    Detail = $"{result.Detail ?? "no detail"}; no usable backup, safe defaults active (trust anchors and credentials reset, commands rejected)" +
                             (result.QuarantinedCopy != null ? $"; damaged file kept as {Path.GetFileName(result.QuarantinedCopy)}" : string.Empty)
                });
                break;

            case ConfigOrigin.FirstRun:
                _logger.LogInformation("No configuration found; created {Path} with defaults.", _store.Path);
                break;
        }

        if (result.Issues.Count > 0)
        {
            foreach (var issue in result.Issues)
            {
                _logger.LogWarning("Configuration value rejected: {Issue}", issue.ToString());
            }

            _recoveryJournal?.Record(new RecoveryNotice
            {
                Component = "config",
                File = _store.Path,
                Defect = "invalid",
                RecoveredFrom = "defaults",
                Severity = "Warning",
                Detail = "values outside sane limits: " + string.Join("; ", result.Issues.Select(i => i.ToString()))
            });
        }
    }

    /// <summary>
    /// Durably and atomically persists the specified configuration to disk and makes it the active
    /// configuration. If it cannot be written the active configuration is left unchanged.
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

    private bool SaveConfigInternal(AgentConfig config)
    {
        if (_store.TrySave(config, out var error))
        {
            _config = config;
            return true;
        }

        _logger.LogError("Error saving config to {Path}: {Error}", _store.Path, error);
        return false;
    }

    /// <summary>
    /// Applies a change to a copy of the active configuration, validates the result and, only if it
    /// is acceptable, persists and activates it. The active configuration object is never mutated.
    /// </summary>
    /// <returns>True when the change was validated, persisted and activated.</returns>
    public bool TryUpdate(Action<AgentConfig> change, out IReadOnlyList<ConfigIssue> problems)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_syncLock)
        {
            AgentConfig candidate;
            try
            {
                candidate = Clone(_config);
                change(candidate);
            }
            catch (Exception ex)
            {
                problems = new[] { new ConfigIssue("(update)", ex.Message, true) };
                return false;
            }

            return TryActivate(candidate, out problems);
        }
    }

    private bool TryActivate(AgentConfig candidate, out IReadOnlyList<ConfigIssue> problems)
    {
        problems = AgentConfigValidator.Validate(candidate).Where(i => i.IsError).ToList();
        if (problems.Count > 0)
        {
            return false;
        }

        if (!SaveConfigInternal(candidate))
        {
            problems = new[] { new ConfigIssue("(persistence)", $"could not write {_store.Path}", true) };
            return false;
        }

        return true;
    }

    private static AgentConfig Clone(AgentConfig config) =>
        JsonSerializer.Deserialize<AgentConfig>(JsonSerializer.Serialize(config)) ?? new AgentConfig();

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

        lock (_syncLock)
        {
            AgentConfig candidate;
            try
            {
                candidate = MergeOnto(_config, jsonConfig);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or NotSupportedException or ArgumentException)
            {
                _logger.LogError("Configuration update rejected: payload is not a usable configuration object ({Error}).", ex.Message);
                return false;
            }

            if (!TryActivate(candidate, out var problems))
            {
                _logger.LogError("Configuration update rejected, active configuration unchanged: {Problems}",
                    string.Join("; ", problems.Select(p => p.ToString())));
                return false;
            }
        }

        _logger.LogInformation("Configuration updated and verified via RSA signature.");
        return true;
    }

    /// <summary>
    /// Overlays the properties present in <paramref name="jsonConfig"/> on the active configuration.
    /// A payload therefore changes exactly what it names: a partial update no longer resets every
    /// omitted setting (backend URL, server public key, API keys) to its default, and property names
    /// are matched case-insensitively. To clear a value it has to be sent explicitly as null.
    /// </summary>
    internal static AgentConfig MergeOnto(AgentConfig current, string jsonConfig)
    {
        var documentOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        if (JsonNode.Parse(jsonConfig, documentOptions: documentOptions) is not JsonObject incoming)
        {
            throw new JsonException("configuration payload must be a JSON object");
        }

        var merged = JsonSerializer.SerializeToNode(current)?.AsObject() ?? new JsonObject();
        foreach (var property in incoming)
        {
            string key = merged.Select(existing => existing.Key)
                .FirstOrDefault(existing => string.Equals(existing, property.Key, StringComparison.OrdinalIgnoreCase)) ?? property.Key;
            merged[key] = property.Value?.DeepClone();
        }

        return merged.Deserialize<AgentConfig>(MergeReadOptions)
               ?? throw new JsonException("configuration payload deserialized to null");
    }
}
