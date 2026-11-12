namespace App.Agent.Daemon.Configuration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

/// <summary>One configuration value that is outside what the agent can safely run with.</summary>
/// <param name="Field">The <see cref="AgentConfig"/> property.</param>
/// <param name="Problem">What is wrong with the value.</param>
/// <param name="IsError">
/// True when the value must not be accepted from an update. On load an error is repaired (see
/// <paramref name="Repair"/>) instead of taking the service down.
/// </param>
/// <param name="Repair">What load-time repair did, or null when the value was left as it is.</param>
public sealed record ConfigIssue(string Field, string Problem, bool IsError, string? Repair = null)
{
    public override string ToString() =>
        Repair == null ? $"{Field}: {Problem}" : $"{Field}: {Problem} ({Repair})";
}

/// <summary>
/// Sanity limits for <see cref="AgentConfig"/>. Used in two ways:
/// <list type="bullet">
/// <item><see cref="Validate"/> - decides whether a configuration update (signed command, local API)
/// may be accepted at all. Only spelling is normalized (case of enumerations, surrounding whitespace).</item>
/// <item><see cref="Repair"/> - makes a configuration read from disk safe to run with: an interval of 0,
/// a negative quota or a NaN tolerance would otherwise spin a loop, evict the whole spool or throw
/// on every cycle. Values are clamped or reset to their defaults and every change is reported.</item>
/// </list>
/// </summary>
public static class AgentConfigValidator
{
    public static readonly IReadOnlyList<string> AuthTypes = new[] { "NoAuth", "HeimdallCert", "UserCert", "ApiKey" };
    public static readonly IReadOnlyList<string> Environments = new[] { "Production", "Staging", "Development", "Test" };
    public static readonly IReadOnlyList<string> SpoolEncryptionModes = new[] { "AES_256_GCM", "DPAPI", "Plaintext" };
    public static readonly IReadOnlyList<string> PiiScrubberLevels = new[] { "Strict", "Standard", "Disabled" };
    public static readonly IReadOnlyList<string> DeltaAlgorithms = new[] { "xxHash64", "SHA256", "None" };
    public static readonly IReadOnlyList<string> ChecksumAlgorithms = new[] { "SHA256", "SHA384", "SHA512" };

    public const int MaxWatchDirectories = 64;
    public const int MaxPathLength = 1024;

    /// <summary>Checks a candidate configuration without changing it.</summary>
    public static IReadOnlyList<ConfigIssue> Validate(AgentConfig config) => Inspect(config, repair: false);

    /// <summary>Repairs a configuration in place and returns what was wrong and what was done about it.</summary>
    public static IReadOnlyList<ConfigIssue> Repair(AgentConfig config) => Inspect(config, repair: true);

