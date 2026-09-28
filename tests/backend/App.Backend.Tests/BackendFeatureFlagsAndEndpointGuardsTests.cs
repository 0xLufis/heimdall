using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Backend.Api.Controllers.V1;
using App.Contracts.Configuration;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class BackendFeatureFlagsAndEndpointGuardsTests
{
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppDbContext(_options));
    }

    private static IDbContextFactory<AppDbContext> CreateInMemoryFactory()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestDbContextFactory(options);
    }

    // ==========================================
    // 1. BackendFeatureFlags Configuration Tests
    // ==========================================

    [Fact]
    public void BackendFeatureFlags_DefaultsToFalse_InProduction()
    {
        var flags = BackendFeatureFlags.FromEnvironment("Production");
        Assert.False(flags.EnableDevFeatures);
        Assert.False(flags.EnableDebugFeatures);
    }

    [Fact]
    public void BackendFeatureFlags_EnablesDevFeatures_WhenEnvironmentIsDevelopment()
    {
        var flags = BackendFeatureFlags.FromEnvironment("Development");
        Assert.True(flags.EnableDevFeatures);
        Assert.False(flags.EnableDebugFeatures);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("false", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    public void BackendFeatureFlags_IsTruthy_ParsesCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, BackendFeatureFlags.IsTruthy(input));
    }

    [Fact]
    public void BackendFeatureFlags_FromProvider_CascadesSettingsProperly()
    {
        var dict = new Dictionary<string, string?>
        {
            ["BackendFeatureFlags:EnableDevFeatures"] = "true",
            ["BackendFeatureFlags:EnableDebugFeatures"] = "true"
        };

        var flags = BackendFeatureFlags.FromProvider(key => dict.GetValueOrDefault(key), "Production");
        Assert.True(flags.EnableDevFeatures);
        Assert.True(flags.EnableDebugFeatures);
    }

    // ==========================================
    // 2. ActiveDirectoryController Guard Tests
    // ==========================================

    [Fact]
    public async Task ActiveDirectoryController_DevEndpoints_GuardedWhenDevFeaturesDisabled()
    {
        var factory = CreateInMemoryFactory();
        var prodFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = false };
        var controller = new ActiveDirectoryController(factory, NullLogger<ActiveDirectoryController>.Instance, prodFlags);

        // 1. GetOrganizationalUnits (simulated discovery)
        var ousResult = await controller.GetOrganizationalUnits() as ObjectResult;
        Assert.NotNull(ousResult);
        Assert.Equal(StatusCodes.Status403Forbidden, ousResult.StatusCode);
        var err1 = Assert.IsType<ApiError>(ousResult.Value);
        Assert.Equal(ErrorCode.AccessDenied, err1.Code);

        // 2. PreviewImport
        var previewResult = controller.PreviewImport(new AdImportPreviewRequest()) as ObjectResult;
        Assert.NotNull(previewResult);
        Assert.Equal(StatusCodes.Status403Forbidden, previewResult.StatusCode);
        var err2 = Assert.IsType<ApiError>(previewResult.Value);
        Assert.Equal(ErrorCode.AccessDenied, err2.Code);

        // 3. TestConnection
        var testResult = controller.TestConnection() as ObjectResult;
        Assert.NotNull(testResult);
        Assert.Equal(StatusCodes.Status403Forbidden, testResult.StatusCode);
        var err3 = Assert.IsType<ApiError>(testResult.Value);
        Assert.Equal(ErrorCode.AccessDenied, err3.Code);
    }

    [Fact]
    public async Task ActiveDirectoryController_DevEndpoints_AllowedWhenDevFeaturesEnabled()
    {
        var factory = CreateInMemoryFactory();
        var devFlags = new BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = false };
        var controller = new ActiveDirectoryController(factory, NullLogger<ActiveDirectoryController>.Instance, devFlags);

        // 1. GetOrganizationalUnits returns 200 OK
        var ousResult = await controller.GetOrganizationalUnits() as OkObjectResult;
        Assert.NotNull(ousResult);
        Assert.Equal(StatusCodes.Status200OK, ousResult.StatusCode);

        // 2. PreviewImport returns 200 OK
        var previewResult = controller.PreviewImport(new AdImportPreviewRequest()) as OkObjectResult;
        Assert.NotNull(previewResult);
        Assert.Equal(StatusCodes.Status200OK, previewResult.StatusCode);

        // 3. TestConnection returns 200 OK
        var testResult = controller.TestConnection() as OkObjectResult;
        Assert.NotNull(testResult);
        Assert.Equal(StatusCodes.Status200OK, testResult.StatusCode);
    }

    // ==========================================
    // 3. AuthController Guard Tests
    // ==========================================

    [Fact]
    public void AuthController_DevEndpoints_GuardedWhenDevFeaturesDisabled()
    {
        var prodFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = false };
        var controller = new AuthController(prodFlags);

        // 1. DevLogin
        var loginRes = controller.DevLogin(new DevLoginRequest { Username = "dev@corp.local" }) as ObjectResult;
        Assert.NotNull(loginRes);
        Assert.Equal(StatusCodes.Status403Forbidden, loginRes.StatusCode);

        // 2. DevImpersonate
        var impersonateRes = controller.DevImpersonate(new DevLoginRequest { Username = "alice", Role = "admin" }) as ObjectResult;
        Assert.NotNull(impersonateRes);
        Assert.Equal(StatusCodes.Status403Forbidden, impersonateRes.StatusCode);
    }

    [Fact]
    public void AuthController_DevEndpoints_AllowedWhenDevFeaturesEnabled()
    {
        var devFlags = new BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = false };
        var controller = new AuthController(devFlags);

        // 1. DevLogin returns 200 OK
        var loginRes = controller.DevLogin(new DevLoginRequest { Username = "dev@corp.local" }) as OkObjectResult;
        Assert.NotNull(loginRes);
        Assert.Equal(StatusCodes.Status200OK, loginRes.StatusCode);

        // 2. DevImpersonate returns 200 OK
        var impersonateRes = controller.DevImpersonate(new DevLoginRequest { Username = "alice", Role = "admin" }) as OkObjectResult;
        Assert.NotNull(impersonateRes);
        Assert.Equal(StatusCodes.Status200OK, impersonateRes.StatusCode);
    }

    // ==========================================
    // 4. SystemSettingsController Guard Tests
    // ==========================================

    [Fact]
    public async Task SystemSettingsController_DevEndpoints_GuardedWhenDevFeaturesDisabled()
    {
        var factory = CreateInMemoryFactory();
        var prodFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = false };
        var controller = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance, null, prodFlags);

        // 1. DevOverrideSetting
        var overrideRes = await controller.DevOverrideSetting(new DevOverrideSettingDto
        {
            Key = "TestKey",
            Category = "TestCat",
            ValueJson = "{\"val\": 123}"
        }) as ObjectResult;
        Assert.NotNull(overrideRes);
        Assert.Equal(StatusCodes.Status403Forbidden, overrideRes.StatusCode);

        // 2. DevResetDefaults
        var resetRes = await controller.DevResetDefaults() as ObjectResult;
        Assert.NotNull(resetRes);
        Assert.Equal(StatusCodes.Status403Forbidden, resetRes.StatusCode);
    }

    [Fact]
    public async Task SystemSettingsController_DevEndpoints_AllowedWhenDevFeaturesEnabled()
    {
        var factory = CreateInMemoryFactory();
        var devFlags = new BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = false };
        var controller = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance, null, devFlags);

        // 1. DevOverrideSetting returns 200 OK
        var overrideRes = await controller.DevOverrideSetting(new DevOverrideSettingDto
        {
            Key = "TestKey",
            Category = "TestCat",
            ValueJson = "{\"val\": 123}"
        }) as OkObjectResult;
        Assert.NotNull(overrideRes);
        Assert.Equal(StatusCodes.Status200OK, overrideRes.StatusCode);

        // 2. DevResetDefaults returns 200 OK
        var resetRes = await controller.DevResetDefaults() as OkObjectResult;
        Assert.NotNull(resetRes);
        Assert.Equal(StatusCodes.Status200OK, resetRes.StatusCode);
    }

    // ==========================================
    // 5. ClientPcController Guard Tests
    // ==========================================

    [Fact]
    public async Task ClientPcController_DebugEndpoints_GuardedWhenDebugFeaturesDisabled()
    {
        var repo = new FakeControllerRepository();
        var pcId = Guid.NewGuid();
        repo.Items.Add(new ClientPc { Id = pcId, Hostname = "IPC-01", MacAddress = "00:11:22:33:44:55" });

        var snapshot = await repo.CreateDiagnosticSnapshotAsync(pcId, "user1", "User 1", null);

        var prodFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = false };
        var controller = new ClientPcController(repo, NullLogger<ClientPcController>.Instance, null, prodFlags);

        // 1. DownloadDiagnosticSnapshot
        var downloadRes = await controller.DownloadDiagnosticSnapshot(pcId, snapshot.Id) as ObjectResult;
        Assert.NotNull(downloadRes);
        Assert.Equal(StatusCodes.Status403Forbidden, downloadRes.StatusCode);

        // 2. GetRawTelemetryDump
        var rawDumpRes = await controller.GetRawTelemetryDump(pcId) as ObjectResult;
        Assert.NotNull(rawDumpRes);
        Assert.Equal(StatusCodes.Status403Forbidden, rawDumpRes.StatusCode);
    }

    [Fact]
    public async Task ClientPcController_DebugEndpoints_AllowedWhenDebugFeaturesEnabled()
    {
        var repo = new FakeControllerRepository();
        var pcId = Guid.NewGuid();
        repo.Items.Add(new ClientPc { Id = pcId, Hostname = "IPC-01", MacAddress = "00:11:22:33:44:55" });

        var snapshot = await repo.CreateDiagnosticSnapshotAsync(pcId, "user1", "User 1", null);

        var debugFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = true };
        var controller = new ClientPcController(repo, NullLogger<ClientPcController>.Instance, null, debugFlags);

        // 1. DownloadDiagnosticSnapshot returns FileContentResult
        var downloadRes = await controller.DownloadDiagnosticSnapshot(pcId, snapshot.Id) as FileContentResult;
        Assert.NotNull(downloadRes);
        Assert.Equal("application/json", downloadRes.ContentType);

        // 2. GetRawTelemetryDump returns 200 OK
        var rawDumpRes = await controller.GetRawTelemetryDump(pcId) as OkObjectResult;
        Assert.NotNull(rawDumpRes);
        Assert.Equal(StatusCodes.Status200OK, rawDumpRes.StatusCode);
    }

    // ==========================================
    // 6. DiagnosticSnapshotController Guard Tests
    // ==========================================

    [Fact]
    public async Task DiagnosticSnapshotController_GuardedWhenFlagsDisabled()
    {
        var repo = new FakeControllerRepository();
        var pcId = Guid.NewGuid();
        var snapshot = await repo.CreateDiagnosticSnapshotAsync(pcId, "user1", "User 1", null);

        var prodFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = false };
        var controller = new DiagnosticSnapshotController(repo, NullLogger<DiagnosticSnapshotController>.Instance, prodFlags);

        // 1. GetRawDump guarded behind EnableDebugFeatures
        var dumpRes = await controller.GetRawDump(snapshot.Id) as ObjectResult;
        Assert.NotNull(dumpRes);
        Assert.Equal(StatusCodes.Status403Forbidden, dumpRes.StatusCode);

        // 2. DevSeedSnapshot guarded behind EnableDevFeatures
        var seedRes = await controller.DevSeedSnapshot(pcId) as ObjectResult;
        Assert.NotNull(seedRes);
        Assert.Equal(StatusCodes.Status403Forbidden, seedRes.StatusCode);
    }

    [Fact]
    public async Task DiagnosticSnapshotController_AllowedWhenFlagsEnabled()
    {
        var repo = new FakeControllerRepository();
        var pcId = Guid.NewGuid();
        var snapshot = await repo.CreateDiagnosticSnapshotAsync(pcId, "user1", "User 1", null);

        var allFlags = new BackendFeatureFlags { EnableDevFeatures = true, EnableDebugFeatures = true };
        var controller = new DiagnosticSnapshotController(repo, NullLogger<DiagnosticSnapshotController>.Instance, allFlags);

        // 1. GetRawDump returns 200 OK
        var dumpRes = await controller.GetRawDump(snapshot.Id) as OkObjectResult;
        Assert.NotNull(dumpRes);
        Assert.Equal(StatusCodes.Status200OK, dumpRes.StatusCode);

        // 2. DevSeedSnapshot returns 200 OK
        var seedRes = await controller.DevSeedSnapshot(pcId) as OkObjectResult;
        Assert.NotNull(seedRes);
        Assert.Equal(StatusCodes.Status200OK, seedRes.StatusCode);
    }

    [Fact]
    public async Task StagingEnvironment_BlocksDevFeatures_AllowsDebugFeatures()
    {
        var repo = new FakeControllerRepository();
        var pcId = Guid.NewGuid();
        var snapshot = await repo.CreateDiagnosticSnapshotAsync(pcId, "user1", "User 1", null);

        // Staging environment feature flags: NO dev features, ONLY debug
        var stagingFlags = new BackendFeatureFlags { EnableDevFeatures = false, EnableDebugFeatures = true };
        var diagController = new DiagnosticSnapshotController(repo, NullLogger<DiagnosticSnapshotController>.Instance, stagingFlags);

        // 1. GetRawDump (debug feature) is ALLOWED in staging
        var dumpRes = await diagController.GetRawDump(snapshot.Id) as OkObjectResult;
        Assert.NotNull(dumpRes);
        Assert.Equal(StatusCodes.Status200OK, dumpRes.StatusCode);

        // 2. DevSeedSnapshot (dev feature) is FORBIDDEN in staging
        var seedRes = await diagController.DevSeedSnapshot(pcId) as ObjectResult;
        Assert.NotNull(seedRes);
        Assert.Equal(StatusCodes.Status403Forbidden, seedRes.StatusCode);

        // 3. AuthController dev endpoints are FORBIDDEN in staging
        var authController = new AuthController(stagingFlags);
        var loginRes = authController.DevLogin(new DevLoginRequest { Username = "dev@corp.local" }) as ObjectResult;
        Assert.NotNull(loginRes);
        Assert.Equal(StatusCodes.Status403Forbidden, loginRes.StatusCode);

        // 4. SystemSettingsController dev endpoints are FORBIDDEN in staging
        var factory = CreateInMemoryFactory();
        var settingsController = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance, null, stagingFlags);
        var resetRes = await settingsController.DevResetDefaults() as ObjectResult;
        Assert.NotNull(resetRes);
        Assert.Equal(StatusCodes.Status403Forbidden, resetRes.StatusCode);
    }
}
