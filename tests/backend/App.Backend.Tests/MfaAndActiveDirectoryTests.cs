using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using App.Backend.Api.Controllers.V1;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class MfaAndActiveDirectoryTests
{
    private class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public TestDbContextFactory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppDbContext(_options));
    }

    [Fact]
    public async Task MfaEvaluation_SysAdminAlwaysRequiresMfa()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance);

        var request = new MfaEvaluationRequestDto
        {
            Role = "SystemAdministrator",
            LastMfaAt = DateTimeOffset.UtcNow.AddMinutes(-5) // 5 minutes ago
        };

        var result = await controller.EvaluateMfa(request) as OkObjectResult;
        Assert.NotNull(result);
        var eval = result.Value as MfaEvaluationResultDto;
        Assert.NotNull(eval);
        Assert.True(eval.MfaRequired);
        Assert.True(eval.IsExpired);
        Assert.Equal("always", eval.AppliedThreshold);
    }

    [Fact]
    public async Task MfaEvaluation_EngineerWeeklyThreshold_ValidWithin7Days_ExpiredAfter7Days()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance);

        // Within 7 days (e.g. 3 days ago) -> valid, not required
        var validReq = new MfaEvaluationRequestDto
        {
            Role = "Engineer",
            LastMfaAt = DateTimeOffset.UtcNow.AddDays(-3)
        };
        var validRes = (await controller.EvaluateMfa(validReq) as OkObjectResult)?.Value as MfaEvaluationResultDto;
        Assert.NotNull(validRes);
        Assert.False(validRes.MfaRequired);
        Assert.False(validRes.IsExpired);
        Assert.Equal("7d", validRes.AppliedThreshold);

        // After 7 days (e.g. 8 days ago) -> expired, MFA required
        var expiredReq = new MfaEvaluationRequestDto
        {
            Role = "Engineer",
            LastMfaAt = DateTimeOffset.UtcNow.AddDays(-8)
        };
        var expiredRes = (await controller.EvaluateMfa(expiredReq) as OkObjectResult)?.Value as MfaEvaluationResultDto;
        Assert.NotNull(expiredRes);
        Assert.True(expiredRes.MfaRequired);
        Assert.True(expiredRes.IsExpired);
    }

    [Fact]
    public async Task MfaEvaluation_TechnicianMonthlyThreshold_ValidWithin30Days_ExpiredAfter30Days()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance);

        // Within 30 days (e.g. 15 days ago) -> valid
        var validReq = new MfaEvaluationRequestDto
        {
            Role = "Technician",
            LastMfaAt = DateTimeOffset.UtcNow.AddDays(-15)
        };
        var validRes = (await controller.EvaluateMfa(validReq) as OkObjectResult)?.Value as MfaEvaluationResultDto;
        Assert.NotNull(validRes);
        Assert.False(validRes.MfaRequired);
        Assert.Equal("30d", validRes.AppliedThreshold);

        // After 30 days (e.g. 35 days ago) -> expired
        var expiredReq = new MfaEvaluationRequestDto
        {
            Role = "Technician",
            LastMfaAt = DateTimeOffset.UtcNow.AddDays(-35)
        };
        var expiredRes = (await controller.EvaluateMfa(expiredReq) as OkObjectResult)?.Value as MfaEvaluationResultDto;
        Assert.NotNull(expiredRes);
        Assert.True(expiredRes.MfaRequired);
        Assert.True(expiredRes.IsExpired);
    }

    [Fact]
    public async Task MfaEvaluation_AnyCustomSecurityGroupRule_CanBeConfiguredAndEvaluated()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new SystemSettingsController(factory, NullLogger<SystemSettingsController>.Instance);

        // Custom policy with QA group rule set to 24h
        var customPolicy = new MfaPolicyDto
        {
            Enabled = true,
            DefaultThreshold = "7d",
            Rules = new List<MfaGroupRuleDto>
            {
                new()
                {
                    Id = "rule-qa",
                    TargetType = "group",
                    TargetName = "Quality Assurance",
                    ForceMfa = true,
                    TimeoutThreshold = "24h"
                }
            }
        };

        await controller.UpdateMfaPolicy(customPolicy);

        var evalReq = new MfaEvaluationRequestDto
        {
            Role = "Viewer",
            Groups = new List<string> { "Quality Assurance" },
            LastMfaAt = DateTimeOffset.UtcNow.AddHours(-30) // 30h ago > 24h
        };

        var evalRes = (await controller.EvaluateMfa(evalReq) as OkObjectResult)?.Value as MfaEvaluationResultDto;
        Assert.NotNull(evalRes);
        Assert.True(evalRes.MfaRequired);
        Assert.True(evalRes.IsExpired);
        Assert.Equal("24h", evalRes.AppliedThreshold);
        Assert.Equal("Quality Assurance", evalRes.MatchedRuleTarget);
    }

    [Fact]
    public async Task ActiveDirectory_DiscoveryAndTemplatingPreview_ExtractsVlanAndTags()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new ActiveDirectoryController(factory, NullLogger<ActiveDirectoryController>.Instance);

        var ousRes = await controller.GetOrganizationalUnits() as OkObjectResult;
        Assert.NotNull(ousRes);
        var ous = ousRes.Value as List<AdOrganizationalUnit>;
        Assert.NotNull(ous);
        Assert.True(ous.Count >= 4);

        // Test preview with custom tag templates
        var previewReq = new AdImportPreviewRequest
        {
            SelectedOuPaths = new List<string> { "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp" },
            TagTemplates = new Dictionary<string, string>
            {
                { "location", "{LOCATION}" },
                { "purpose", "{PURPOSE}" },
                { "vlan_id", "{VLAN_ID}" }
            }
        };

        var previewRes = controller.PreviewImport(previewReq) as OkObjectResult;
        Assert.NotNull(previewRes);

        // Import hosts
        var importReq = new AdHostImportRequest
        {
            Hosts = new List<AdHostPreviewItem>
            {
                new()
                {
                    Hostname = "CPC-L06-ROB-01",
                    Name = "Kuka Robot Controller 01",
                    IpAddress = "10.10.10.11",
                    MacAddress = "00:1A:2B:3C:4D:11",
                    VlanId = 10,
                    VlanName = "VLAN 10 - Production Line",
                    Subnet = "10.10.10.0/24",
                    AdOuPath = "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp",
                    OuTags = new Dictionary<string, string> { { "location", "Line 06 - Hall A" } }
                }
            }
        };

        var importRes = await controller.ImportHosts(importReq) as OkObjectResult;
        Assert.NotNull(importRes);

        // Verify persisted in DB
        using var verifyDb = factory.CreateDbContext();
        var savedPc = await verifyDb.ClientPcs.FirstOrDefaultAsync(p => p.Hostname == "CPC-L06-ROB-01");
        Assert.NotNull(savedPc);
        Assert.Equal(10, savedPc.VlanId);
        Assert.Equal("10.10.10.0/24", savedPc.Subnet);
        Assert.Equal("OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp", savedPc.AdOuPath);
    }

    [Fact]
    public void ActiveDirectory_TemplatingPreview_ResolvesDynamicKeyAndValueTokens()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new ActiveDirectoryController(factory, NullLogger<ActiveDirectoryController>.Instance);

        var previewReq = new AdImportPreviewRequest
        {
            SelectedOuPaths = new List<string> { "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp" },
            TagRules = new List<TagTemplateRule>
            {
                new() { KeyTemplate = "zone.{LOCATION}", ValueTemplate = "{MACHINE_TYPE}" },
                new() { KeyTemplate = "net.vlan_{VLAN_ID}", ValueTemplate = "{SUBNET}" },
                new() { KeyTemplate = "workstation.role", ValueTemplate = "{PURPOSE}" },
                new() { KeyTemplate = "fqdn", ValueTemplate = "{HOSTNAME}.factory.corp" },
            }
        };

        var previewRes = controller.PreviewImport(previewReq) as OkObjectResult;
        Assert.NotNull(previewRes);

        var val = previewRes.Value as AdImportPreviewResponse;
        Assert.NotNull(val);
        var previewList = val.Preview;
        Assert.NotEmpty(previewList);

        var firstHost = previewList.First();
        Assert.True(firstHost.OuTags.ContainsKey("zone.Line 06 - Hall A"));
        Assert.Equal("Manipulator", firstHost.OuTags["zone.Line 06 - Hall A"]);
        Assert.True(firstHost.OuTags.ContainsKey("net.vlan_10"));
        Assert.Equal("10.10.10.0/24", firstHost.OuTags["net.vlan_10"]);
        Assert.True(firstHost.OuTags.ContainsKey("workstation.role"));
        Assert.Equal("Robotic Pick & Place / Handling", firstHost.OuTags["workstation.role"]);
        Assert.True(firstHost.OuTags.ContainsKey("fqdn"));
        Assert.Equal($"{firstHost.Hostname}.factory.corp", firstHost.OuTags["fqdn"]);
    }

    [Fact]
    public async Task CertificateManagement_RootCaImportAndOuRuleAutoEnrollment()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var factory = new TestDbContextFactory(options);
        var controller = new CertificateManagementController(factory, NullLogger<CertificateManagementController>.Instance);

        // 1. Generate an X.509 cert to import as Root CA
        using var rsa = RSA.Create(2048);
        var certReq = new CertificateRequest("CN=Factory Corporate Root CA, O=Industrial Corp, C=US", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        certReq.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        var testCert = certReq.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(5));
        var pem = testCert.ExportCertificatePem();

        var importResult = await controller.ImportRootCertificate(new ImportRootCertRequest
        {
            RawPem = pem,
            ProfileName = "Corporate-Master-Root-CA"
        }) as OkObjectResult;

        Assert.NotNull(importResult);
        var rootRecord = importResult.Value as ClientCertificateRecord;
        Assert.NotNull(rootRecord);
        Assert.True(rootRecord.IsRootCa);
        Assert.Equal(testCert.Thumbprint, rootRecord.Thumbprint);

        // 2. Query active root CA
        var getRootRes = await controller.GetRootCa() as OkObjectResult;
        Assert.NotNull(getRootRes);
        var activeRoot = getRootRes.Value as ClientCertificateRecord;
        Assert.NotNull(activeRoot);
        Assert.Equal(testCert.Thumbprint, activeRoot.Thumbprint);

        // 3. Ensure OU rules are seeded and add client PC
        await controller.GetOuRules();

        using (var db = factory.CreateDbContext())
        {
            db.ClientPcs.Add(new ClientPc
            {
                Id = Guid.NewGuid(),
                Name = "Robot Controller IPC",
                Hostname = "CPC-L06-ROB-01",
                MacAddress = "00:AA:BB:CC:DD:01",
                AdOuPath = "OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp"
            });
            await db.SaveChangesAsync();
        }

        var syncRes = await controller.SyncOuCertificates() as OkObjectResult;
        Assert.NotNull(syncRes);

        using (var verifyDb = factory.CreateDbContext())
        {
            var pc = await verifyDb.ClientPcs.FirstOrDefaultAsync(p => p.Hostname == "CPC-L06-ROB-01");
            Assert.NotNull(pc);
            Assert.False(string.IsNullOrEmpty(pc.CertificateThumbprint));
            Assert.Equal("High-Assurance-Robotics-mTLS", pc.CertificateProfileName);
        }
    }
}
