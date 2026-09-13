using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.Extensions;
using App.Agent.Daemon.Infrastructure.Plugins;
using App.Backend.Api.Services.Plugins;
using App.Shared.Data;
using App.Shared.Errors;
using App.Shared.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class PluginSigningAndSandboxTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly string _testTempDir;

    public PluginSigningAndSandboxTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"PluginTestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _testTempDir = Path.Combine(Path.GetTempPath(), "heimdall_plugin_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        if (Directory.Exists(_testTempDir))
        {
            try { Directory.Delete(_testTempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task PluginService_SignsPackage_AndVerifiesWithPublicKey()
    {
        var pluginService = new PluginService(_dbContext, NullLogger<PluginService>.Instance);
        string publicKeyPem = pluginService.GetMasterPublicKeyPem();
        Assert.False(string.IsNullOrWhiteSpace(publicKeyPem));
        Assert.Contains("BEGIN RSA PUBLIC KEY", publicKeyPem);

        var manifest = new PluginManifest
        {
            PluginId = "telemetry-sensor",
            Name = "Telemetry Sensor",
            Version = "1.0.0",
            Description = "Monitors custom telemetry",
            Entrypoint = "main.py",
            RuntimeType = "Python",
            IsSigned = false,
            Signature = string.Empty,
            PayloadHash = string.Empty
        };

        var files = new Dictionary<string, string>
        {
            ["main.py"] = "print('Hello from secure sensor!')",
            ["config.json"] = "{\"rate\": 100}"
        };

        var signedManifest = await pluginService.RegisterAndSignPluginAsync(manifest, files, sign: true);

        Assert.True(signedManifest.IsSigned);
        Assert.False(string.IsNullOrEmpty(signedManifest.Signature));
        Assert.False(string.IsNullOrEmpty(signedManifest.PayloadHash));

        // Verify cryptographic signature with exported public key
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        byte[] hashBytes = Encoding.UTF8.GetBytes(signedManifest.PayloadHash);
        byte[] sigBytes = Convert.FromBase64String(signedManifest.Signature);

        bool isValid = rsa.VerifyData(hashBytes, sigBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(isValid);
    }

    [Fact]
    public async Task PluginManager_AcceptsSignedPlugin_InProductionMode()
    {
        var pluginService = new PluginService(_dbContext, NullLogger<PluginService>.Instance);
        string publicKeyPem = pluginService.GetMasterPublicKeyPem();

        var manifest = new PluginManifest
        {
            PluginId = "vibration-probe",
            Name = "Vibration Probe",
            Version = "2.1.0",
            Description = "High-frequency vibration monitor",
            Entrypoint = "probe.py",
            RuntimeType = "Python",
            IsSigned = false,
            Signature = string.Empty,
            PayloadHash = string.Empty
        };

        var files = new Dictionary<string, string>
        {
            ["probe.py"] = "print('Analyzing vibration harmonics...')"
        };

        var signedManifest = await pluginService.RegisterAndSignPluginAsync(manifest, files, sign: true);
        var bundle = new PluginPackageBundle
        {
            Manifest = signedManifest,
            Files = files
        };

        var config = new AgentConfig
        {
            Environment = "Production",
            AllowUnsignedPlugins = false,
            ServerPublicKey = publicKeyPem,
            SandboxesDirectory = _testTempDir
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var sandboxService = new PluginSandboxService(configService, NullLogger<PluginSandboxService>.Instance);
        var extRegistry = new ExtensionRegistry(configService, NullLogger<ExtensionRegistry>.Instance);
        var pluginManager = new PluginManager(configService, sandboxService, extRegistry, NullLogger<PluginManager>.Instance);

        var result = await pluginManager.InstallPluginAsync(bundle);

        Assert.True(result.Success);
        Assert.False(result.IsSandboxed);
        Assert.Equal(ErrorCode.None, result.ErrorCode);

        var installed = pluginManager.GetInstalledPlugins();
        Assert.Single(installed);
        Assert.True(installed[0].IsSigned);
    }

    [Fact]
    public async Task PluginManager_RejectsUnsignedOrTamperedPlugin_InProductionMode()
    {
        var pluginService = new PluginService(_dbContext, NullLogger<PluginService>.Instance);
        string publicKeyPem = pluginService.GetMasterPublicKeyPem();

        var manifest = new PluginManifest
        {
            PluginId = "tampered-plugin",
            Name = "Tampered Plugin",
            Version = "1.0.0",
            Description = "Payload modified after signing",
            Entrypoint = "run.py",
            RuntimeType = "Python",
            IsSigned = false,
            Signature = string.Empty,
            PayloadHash = string.Empty
        };

        var files = new Dictionary<string, string>
        {
            ["run.py"] = "print('Original safe script')"
        };

        var signedManifest = await pluginService.RegisterAndSignPluginAsync(manifest, files, sign: true);

        // Tamper with the files dictionary
        var tamperedFiles = new Dictionary<string, string>
        {
            ["run.py"] = "print('Malicious injected code!')"
        };
        var tamperedBundle = new PluginPackageBundle
        {
            Manifest = signedManifest,
            Files = tamperedFiles
        };

        var config = new AgentConfig
        {
            Environment = "Production",
            AllowUnsignedPlugins = false,
            ServerPublicKey = publicKeyPem,
            SandboxesDirectory = _testTempDir
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var sandboxService = new PluginSandboxService(configService, NullLogger<PluginSandboxService>.Instance);
        var extRegistry = new ExtensionRegistry(configService, NullLogger<ExtensionRegistry>.Instance);
        var pluginManager = new PluginManager(configService, sandboxService, extRegistry, NullLogger<PluginManager>.Instance);

        var result = await pluginManager.InstallPluginAsync(tamperedBundle);

        // Tampered payload hash doesn't match original signed content hash
        // In Production, failure to verify must result in rejection
        Assert.False(result.Success);
        Assert.Equal(ErrorCode.PluginSignatureInvalid, result.ErrorCode);
    }

    [Fact]
    public async Task PluginManager_PermitsUnsignedPlugin_InDevelopmentSandbox()
    {
        var manifest = new PluginManifest
        {
            PluginId = "dev-sensor",
            Name = "Dev Sensor",
            Version = "0.1.0-alpha",
            Description = "Unsigned development plugin",
            Entrypoint = "sensor.py",
            RuntimeType = "Python",
            IsSigned = false,
            Signature = string.Empty,
            PayloadHash = string.Empty
        };

        var files = new Dictionary<string, string>
        {
            ["sensor.py"] = "print('Simulating sensor reading...')"
        };
        var bundle = new PluginPackageBundle
        {
            Manifest = manifest,
            Files = files
        };

        var config = new AgentConfig
        {
            Environment = "Development",
            AllowUnsignedPlugins = true,
            ServerPublicKey = string.Empty,
            SandboxesDirectory = _testTempDir
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var sandboxService = new PluginSandboxService(configService, NullLogger<PluginSandboxService>.Instance);
        var extRegistry = new ExtensionRegistry(configService, NullLogger<ExtensionRegistry>.Instance);
        var pluginManager = new PluginManager(configService, sandboxService, extRegistry, NullLogger<PluginManager>.Instance);

        var result = await pluginManager.InstallPluginAsync(bundle);

        Assert.True(result.Success);
        Assert.True(result.IsSandboxed);
        Assert.Equal(ErrorCode.None, result.ErrorCode);

        var installed = pluginManager.GetInstalledPlugins();
        Assert.Single(installed);
        Assert.False(installed[0].IsSigned);
    }

    [Fact]
    public async Task PluginSandboxService_RejectsPathTraversal_InPluginBundle()
    {
        var manifest = new PluginManifest
        {
            PluginId = "traversal-exploit",
            Name = "Traversal Exploit",
            Version = "1.0.0",
            Description = "Escapes sandbox via directory traversal",
            Entrypoint = "main.py",
            RuntimeType = "Python",
            IsSigned = true, // Mark signed so it passes the prod check to test path containment specifically
            Signature = "fake-sig",
            PayloadHash = "fake-hash"
        };

        var maliciousFiles = new Dictionary<string, string>
        {
            ["../../escaped.txt"] = "malicious payload",
            ["main.py"] = "print('Attempting traversal')"
        };
        var bundle = new PluginPackageBundle
        {
            Manifest = manifest,
            Files = maliciousFiles
        };

        var config = new AgentConfig
        {
            Environment = "Development",
            AllowUnsignedPlugins = true,
            SandboxesDirectory = _testTempDir
        };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var sandboxService = new PluginSandboxService(configService, NullLogger<PluginSandboxService>.Instance);

        var execResult = await sandboxService.ExecuteSandboxedAsync(bundle);

        Assert.False(execResult.Success);
        Assert.Equal(ErrorCode.SandboxViolation, execResult.ErrorCode);
        Assert.Contains("escapes sandbox root", execResult.StandardError);
    }
}