    private static List<ConfigIssue> Inspect(AgentConfig config, bool repair)
    {
        ArgumentNullException.ThrowIfNull(config);
        var defaults = new AgentConfig();
        var issues = new List<ConfigIssue>();

        // --- Numeric limits -------------------------------------------------------------------
        config.HeartbeatIntervalSeconds = Range(issues, repair, nameof(AgentConfig.HeartbeatIntervalSeconds),
            config.HeartbeatIntervalSeconds, 1, 86_400, defaults.HeartbeatIntervalSeconds);
        config.HardwarePollIntervalSeconds = Range(issues, repair, nameof(AgentConfig.HardwarePollIntervalSeconds),
            config.HardwarePollIntervalSeconds, 1, 604_800, defaults.HardwarePollIntervalSeconds);
        config.MaxSpoolDiskMb = Range(issues, repair, nameof(AgentConfig.MaxSpoolDiskMb),
            config.MaxSpoolDiskMb, 1, 1_048_576, defaults.MaxSpoolDiskMb);
        config.MaxMemoryMb = Range(issues, repair, nameof(AgentConfig.MaxMemoryMb),
            config.MaxMemoryMb, 64, 1_048_576, defaults.MaxMemoryMb);
        config.MaxNetworkEgressBytesPerSec = Range(issues, repair, nameof(AgentConfig.MaxNetworkEgressBytesPerSec),
            config.MaxNetworkEgressBytesPerSec, 1024, int.MaxValue, defaults.MaxNetworkEgressBytesPerSec);
        config.PluginExecutionTimeoutSeconds = Range(issues, repair, nameof(AgentConfig.PluginExecutionTimeoutSeconds),
            config.PluginExecutionTimeoutSeconds, 1, 3600, defaults.PluginExecutionTimeoutSeconds);
        config.DefaultExtensionTtlSeconds = Range(issues, repair, nameof(AgentConfig.DefaultExtensionTtlSeconds),
            config.DefaultExtensionTtlSeconds, 1, 31_536_000, defaults.DefaultExtensionTtlSeconds);
        config.ExtensionPayloadMaxBytes = Range(issues, repair, nameof(AgentConfig.ExtensionPayloadMaxBytes),
            config.ExtensionPayloadMaxBytes, 64, 104_857_600, defaults.ExtensionPayloadMaxBytes);
        config.MaxReportPayloadBytes = Range(issues, repair, nameof(AgentConfig.MaxReportPayloadBytes),
            config.MaxReportPayloadBytes, 16_384, 67_108_864, defaults.MaxReportPayloadBytes);
        config.SessionHeartbeatSeconds = Range(issues, repair, nameof(AgentConfig.SessionHeartbeatSeconds),
            config.SessionHeartbeatSeconds, 5, 3600, defaults.SessionHeartbeatSeconds);

        if (double.IsNaN(config.DeadbandTolerancePercentage) || double.IsInfinity(config.DeadbandTolerancePercentage) ||
            config.DeadbandTolerancePercentage < 0 || config.DeadbandTolerancePercentage > 100)
        {
            string problem = $"{config.DeadbandTolerancePercentage} is not a percentage between 0 and 100";
            if (repair)
            {
                config.DeadbandTolerancePercentage = double.IsFinite(config.DeadbandTolerancePercentage)
                    ? Math.Clamp(config.DeadbandTolerancePercentage, 0, 100)
                    : defaults.DeadbandTolerancePercentage;
                issues.Add(new ConfigIssue(nameof(AgentConfig.DeadbandTolerancePercentage), problem, true, $"set to {config.DeadbandTolerancePercentage}"));
            }
            else
            {
                issues.Add(new ConfigIssue(nameof(AgentConfig.DeadbandTolerancePercentage), problem, true));
            }
        }

        // --- Connectivity ---------------------------------------------------------------------
        if (!TryValidateBackendUrl(config.BackendUrl, out var urlProblem))
        {
            if (repair)
            {
                // No previous value to go back to at load time; the default keeps the env override working.
                config.BackendUrl = defaults.BackendUrl;
                issues.Add(new ConfigIssue(nameof(AgentConfig.BackendUrl), urlProblem!, true, $"reset to {defaults.BackendUrl}"));
            }
            else
            {
                issues.Add(new ConfigIssue(nameof(AgentConfig.BackendUrl), urlProblem!, true));
            }
        }
        else
        {
            config.BackendUrl = config.BackendUrl.Trim();
        }

        string? canonicalAuth = Canonical(AuthTypes, config.AuthType);
        if (canonicalAuth == null)
        {
            // Never rewritten on load: silently turning an unknown mode into "NoAuth" would be a downgrade.
            issues.Add(new ConfigIssue(nameof(AgentConfig.AuthType),
                $"'{Printable(config.AuthType)}' is not one of {string.Join(", ", AuthTypes)}", true));
        }
        else
        {
            config.AuthType = canonicalAuth;
        }

        if (config.ClientCertificatePath != null && !IsUsablePath(config.ClientCertificatePath, allowParentSegments: true))
        {
            issues.Add(new ConfigIssue(nameof(AgentConfig.ClientCertificatePath), "is not a usable file path", true,
                repair ? "ignored" : null));
            if (repair) config.ClientCertificatePath = null;
        }

        if (!string.IsNullOrWhiteSpace(config.ServerPublicKey) && !IsParseablePublicKey(config.ServerPublicKey))
        {
            // Kept on load (a broken key is fail-secure: every signed command is rejected), refused in updates.
            issues.Add(new ConfigIssue(nameof(AgentConfig.ServerPublicKey),
                "cannot be parsed as an RSA public key (PEM, XML or Base64 SubjectPublicKeyInfo); signed commands will be rejected", true));
        }

        if (!string.IsNullOrWhiteSpace(config.TrustedCaCertificatesPem) && !IsParseableCertificateBundle(config.TrustedCaCertificatesPem))
        {
            // Fail-secure like ServerPublicKey: an unreadable bundle means every signed plugin is rejected.
            issues.Add(new ConfigIssue(nameof(AgentConfig.TrustedCaCertificatesPem),
                "contains no parseable PEM certificate; signed plugins will be rejected", true));
        }

        // --- Policy enumerations --------------------------------------------------------------
        string? canonicalEnvironment = Canonical(Environments, config.Environment);
        if (canonicalEnvironment == null)
        {
            // Anything that is not "Production" relaxes the plugin signature policy, so an unreadable
            // value must fail towards Production.
            string problem = $"'{Printable(config.Environment)}' is not one of {string.Join(", ", Environments)}";
            if (repair)
            {
                config.Environment = "Production";
                issues.Add(new ConfigIssue(nameof(AgentConfig.Environment), problem, true, "treated as Production"));
            }
            else
            {
                issues.Add(new ConfigIssue(nameof(AgentConfig.Environment), problem, true));
            }
        }
        else
        {
            config.Environment = canonicalEnvironment;
        }

        config.SpoolEncryptionMode = Enumeration(issues, repair, nameof(AgentConfig.SpoolEncryptionMode), config.SpoolEncryptionMode, SpoolEncryptionModes, defaults.SpoolEncryptionMode);
        config.PiiScrubberStrictLevel = Enumeration(issues, repair, nameof(AgentConfig.PiiScrubberStrictLevel), config.PiiScrubberStrictLevel, PiiScrubberLevels, defaults.PiiScrubberStrictLevel);
        config.DeltaEvaluationAlgorithm = Enumeration(issues, repair, nameof(AgentConfig.DeltaEvaluationAlgorithm), config.DeltaEvaluationAlgorithm, DeltaAlgorithms, defaults.DeltaEvaluationAlgorithm);
        config.RecipeChecksumAlgorithm = Enumeration(issues, repair, nameof(AgentConfig.RecipeChecksumAlgorithm), config.RecipeChecksumAlgorithm, ChecksumAlgorithms, defaults.RecipeChecksumAlgorithm);

        // --- Paths and free text --------------------------------------------------------------
        config.PluginsDirectory = Directory(issues, repair, nameof(AgentConfig.PluginsDirectory), config.PluginsDirectory, defaults.PluginsDirectory);
        config.SandboxesDirectory = Directory(issues, repair, nameof(AgentConfig.SandboxesDirectory), config.SandboxesDirectory, defaults.SandboxesDirectory);

        if (config.RecipeWatchDirectories == null)
        {
            issues.Add(new ConfigIssue(nameof(AgentConfig.RecipeWatchDirectories), "is null", true, repair ? "reset to defaults" : null));
            if (repair) config.RecipeWatchDirectories = defaults.RecipeWatchDirectories;
        }
        else
        {
            var usable = config.RecipeWatchDirectories
                .Where(d => IsUsablePath(d, allowParentSegments: true))
                .Select(d => d.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(MaxWatchDirectories)
                .ToList();
            if (usable.Count != config.RecipeWatchDirectories.Count)
            {
                issues.Add(new ConfigIssue(nameof(AgentConfig.RecipeWatchDirectories),
                    $"{config.RecipeWatchDirectories.Count - usable.Count} of {config.RecipeWatchDirectories.Count} entries are empty, duplicated, unusable or beyond the limit of {MaxWatchDirectories}",
                    true, repair ? "dropped" : null));
                if (repair) config.RecipeWatchDirectories = usable;
            }
        }

        if (string.IsNullOrWhiteSpace(config.ConfigSchemaVersion) || config.ConfigSchemaVersion.Length > 32 || HasControlCharacters(config.ConfigSchemaVersion))
        {
            issues.Add(new ConfigIssue(nameof(AgentConfig.ConfigSchemaVersion), "is empty or not a version string", false,
                repair ? $"reset to {defaults.ConfigSchemaVersion}" : null));
            if (repair) config.ConfigSchemaVersion = defaults.ConfigSchemaVersion;
        }

        if (config.ExtensionApiKey == null)
        {
            config.ExtensionApiKey = string.Empty;
        }
        else if (config.ExtensionApiKey.Length > 4096 || HasControlCharacters(config.ExtensionApiKey))
        {
            // Not repaired: replacing the key with "" would switch extension authentication off.
            issues.Add(new ConfigIssue(nameof(AgentConfig.ExtensionApiKey), "is longer than 4096 characters or contains control characters", true));
        }

        return issues;
    }

    /// <summary>The backend URL must be an absolute http(s) URL with a host; the MQTT broker host is derived from it.</summary>
    public static bool TryValidateBackendUrl(string? value, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            problem = "is empty";
            return false;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > 2048 || HasControlCharacters(trimmed))
        {
            problem = "is longer than 2048 characters or contains control characters";
            return false;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.Host))
        {
            problem = $"'{Printable(trimmed)}' is not an absolute http:// or https:// URL";
            return false;
        }

