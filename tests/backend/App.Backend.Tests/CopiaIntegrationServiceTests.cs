using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using App.Backend.Api.Services;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class CopiaIntegrationServiceTests
{
    private readonly CopiaIntegrationService _service;
    private readonly AppDbContext _context;

    public CopiaIntegrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        var factory = new TestDbContextFactory(options);

        _service = new CopiaIntegrationService(
            NullLogger<CopiaIntegrationService>.Instance,
            factory,
            null
        );
    }

    [Fact]
    public void ShouldProcessPlcEvent_EnforcesDebounceWindow()
    {
        var amsNetId = "192.168.1.10.1.1";
        var eventType = "download";
        var window = TimeSpan.FromSeconds(5);

        // First event should always be processed
        bool first = _service.ShouldProcessPlcEvent(amsNetId, eventType, window);
        Assert.True(first);

        // Immediate subsequent event within window should be debounced
        bool immediate = _service.ShouldProcessPlcEvent(amsNetId, eventType, window);
        Assert.False(immediate);

        // Event for a different AMS Net ID should be processed
        bool differentPlc = _service.ShouldProcessPlcEvent("192.168.1.20.1.1", eventType, window);
        Assert.True(differentPlc);

        // Event for a different event type on same AMS Net ID should be processed
        bool differentEvent = _service.ShouldProcessPlcEvent(amsNetId, "login", window);
        Assert.True(differentEvent);
    }

    [Fact]
    public void VerifyWebhookSignature_ValidatesCorrectHmacSha256()
    {
        var secret = "IndustrialAutomationSecret123!";
        var payload = "{\"event\":\"push\",\"repository\":{\"name\":\"PlcCell01\"}}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var validSignature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        Assert.True(_service.VerifyWebhookSignature(payload, validSignature, secret));
        Assert.False(_service.VerifyWebhookSignature(payload, "sha256=invalid_hash", secret));
        Assert.False(_service.VerifyWebhookSignature(payload, validSignature, "WrongSecret"));
    }

    [Fact]
    public async Task TrackLocalPlcChangeAsync_UsesHeimdallProbeServiceUser_AndQueuesPendingNotification()
    {
        var deviceId = "PLC-LINE04-ROBOT01";
        var repoPath = "repos/plc-line04-robot01";
        var modifiedFiles = new[] { "POUs/MAIN.TcPOU", "POUs/FB_Gripper.TcPOU" };

        var result = await _service.TrackLocalPlcChangeAsync(
            deviceId,
            repoPath,
            "main",
            modifiedFiles,
            "ADS download captured locally"
        );

        Assert.True(result.Success);
        Assert.StartsWith("sync-", result.SyncId);
        Assert.StartsWith("loc-", result.LocalCommitHash);
        Assert.Equal("heimdall-probe <heimdall-probe@internal>", result.ServiceUserAuthor);
        Assert.Contains("heimdall-probe", result.NotificationMessage);
        Assert.Contains("EULA Compliance Notice", result.NotificationMessage);

        var pendingSyncs = _service.GetPendingCloudSyncs();
        Assert.Contains(pendingSyncs, s => s.SyncId == result.SyncId);

        var record = _service.GetPendingCloudSyncById(result.SyncId);
        Assert.NotNull(record);
        Assert.Equal(CloudSyncStatus.PendingUserAuthorization, record.Status);
    }

    [Fact]
    public async Task SyncLocalRepoToCopiaCloudAsync_ThrowsEulaComplianceViolation_WhenApiKeyMissingOrServiceUser()
    {
        // Setup local tracking first
        var tracking = await _service.TrackLocalPlcChangeAsync(
            "PLC-LINE02-WELD01",
            "repos/weld01",
            "main",
            new[] { "POUs/MAIN.TcPOU" }
        );

        // 1. Missing / whitespace API key -> must throw EULA compliance error
        var exEmpty = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SyncLocalRepoToCopiaCloudAsync(tracking.SyncId, "", "engineer@factory.com"));
        Assert.Contains("EULA Compliance Violation", exEmpty.Message);

        // 2. Generic service user key -> must throw EULA compliance error
        var exService = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SyncLocalRepoToCopiaCloudAsync(tracking.SyncId, "service-user", "engineer@factory.com"));
        Assert.Contains("Generic service account keys or shared keys cannot be used", exService.Message);

        var exProbe = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SyncLocalRepoToCopiaCloudAsync(tracking.SyncId, "heimdall-probe", "engineer@factory.com"));
        Assert.Contains("Generic service account keys or shared keys cannot be used", exProbe.Message);
    }

    [Fact]
    public async Task SyncLocalRepoToCopiaCloudAsync_SucceedsWithUserSpecificApiKey_AndUpdatesSoftwareAsset()
    {
        // Add existing SoftwareAsset to in-memory DB
        var asset = new SoftwareAsset
        {
            Id = Guid.NewGuid(),
            Name = "PLC-LINE03-DISP01",
            Version = "initial-v1.0"
        };
        _context.SoftwareAssets.Add(asset);
        await _context.SaveChangesAsync();

        // 1. Track locally with heimdall-probe
        var tracking = await _service.TrackLocalPlcChangeAsync(
            asset.Name,
            "repos/disp01",
            "main",
            new[] { "POUs/MAIN.TcPOU", "DUTs/ST_Dispenser.TcDUT" }
        );

        // 2. User authorizes cloud sync with their personal API key
        var userPersonalKey = "copia_pat_usr_998877665544332211";
        var userEmail = "controls.engineer@auto-plant.com";
        var userDisplayName = "Elena Rostova";

        var syncResult = await _service.SyncLocalRepoToCopiaCloudAsync(
            tracking.SyncId,
            userPersonalKey,
            userEmail,
            userDisplayName
        );

        Assert.True(syncResult.Success);
        Assert.Equal(tracking.SyncId, syncResult.SyncId);
        Assert.Equal(tracking.LocalCommitHash, syncResult.LocalCommitHash);
        Assert.StartsWith("copia-", syncResult.CloudCommitHash);
        Assert.Equal(userEmail, syncResult.UserEmail);

        // Verify pending sync status transitioned to Completed
        var updatedRecord = _service.GetPendingCloudSyncById(tracking.SyncId);
        Assert.NotNull(updatedRecord);
        Assert.Equal(CloudSyncStatus.Completed, updatedRecord.Status);
        Assert.Equal(userEmail, updatedRecord.SyncedByUserEmail);
        Assert.NotNull(updatedRecord.SyncedAt);

        // Verify SoftwareAsset version updated in DB to cloud commit
        await _context.Entry(asset).ReloadAsync();
        Assert.Equal(syncResult.CloudCommitHash, asset.Version);
    }

    [Fact]
    public async Task TriggerDeviceLinkBackupAsync_WithoutUserKey_FallsBackToLocalTrackingWithoutCloudEulaViolation()
    {
        var deviceId = "PLC-LINE01-CELL02";
        var reason = "ADS Online Change Detected";

        // Without user API key -> tracks locally via heimdall-probe and queues notification
        bool result = await _service.TriggerDeviceLinkBackupAsync(deviceId, reason);
        Assert.True(result);

        var pending = _service.GetPendingCloudSyncs();
        Assert.Contains(pending, p => p.DeviceId == deviceId && p.ServiceUserAuthor.Contains("heimdall-probe"));
    }

    [Fact]
    public async Task ExtractAndRebasePlcConfigAsync_ExtractsArchiveAndFiltersVolatiles_AndRebasesOntoControllerBranch()
    {
        var deviceId = "CX5130-LINE03";

        // Create a synthetic TwinCAT CurrentConfig.tnzip archive in memory
        using var memStream = new MemoryStream();
        using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Valid project files
            var slnEntry = archive.CreateEntry("MyPlcSolution.sln");
            using (var w = new StreamWriter(slnEntry.Open())) w.WriteLine("Microsoft Visual Studio Solution File");

            var projEntry = archive.CreateEntry("MyTwinCatProject/MyTwinCatProject.tsproj");
            using (var w = new StreamWriter(projEntry.Open())) w.WriteLine("<TcSmProject>");

            var pouEntry = archive.CreateEntry("MyTwinCatProject/POUs/MAIN.TcPOU");
            using (var w = new StreamWriter(pouEntry.Open())) w.WriteLine("<TcPlcObject><POU Name=\"MAIN\"/></TcPlcObject>");

            var dutEntry = archive.CreateEntry("MyTwinCatProject/DUTs/ST_Motor.TcDUT");
            using (var w = new StreamWriter(dutEntry.Open())) w.WriteLine("<TcPlcObject><DUT Name=\"ST_Motor\"/></TcPlcObject>");

            // Volatile files that MUST be ignored
            var bootEntry = archive.CreateEntry("_Boot/TwinCAT_Runtime.bootdata");
            using (var w = new StreamWriter(bootEntry.Open())) w.Write("binary volatile data");

            var compileEntry = archive.CreateEntry("_CompileInfo/symbols.compileinfo");
            using (var w = new StreamWriter(compileEntry.Open())) w.Write("compiler temp info");

            var suoEntry = archive.CreateEntry(".vs/MySolution/v17/.suo");
            using (var w = new StreamWriter(suoEntry.Open())) w.Write("user visual studio options");
        }

        memStream.Position = 0;

        var result = await _service.ExtractAndRebasePlcConfigAsync(deviceId, memStream);

        Assert.True(result.Success);
        Assert.Equal(deviceId, result.DeviceId);
        Assert.Equal("controller/cx5130-line03", result.TargetBranch);
        Assert.StartsWith("loc-", result.LocalCommitHash);
        Assert.StartsWith("sync-", result.SyncId);

        // Verify valid files are in ExtractedFiles
        Assert.Contains("MyPlcSolution.sln", result.ExtractedFiles);
        Assert.Contains("MyTwinCatProject/MyTwinCatProject.tsproj", result.ExtractedFiles);
        Assert.Contains("MyTwinCatProject/POUs/MAIN.TcPOU", result.ExtractedFiles);
        Assert.Contains("MyTwinCatProject/DUTs/ST_Motor.TcDUT", result.ExtractedFiles);
        Assert.Contains(".gitignore", result.ExtractedFiles);
        Assert.Contains(".gitattributes", result.ExtractedFiles);

        // Verify volatile files are filtered into IgnoredVolatileFiles
        Assert.Contains("_Boot/TwinCAT_Runtime.bootdata", result.IgnoredVolatileFiles);
        Assert.Contains("_CompileInfo/symbols.compileinfo", result.IgnoredVolatileFiles);
        Assert.Contains(".vs/MySolution/v17/.suo", result.IgnoredVolatileFiles);

        // Verify notification message
        Assert.Contains("TwinCAT CurrentConfig.tnzip pulled from PLC", result.NotificationMessage);
        Assert.Contains("Copia Cloud", result.NotificationMessage);

        // Verify pending cloud sync was registered with heimdall-probe
        var pending = _service.GetPendingCloudSyncById(result.SyncId);
        Assert.NotNull(pending);
        Assert.Equal(CloudSyncStatus.PendingUserAuthorization, pending.Status);
        Assert.Equal("controller/cx5130-line03", pending.Branch);
        Assert.Contains("heimdall-probe", pending.ServiceUserAuthor);
    }
}
