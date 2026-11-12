namespace App.Backend.Api.Services.Plugins;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Plugins;
using App.Shared.Sanitization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>The user signature of an uploaded plugin is not acceptable (403).</summary>
public class PluginSignatureException : Exception
{
    public PluginSignatureException(string message) : base(message) { }
}

public class PluginService : IPluginService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly AppDbContext? _directDbContext;
    private readonly ILogger<PluginService> _logger;
    private readonly ConcurrentDictionary<string, PluginPackageBundle> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly RSA _rsa;
    private readonly App.Backend.Api.Services.Pki.PkiService? _pki;

    [ActivatorUtilitiesConstructor]
    public PluginService(IServiceScopeFactory scopeFactory, ILogger<PluginService> logger, App.Backend.Api.Services.Pki.PkiService? pki = null)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pki = pki;
        _rsa = InitRsa();
    }

    public PluginService(AppDbContext dbContext, ILogger<PluginService> logger, App.Backend.Api.Services.Pki.PkiService? pki = null)
    {
        _directDbContext = dbContext;
        _logger = logger;
        _pki = pki;
        _rsa = InitRsa();
    }

    private RSA InitRsa()
    {
        var rsa = RSA.Create(2048);
        string? privateKeyPem = Environment.GetEnvironmentVariable("HEIMDALL_MASTER_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(privateKeyPem))
        {
            try
            {
                rsa.ImportFromPem(privateKeyPem);
                _logger.LogInformation("Loaded master RSA signing key from environment.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse HEIMDALL_MASTER_PRIVATE_KEY PEM. Generated ephemeral key pair.");
            }
        }
        return rsa;
    }

    private (AppDbContext Db, IDisposable? Scope) GetDbContext()
    {
        if (_directDbContext != null)
        {
            return (_directDbContext, null);
        }
        if (_scopeFactory != null)
        {
            var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return (db, scope);
        }
        throw new InvalidOperationException("Neither AppDbContext nor IServiceScopeFactory was provided to PluginService.");
    }

    private (App.Backend.Api.Services.Pki.PkiService Pki, IDisposable? Scope) GetPki()
    {
        if (_pki != null) return (_pki, null);
        if (_scopeFactory != null)
        {
            var scope = _scopeFactory.CreateScope();
            return (scope.ServiceProvider.GetRequiredService<App.Backend.Api.Services.Pki.PkiService>(), scope);
        }
        throw new InvalidOperationException("PKI service is not available.");
    }

    public string GetMasterPublicKeyPem()
    {
        return _rsa.ExportRSAPublicKeyPem();
    }

    public Task<PluginManifest> RegisterAndSignPluginAsync(PluginManifest manifest, Dictionary<string, string> files, bool sign = true)
    {
        if (string.IsNullOrWhiteSpace(manifest.PluginId))
            throw new ArgumentException("PluginId must not be empty.", nameof(manifest));
        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw new ArgumentException("Plugin Name must not be empty.", nameof(manifest));
        if (string.IsNullOrWhiteSpace(manifest.Entrypoint))
            throw new ArgumentException("Plugin Entrypoint must not be empty.", nameof(manifest));

        // Sanitize PluginId and Entrypoint
        string safePluginId = StringSanitizer.ToSafeToken(manifest.PluginId);
        string safeEntrypoint = StringSanitizer.StripCrlf(manifest.Entrypoint);

        // Deterministically hash all files to produce PayloadHash
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
        string payloadHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        string signature = string.Empty;
        bool isSigned = false;

        if (sign)
        {
            byte[] sigBytes = _rsa.SignData(Encoding.UTF8.GetBytes(payloadHash), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            signature = Convert.ToBase64String(sigBytes);
            isSigned = true;
        }

        var finalizedManifest = manifest with
        {
            PluginId = safePluginId,
            Entrypoint = safeEntrypoint,
            PayloadHash = payloadHash,
            Signature = signature,
            SignatureAlgorithm = "RSA-SHA256",
            IsSigned = isSigned,
            CreatedUtc = DateTimeOffset.UtcNow
        };

        var bundle = new PluginPackageBundle
        {
            Manifest = finalizedManifest,
            Files = files
        };

        _plugins[safePluginId] = bundle;
        _logger.LogInformation("Registered plugin {PluginId} v{Version} (Signed: {IsSigned})", 
            safePluginId, finalizedManifest.Version, isSigned);

        return Task.FromResult(finalizedManifest);
    }

    /// <summary>
    /// Registers a double-signed plugin: verifies the installing user's detached signature over the
    /// package (hash, signature, chain to a trusted CA, Heimdall revocation, certificate maps to the
    /// calling user), then countersigns (package hash + user signature) with the server's computer
    /// certificate. Throws <see cref="PluginSignatureException"/> when the user signature is not acceptable.
    /// </summary>
    public async Task<PluginPackageBundle> RegisterSignedPluginAsync(byte[] packageBytes, PluginSignatureEnvelope userSignature, string callerUserId)
    {
        var (pki, pkiScope) = GetPki();
        using var pkiScopeGuard = pkiScope;
        if (userSignature.Format != PluginSignatures.UserFormat)
            throw new PluginSignatureException($"Unknown signature format '{userSignature.Format}'.");

        var packageSha256 = PluginSignatures.Sha256Hex(packageBytes);
        if (!string.Equals(packageSha256, userSignature.PackageSha256, StringComparison.OrdinalIgnoreCase))
            throw new PluginSignatureException("The signature was made for a different package (SHA-256 mismatch).");
        if (!PluginSignatures.VerifySignature(userSignature, Convert.FromHexString(packageSha256), out _))
            throw new PluginSignatureException("The user signature does not verify against the signer certificate.");

        var validation = await pki.ValidateAsync(userSignature.CertificateChainPem);
        if (!validation.Valid)
            throw new PluginSignatureException($"The signer certificate is not trusted: {validation.Status} - {validation.Reason}");
        if (validation.User == null)
            throw new PluginSignatureException("The signer certificate does not map to a Heimdall user (UPN / e-mail).");
        if (!string.Equals(validation.User.UserId, callerUserId, StringComparison.Ordinal))
            throw new PluginSignatureException("The plugin must be signed with the uploading user's own certificate.");

        PluginPackageFile package;
        try { package = PluginSignatures.ParsePackage(packageBytes); }
        catch (Exception ex) when (ex is JsonException or FormatException) { throw new ArgumentException($"Invalid plugin package: {ex.Message}"); }
        if (package.Files == null || package.Files.Count == 0) throw new ArgumentException("The plugin package has no files.");

        var manifest = await RegisterAndSignPluginAsync(package.Manifest, package.Files, sign: true);

        using var serverCert = await pki.GetOrCreateServerSigningCertificateAsync();
        var serverChain = await pki.BuildChainPemAsync(serverCert);
        var countersignature = PluginSignatures.Sign(serverCert, serverChain,
            PluginSignatures.CountersignedHash(packageSha256, userSignature.Signature), packageSha256, PluginSignatures.ServerFormat);

        var bundle = new PluginPackageBundle
        {
            Manifest = manifest,
            Files = package.Files,
            PayloadBase64 = Convert.ToBase64String(packageBytes),
            UserSignature = userSignature,
            ServerSignature = countersignature
        };
        _plugins[manifest.PluginId] = bundle;
        _logger.LogInformation("Registered double-signed plugin {PluginId} v{Version}: user {User} ({UserCert}), countersigned by {ServerCert}",
            manifest.PluginId, manifest.Version, validation.User.Email, validation.Thumbprint, serverCert.Thumbprint);
        return bundle;
    }

    public Task<PluginPackageBundle?> GetPluginBundleAsync(string pluginId)
    {
        _plugins.TryGetValue(pluginId, out var bundle);
        return Task.FromResult(bundle);
    }

    public Task<List<PluginManifest>> ListPluginsAsync()
    {
        var list = _plugins.Values.Select(b => b.Manifest).ToList();
        return Task.FromResult(list);
    }

    public bool VerifyPluginSignature(PluginManifest manifest)
    {
        if (!manifest.IsSigned || string.IsNullOrEmpty(manifest.Signature) || string.IsNullOrEmpty(manifest.PayloadHash))
            return false;

        try
        {
            byte[] hashBytes = Encoding.UTF8.GetBytes(manifest.PayloadHash);
            byte[] sigBytes = Convert.FromBase64String(manifest.Signature);
            return _rsa.VerifyData(hashBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PushPluginToAgentAsync(string pluginId, Guid clientPcId)
    {
        var bundle = await GetPluginBundleAsync(pluginId);
        if (bundle == null) return false;

        var (db, scope) = GetDbContext();
        try
        {
            var clientPc = await db.ClientPcs.FindAsync(clientPcId);
            if (clientPc == null) return false;

            string payloadJson = JsonSerializer.Serialize(bundle);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            byte[] sigBytes = _rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string commandSignature = Convert.ToBase64String(sigBytes);

            var command = new QueuedAgentCommand
            {
                Id = Guid.NewGuid(),
                ClientPcId = clientPc.Id,
                Type = "INSTALL_PLUGIN",
                Payload = payloadJson,
                Signature = commandSignature,
                IsProcessed = false,
                CreatedAt = DateTime.UtcNow
            };

            db.QueuedAgentCommands.Add(command);
            await db.SaveChangesAsync();

            _logger.LogInformation("Queued INSTALL_PLUGIN command for {PluginId} targeting client PC {ClientPcId} ({Hostname})",
                pluginId, clientPcId, clientPc.Hostname);

            return true;
        }
        finally
        {
            scope?.Dispose();
        }
    }

    public async Task<bool> PushPluginToMachineAsync(string pluginId, Guid machineId)
    {
        var (db, scope) = GetDbContext();
        try
        {
            var machine = await db.Machines
                .Include(m => m.Controllers)
                .FirstOrDefaultAsync(m => m.Id == machineId);

            if (machine == null || !machine.Controllers.Any()) return false;

            bool pushedAny = false;
            foreach (var ctrl in machine.Controllers)
            {
                bool success = await PushPluginToAgentAsync(pluginId, ctrl.Id);
                if (success) pushedAny = true;
            }

            return pushedAny;
        }
        finally
        {
            scope?.Dispose();
        }
    }

    public Task<bool> DeletePluginAsync(string pluginId)
    {
        bool removed = _plugins.TryRemove(pluginId, out _);
        return Task.FromResult(removed);
    }
}
