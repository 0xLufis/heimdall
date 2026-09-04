using App.Shared.Data;
using App.Shared.Entities;
using App.Agent.Daemon;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class MultiTenancyAndGovernanceTests
{
    [Fact]
    public async Task MultiTenancy_WhenCurrentOrganizationIdIsSet_FiltersOutOtherTenants()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        using (var seedContext = new AppDbContext(options))
        {
            seedContext.ClientPcs.AddRange(
                new ClientPc { Id = Guid.NewGuid(), Name = "PC-PlantA-1", MacAddress = "00:11:22:33:44:01", OrganizationId = "Plant-A" },
                new ClientPc { Id = Guid.NewGuid(), Name = "PC-PlantA-2", MacAddress = "00:11:22:33:44:02", OrganizationId = "Plant-A" },
                new ClientPc { Id = Guid.NewGuid(), Name = "PC-PlantB-1", MacAddress = "00:11:22:33:44:03", OrganizationId = "Plant-B" }
            );
            await seedContext.SaveChangesAsync();
        }

        // Act - Query with Plant-A tenant filter
        using var tenantContext = new AppDbContext(options);
        tenantContext.CurrentOrganizationId = "Plant-A";
        var plantAPcs = await tenantContext.ClientPcs.ToListAsync();

        // Assert
        Assert.Equal(2, plantAPcs.Count);
        Assert.All(plantAPcs, pc => Assert.Equal("Plant-A", pc.OrganizationId));
    }

    [Fact]
    public void ConfigurationService_WhenServerPublicKeyMissingAndAllowUnsignedFalse_RejectsCommand()
    {
        // Arrange
        var config = new AgentConfig
        {
            ServerPublicKey = null,
            AllowUnsignedCommands = false
        };
        var service = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);

        // Act
        bool isValid = service.VerifySignature("TEST_PAYLOAD", "invalid_signature");

        // Assert - Fail-secure rejection
        Assert.False(isValid);
    }

    [Fact]
    public void ConfigurationService_WhenServerPublicKeyMissingAndAllowUnsignedTrue_AcceptsCommandWithWarning()
    {
        // Arrange
        var config = new AgentConfig
        {
            ServerPublicKey = null,
            AllowUnsignedCommands = true
        };
        var service = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);

        // Act
        bool isValid = service.VerifySignature("TEST_PAYLOAD", null);

        // Assert - Insecure dev override
        Assert.True(isValid);
    }

    [Fact]
    public async Task LocalTelemetrySpooler_BuffersAndDrainsPayloadsCorrectly()
    {
        // Arrange
        string testDir = Path.Combine(Path.GetTempPath(), "heimdall_test_spool_" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new AgentConfig { MaxSpoolDiskMb = 10 };
            var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
            var spooler = new App.Agent.Daemon.Infrastructure.Spooling.LocalTelemetrySpooler(
                NullLogger<App.Agent.Daemon.Infrastructure.Spooling.LocalTelemetrySpooler>.Instance,
                configService,
                testDir
            );

            // Act - Buffer 2 items
            await spooler.SpoolPayloadAsync("{\"event\": 1}");
            await spooler.SpoolPayloadAsync("{\"event\": 2}");

            var received = new List<string>();
            int drainedCount = await spooler.DrainSpoolAsync(payload =>
            {
                received.Add(payload);
                return Task.FromResult(true);
            });

            // Assert
            Assert.Equal(2, drainedCount);
            Assert.Equal(2, received.Count);
            Assert.Contains("{\"event\": 1}", received);
            Assert.Contains("{\"event\": 2}", received);

            // Second drain should be 0 because spooled files are cleared
            int secondDrain = await spooler.DrainSpoolAsync(_ => Task.FromResult(true));
            Assert.Equal(0, secondDrain);
        }
        finally
        {
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, true);
            }
        }
    }
}
