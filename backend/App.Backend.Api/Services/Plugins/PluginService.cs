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
using Microsoft.Extensions.Logging;

public class PluginService : IPluginService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PluginService> _logger;
    private readonly ConcurrentDictionary<string, PluginPackageBundle> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly RSA _rsa;

    public PluginService(AppDbContext dbContext, ILogger<PluginService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        // Initialize RSA cryptographic authority
        _rsa = RSA.Create(2048);
        string? privateKeyPem = Environment.GetEnvironmentVariable("HEIMDALL_MASTER_PRIVATE_KEY");
        if (!string.IsNullOrEmpty(privateKeyPem))
        {
            try
            {
                _rsa.ImportFromPem(privateKeyPem);
                _logger.LogInformation("Loaded master RSA signing key from environment.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse HEIMDALL_MASTER_PRIVATE_KEY PEM. Generated ephemeral key pair.");
            }
        }
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

        var clientPc = await _dbContext.ClientPcs.FindAsync(clientPcId);
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

        _dbContext.QueuedAgentCommands.Add(command);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Queued INSTALL_PLUGIN command for {PluginId} targeting client PC {ClientPcId} ({Hostname})",
            pluginId, clientPcId, clientPc.Hostname);

        return true;
    }

    public async Task<bool> PushPluginToMachineAsync(string pluginId, Guid machineId)
    {
        var machine = await _dbContext.Machines
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

    public Task<bool> DeletePluginAsync(string pluginId)
    {
        bool removed = _plugins.TryRemove(pluginId, out _);
        return Task.FromResult(removed);
    }
}
