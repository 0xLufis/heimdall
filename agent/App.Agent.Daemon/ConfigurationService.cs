using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace App.Agent.Daemon;

public class AgentConfig
{
    public string BackendUrl { get; set; } = "http://localhost:5001";
    public string AuthType { get; set; } = "NoAuth"; // NoAuth, HeimdallCert, UserCert
    public string? ClientCertificatePath { get; set; }
    public string? ServerPublicKey { get; set; } // RSA Public Key for verifying config updates
}

public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly string _configPath;
    private AgentConfig _config;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _configPath = GetDefaultConfigPath();
        _config = LoadConfig();
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

    public bool UpdateConfigSigned(string jsonConfig, string signatureBase64)
    {
        if (string.IsNullOrEmpty(_config.ServerPublicKey))
        {
            _logger.LogWarning("Cannot verify signed config update: ServerPublicKey is not set.");
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.FromXmlString(_config.ServerPublicKey); // Simplified for now, usually PEM or other formats are used

            var signature = Convert.FromBase64String(signatureBase64);
            var data = System.Text.Encoding.UTF8.GetBytes(jsonConfig);

            if (rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            {
                var newConfig = JsonSerializer.Deserialize<AgentConfig>(jsonConfig);
                if (newConfig != null)
                {
                    SaveConfig(newConfig);
                    _logger.LogInformation("Configuration updated and verified via RSA signature.");
                    return true;
                }
            }
            else
            {
                _logger.LogWarning("Configuration update signature verification failed.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signed configuration update.");
        }

        return false;
    }
}
