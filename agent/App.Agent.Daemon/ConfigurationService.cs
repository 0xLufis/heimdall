using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace App.Agent.Daemon;

public class AgentConfig
{
    // Schema Version
    public string ConfigSchemaVersion { get; set; } = "1.0.0";

    // Connectivity
    public string BackendUrl { get; set; } = "http://localhost:5001";
    public string AuthType { get; set; } = "NoAuth"; // NoAuth, HeimdallCert, UserCert
    public string? ClientCertificatePath { get; set; }
    public string? ServerPublicKey { get; set; } // RSA Public Key for verifying config updates

    // Master Governance & Encryption Template Flags
    public bool EnforceHardwareBinding { get; set; } = true;
    public string SpoolEncryptionMode { get; set; } = "AES_256_GCM"; // AES_256_GCM, DPAPI, Plaintext
    public bool TelemetryPayloadEncryption { get; set; } = false;
    public bool AllowRemoteExecution { get; set; } = true;
    public bool AllowUnsignedCommands { get; set; } = false; // Must be explicitly enabled in Dev/Testing; default fail-secure
    public string PiiScrubberStrictLevel { get; set; } = "Strict"; // Strict, Standard, Disabled

    // Performance & Limits
    public int MaxNetworkEgressBytesPerSec { get; set; } = 1048576; // 1 MB/s Token Bucket
    public string DeltaEvaluationAlgorithm { get; set; } = "xxHash64"; // xxHash64, SHA256, None
    public double DeadbandTolerancePercentage { get; set; } = 1.0;
    public int MaxSpoolDiskMb { get; set; } = 500;
    public int HeartbeatIntervalSeconds { get; set; } = 10;
}

public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath;
    private AgentConfig _config;

    public ConfigurationService(ILogger<ConfigurationService> logger, AgentConfig? initialConfig = null)
    {
        _logger = logger;
        _configPath = GetDefaultConfigPath();
        _config = initialConfig ?? LoadConfig();
    }

    public AgentConfig Config => _config;

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
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AgentConfig>(json) ?? new AgentConfig();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading config from {Path}", _configPath);
            }
        }
        
        var defaultConfig = new AgentConfig();
        SaveConfig(defaultConfig);
        return defaultConfig;
    }

    public void SaveConfig(AgentConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
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

    public bool UpdateConfigSigned(string jsonConfig, string signatureBase64)
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