        return true;
    }

    /// <summary>True when the text can be loaded the way <c>ConfigurationService.VerifySignature</c> loads it.</summary>
    public static bool IsParseableCertificateBundle(string pem)
    {
        try { return App.Contracts.Security.CertificateChainValidator.ParsePemBundle(pem).Count > 0; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }

    public static bool IsParseablePublicKey(string key)
    {
        try
        {
            using var rsa = RSA.Create();
            string trimmed = key.Trim();
            if (trimmed.StartsWith('<'))
            {
                rsa.FromXmlString(trimmed);
            }
            else if (trimmed.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                rsa.ImportFromPem(trimmed);
            }
            else
            {
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(trimmed), out _);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int Range(List<ConfigIssue> issues, bool repair, string field, int value, int min, int max, int fallback)
    {
        if (value >= min && value <= max) return value;

        string problem = $"{value} is outside {min}..{max}";
        if (!repair)
        {
            issues.Add(new ConfigIssue(field, problem, true));
            return value;
        }

        // Zero / negative means "not a real setting" (torn or hand-broken): use the default.
        // A real but excessive value is clamped to the nearest limit.
        int repaired = value <= 0 ? fallback : Math.Clamp(value, min, max);
        issues.Add(new ConfigIssue(field, problem, true, $"set to {repaired}"));
        return repaired;
    }

    private static string Enumeration(List<ConfigIssue> issues, bool repair, string field, string? value, IReadOnlyList<string> allowed, string fallback)
    {
        string? canonical = Canonical(allowed, value);
        if (canonical != null) return canonical;

        string problem = $"'{Printable(value)}' is not one of {string.Join(", ", allowed)}";
        if (!repair)
        {
            issues.Add(new ConfigIssue(field, problem, true));
            return value ?? string.Empty;
        }

        issues.Add(new ConfigIssue(field, problem, true, $"reset to {fallback}"));
        return fallback;
    }

    private static string Directory(List<ConfigIssue> issues, bool repair, string field, string? value, string fallback)
    {
        if (value != null && IsUsablePath(value, allowParentSegments: false)) return value.Trim();

        const string problem = "is empty, contains control characters or '..' segments";
        if (!repair)
        {
            issues.Add(new ConfigIssue(field, problem, true));
            return value ?? string.Empty;
        }

        issues.Add(new ConfigIssue(field, problem, true, $"reset to {fallback}"));
        return fallback;
    }

    private static string? Canonical(IReadOnlyList<string> allowed, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string trimmed = value.Trim();
        return allowed.FirstOrDefault(a => string.Equals(a, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUsablePath(string? path, bool allowParentSegments)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > MaxPathLength || HasControlCharacters(path)) return false;
        if (allowParentSegments) return true;
        return !path.Split('/', '\\').Any(segment => segment == "..");
    }

    private static bool HasControlCharacters(string text)
    {
        foreach (char c in text)
        {
            if (char.IsControl(c)) return true;
        }
        return false;
    }

    private static string Printable(string? text)
    {
        if (text == null) return "null";
        var cleaned = new string(text.Select(c => char.IsControl(c) ? '?' : c).ToArray());
        return cleaned.Length <= 80 ? cleaned : cleaned[..80] + "...";
    }
}
