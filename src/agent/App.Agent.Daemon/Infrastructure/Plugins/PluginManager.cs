namespace App.Agent.Daemon.Infrastructure.Plugins;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using App.Agent.Daemon.Extensions;
using App.Agent.Daemon.Interfaces;
using App.Shared.Errors;
using App.Shared.Plugins;
using App.Shared.Sanitization;
using Microsoft.Extensions.Logging;

public class PluginManager : IPluginManager
{
    private readonly IConfigurationService _configService;
    private readonly IPluginSandboxService _sandboxService;
    private readonly IExtensionRegistry _extensionRegistry;
    private readonly ILogger<PluginManager> _logger;
    private readonly ConcurrentDictionary<string, PluginPackageBundle> _installedPlugins = new(StringComparer.OrdinalIgnoreCase);

    public PluginManager(
        IConfigurationService configService,
        IPluginSandboxService sandboxService,
        IExtensionRegistry extensionRegistry,
        ILogger<PluginManager> logger)
    {
        _configService = configService;
        _sandboxService = sandboxService;
        _extensionRegistry = extensionRegistry;
        _logger = logger;
    }

    public bool VerifySignature(PluginManifest manifest)
    {
        return VerifySignature(manifest, null);
    }

    public bool VerifySignature(PluginManifest manifest, Dictionary<string, string>? files)
    {
        if (!manifest.IsSigned || string.IsNullOrWhiteSpace(manifest.Signature) || string.IsNullOrWhiteSpace(manifest.PayloadHash))
        {
            return false;
        }

        if (files != null)
        {
            var sortedFiles = files.OrderBy(kv => kv.Key, StringComparer.Ordinal);
            using var sha256 = SHA256.Create();
            var contentBuilder = new StringBuilder();
            foreach (var kvp in sortedFiles)
            {
                contentBuilder.Append(kvp.Key);
                contentBuilder.Append(':');
                contentBuilder.Append(kvp.Value);
                contentBuilder.Append(';');
            }
            byte[] payloadBytes = Encoding.UTF8.GetBytes(contentBuilder.ToString());
            byte[] hashBytes = sha256.ComputeHash(payloadBytes);
            string computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

            if (!string.Equals(computedHash, manifest.PayloadHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Plugin payload hash mismatch for '{PluginId}': Expected {Expected}, Computed {Actual}",
                    manifest.PluginId, manifest.PayloadHash, computedHash);
                return false;
            }
        }

        string? publicKeyPem = _configService.Config.ServerPublicKey;
        if (string.IsNullOrEmpty(publicKeyPem))
        {
            publicKeyPem = Environment.GetEnvironmentVariable("HEIMDALL_MASTER_PUBLIC_KEY");
        }

        if (string.IsNullOrWhiteSpace(publicKeyPem))
        {
            _logger.LogWarning("Plugin verification failed: No ServerPublicKey configured on agent.");
            return false;
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            byte[] hashBytes = Encoding.UTF8.GetBytes(manifest.PayloadHash);
            byte[] sigBytes = Convert.FromBase64String(manifest.Signature);

            return rsa.VerifyData(hashBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RSA verification exception for plugin '{PluginId}'", manifest.PluginId);
            return false;
        }
    }

    /// <summary>
    /// Verifies a double-signed bundle: user signature + server countersignature over the package bytes
    /// (<see cref="PluginPackageBundle.PayloadBase64"/>), both certificates chaining to
    /// <see cref="AgentConfig.TrustedCaCertificatesPem"/>, and the bundle's manifest / files equal the signed package.
    /// </summary>
    public PluginSignatureCheck VerifyDoubleSignature(PluginPackageBundle bundle)
    {
        if (bundle.UserSignature == null || bundle.ServerSignature == null || string.IsNullOrEmpty(bundle.PayloadBase64))
            return new PluginSignatureCheck(false, "The plugin is not double-signed.");

        List<App.Contracts.Security.TrustedCa> trusted;
        try
        {
            trusted = App.Contracts.Security.CertificateChainValidator.ParsePemBundle(_configService.Config.TrustedCaCertificatesPem)
                .Select(c => new App.Contracts.Security.TrustedCa(c, App.Contracts.Security.TrustSources.Imported))
                .ToList();
        }
        catch (CryptographicException)
        {
            return new PluginSignatureCheck(false, "TrustedCaCertificatesPem cannot be parsed.");
        }
        if (trusted.Count == 0)
            return new PluginSignatureCheck(false, "No trusted CA certificates configured (TrustedCaCertificatesPem).");

        byte[] package;
        try { package = Convert.FromBase64String(bundle.PayloadBase64); }
        catch (FormatException) { return new PluginSignatureCheck(false, "The signed package is not valid Base64."); }

        var check = PluginSignatures.VerifyDoubleSigned(package, bundle.UserSignature, bundle.ServerSignature, trusted, serverIssuerMustBeHeimdall: false);
        if (!check.IsValid) return check;

        // The installed content must be exactly the signed content.
        try
        {
            var signed = PluginSignatures.ParsePackage(package);
            if (!string.Equals(StringSanitizer.ToSafeToken(signed.Manifest.PluginId), StringSanitizer.ToSafeToken(bundle.Manifest.PluginId), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(signed.Manifest.Entrypoint, bundle.Manifest.Entrypoint, StringComparison.Ordinal) ||
                PluginSignatures.ComputePayloadHash(signed.Files) != PluginSignatures.ComputePayloadHash(bundle.Files))
                return new PluginSignatureCheck(false, "The bundle's manifest or files differ from the signed package.");
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException)
        {
            return new PluginSignatureCheck(false, "The signed package cannot be parsed.");
        }
        return check;
    }

    public Task<PluginInstallResult> InstallPluginAsync(PluginPackageBundle bundle)
    {
        if (bundle == null || bundle.Manifest == null)
        {
            return Task.FromResult(new PluginInstallResult(false, string.Empty, false, "Bundle cannot be null", ErrorCode.InvalidInput));
        }

        var manifest = bundle.Manifest;
        string safePluginId = StringSanitizer.ToSafeToken(manifest.PluginId);

        if (string.IsNullOrWhiteSpace(safePluginId))
        {
            return Task.FromResult(new PluginInstallResult(false, string.Empty, false, "PluginId is required", ErrorCode.InvalidInput));
        }

        string env = _configService.Config.Environment;
        bool isProduction = string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase);
        bool devMode = !isProduction || _configService.Config.AllowUnsignedPlugins;

        // Double signature (user certificate + server countersignature), both chains to the trusted CAs.
        var doubleSigned = VerifyDoubleSignature(bundle);
        bool signatureValid = doubleSigned.IsValid;

        if (!devMode && !signatureValid)
        {
            _logger.LogWarning("Rejected plugin '{PluginId}': {Reason}", safePluginId, doubleSigned.Reason);
            return Task.FromResult(new PluginInstallResult(
                Success: false,
                PluginId: safePluginId,
                IsSandboxed: false,
                Message: $"Signature verification failed: {doubleSigned.Reason} Unsigned, single-signed or tampered plugins are rejected outside dev mode.",
                ErrorCode: ErrorCode.PluginSignatureInvalid));
        }

        // Dev mode: legacy server-key signatures still count as signed (not sandboxed).
        if (!signatureValid && devMode)
        {
            signatureValid = VerifySignature(manifest, bundle.Files);
        }

        bool isSandboxed = !signatureValid;

        var updatedManifest = manifest with
        {
            PluginId = safePluginId,
            IsSigned = signatureValid
        };

        var finalizedBundle = bundle with
        {
            Manifest = updatedManifest
        };

        _installedPlugins[safePluginId] = finalizedBundle;

        _logger.LogInformation("Installed plugin '{PluginId}' v{Version} (Signed: {IsSigned}, Sandboxed: {IsSandboxed}, Env: {Env})",
            safePluginId, manifest.Version, signatureValid, isSandboxed, env);

        return Task.FromResult(new PluginInstallResult(
            Success: true,
            PluginId: safePluginId,
            IsSandboxed: isSandboxed,
            Message: isSandboxed ? "Installed into development sandbox (unsigned)." : "Installed and verified successfully.",
            ErrorCode: ErrorCode.None));
    }

    public async Task<PluginExecutionResult> ExecutePluginAsync(string pluginId, string[]? arguments = null)
    {
        string safeId = StringSanitizer.ToSafeToken(pluginId);
        if (!_installedPlugins.TryGetValue(safeId, out var bundle))
        {
            return new PluginExecutionResult(
                Success: false,
                PluginId: safeId,
                ExitCode: -1,
                StandardOutput: string.Empty,
                StandardError: $"Plugin '{safeId}' is not installed.",
                IsSandboxed: false,
                ErrorCode: ErrorCode.PluginNotFound);
        }

        return await _sandboxService.ExecuteSandboxedAsync(bundle, arguments);
    }

    public Task<bool> UninstallPluginAsync(string pluginId)
    {
        string safeId = StringSanitizer.ToSafeToken(pluginId);
        bool removed = _installedPlugins.TryRemove(safeId, out _);
        _sandboxService.PurgeSandbox(safeId);
        return Task.FromResult(removed);
    }

    public IReadOnlyList<PluginManifest> GetInstalledPlugins()
    {
        return _installedPlugins.Values.Select(b => b.Manifest).ToList();
    }
}
