using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App.Agent.Daemon;
using App.Agent.Daemon.CommandHandling;
using App.Agent.Daemon.Interfaces;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using App.Shared.Errors;
using App.Shared.Protos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class DiagnosticSnapshotTests : IDisposable
{
    private readonly AppDbContext _dbContext;
    private readonly ControllerRepository _repository;

    public DiagnosticSnapshotTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"DiagnosticSnapshotDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);
        _repository = new ControllerRepository(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateDiagnosticSnapshotAsync_PersistsRecord_AndComputesValidSha256()
    {
        var clientPcId = Guid.NewGuid();
        var clientPc = new ClientPc
        {
            Id = clientPcId,
            Name = "IPC-LINE1-OP20",
            Hostname = "IPC-LINE1-OP20",
            MacAddress = "00:11:22:33:44:55",
            MachineIdentifier = "MID-8849-XYZ",
            LastOnline = DateTimeOffset.UtcNow,
            ResourceAverages = new ResourceAverages
            {
                CpuUsageAverage = 18.5,
                RamUsageAverage = 42.1
            },
            FreeDiskSpace = new DiskSpaceInfo
            {
                TotalFreeGB = 185.4,
                OsDriveFreeGB = 45.2,
                Drives = new Dictionary<string, double> { ["C:"] = 45.2, ["D:"] = 140.2 }
            },
            InventoryItems = new List<BaseInventoryItem>
            {
                new PcHardware
                {
                    Id = Guid.NewGuid(),
                    Name = "Intel Core i7-11700",
                    DisplayName = "Core i7 CPU",
                    Technology = "x86_64"
                },
                new PcHardware
                {
                    Id = Guid.NewGuid(),
                    Name = "CX2040-Beckhoff-PLC",
                    DisplayName = "Embedded PLC",
                    Technology = "TwinCAT-3"
                }
            }
        };

        _dbContext.ClientPcs.Add(clientPc);
        await _dbContext.SaveChangesAsync();

        var snapshot = await _repository.CreateDiagnosticSnapshotAsync(
            clientPcId,
            userId: "usr-admin-42",
            userName: "Alice Engineer",
            orgId: "org-plant-munich");

        Assert.NotNull(snapshot);
        Assert.Equal(clientPcId, snapshot.ClientPcId);
        Assert.Equal("IPC-LINE1-OP20", snapshot.Hostname);
        Assert.Equal("usr-admin-42", snapshot.CapturedByUserId);
        Assert.Equal("Alice Engineer", snapshot.CapturedByUserName);
        Assert.Equal("org-plant-munich", snapshot.OrganizationId);

        // Verify SHA-256 integrity
        Assert.False(string.IsNullOrEmpty(snapshot.PayloadHashSha256));
        Assert.Equal(64, snapshot.PayloadHashSha256.Length);

        byte[] expectedHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(snapshot.SnapshotPayloadJson));
        string expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();
        Assert.Equal(expectedHash, snapshot.PayloadHashSha256);

        // Verify database persistence
        var persisted = await _dbContext.DiagnosticSnapshots.FindAsync(snapshot.Id);
        Assert.NotNull(persisted);
        Assert.Equal(snapshot.PayloadHashSha256, persisted.PayloadHashSha256);
        Assert.Contains("CX2040-Beckhoff-PLC", persisted.SnapshotPayloadJson);
        Assert.Contains("18.5", persisted.SnapshotPayloadJson);
    }

    [Fact]
    public async Task CreateDiagnosticSnapshotAsync_RecordsAuditLog_AndQueuesAgentCommand()
    {
        var clientPcId = Guid.NewGuid();
        var clientPc = new ClientPc
        {
            Id = clientPcId,
            Name = "TEST-NODE-01",
            Hostname = "TEST-NODE-01",
            MacAddress = "AA:BB:CC:DD:EE:FF"
        };
        _dbContext.ClientPcs.Add(clientPc);
        await _dbContext.SaveChangesAsync();

        var snapshot = await _repository.CreateDiagnosticSnapshotAsync(
            clientPcId,
            userId: "usr-secops-01",
            userName: "Bob Security",
            orgId: "org-defense");

        // 1. AuditLog
        var audit = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "DIAGNOSTIC_SNAPSHOT" && a.EntityId == clientPcId.ToString());
        Assert.NotNull(audit);
        Assert.Equal("usr-secops-01", audit.UserId);
        Assert.Equal("Bob Security", audit.UserName);
        Assert.Contains(snapshot.Id.ToString(), audit.NewValuesJson);

        // 2. AgentEvent
        var agentEvent = await _dbContext.AgentEvents
            .FirstOrDefaultAsync(e => e.ClientPcId == clientPcId && e.Source == "DiagnosticSnapshot");
        Assert.NotNull(agentEvent);
        Assert.Equal("Information", agentEvent.Level);
        Assert.Contains(snapshot.Id.ToString(), agentEvent.Message);

        // 3. QueuedAgentCommand
        var command = await _dbContext.QueuedAgentCommands
            .FirstOrDefaultAsync(c => c.ClientPcId == clientPcId && c.Type == "TRIGGER_DIAGNOSTIC_SNAPSHOT");
        Assert.NotNull(command);
        Assert.Contains(snapshot.Id.ToString(), command.Payload);
    }

    [Fact]
    public async Task GetSnapshotsByClientPcIdAsync_ReturnsChronologicallyOrderedHistory()
    {
        var clientPcId = Guid.NewGuid();
        var clientPc = new ClientPc
        {
            Id = clientPcId,
            Name = "HISTORY-NODE",
            Hostname = "HISTORY-NODE",
            MacAddress = "11:22:33:44:55:66"
        };
        _dbContext.ClientPcs.Add(clientPc);
        await _dbContext.SaveChangesAsync();

        // Create two snapshots
        var s1 = await _repository.CreateDiagnosticSnapshotAsync(clientPcId, "user1", "User 1", null);
        await Task.Delay(10);
        var s2 = await _repository.CreateDiagnosticSnapshotAsync(clientPcId, "user2", "User 2", null);

        var list = await _repository.GetSnapshotsByClientPcIdAsync(clientPcId);
        Assert.Equal(2, list.Count);
        // Ordered descending by CapturedAtUtc
        Assert.Equal(s2.Id, list[0].Id);
        Assert.Equal(s1.Id, list[1].Id);

        var retrieved = await _repository.GetSnapshotByIdAsync(s1.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(s1.Id, retrieved.Id);
    }

    [Fact]
    public async Task CommandHandler_HandlesTriggerDiagnosticSnapshotCommand()
    {
        var config = new AgentConfig { AllowUnsignedCommands = true };
        var configService = new ConfigurationService(NullLogger<ConfigurationService>.Instance, config);
        var fileScanner = new App.Agent.Daemon.Infrastructure.FileSystem.FileSystemScanner();

        var fakeReporter = new MockSystemInfoReporter();
        var handler = new CommandHandler(configService, fileScanner, NullLogger<CommandHandler>.Instance, reporter: fakeReporter);

        var serverCommand = new ServerCommand
        {
            Type = "TRIGGER_DIAGNOSTIC_SNAPSHOT",
            Payload = "{\"SnapshotId\": \"00000000-0000-0000-0000-000000000001\"}",
            Signature = "mock_sig"
        };

        var result = await handler.HandleCommandAsync(serverCommand);

        Assert.True(result.Success);
        Assert.Equal(ErrorCode.None, result.ErrorCode);
        Assert.True(fakeReporter.SyncTriggered);
    }

    private class MockSystemInfoReporter : ISystemInfoReporter
    {
        public bool SyncTriggered { get; private set; }

        public Task<SystemInfoResponse?> ReportInfoAsync(SystemInfoData data)
        {
            return Task.FromResult<SystemInfoResponse?>(new SystemInfoResponse { Success = true });
        }

        public IConfigurationService GetConfigService()
        {
            return new ConfigurationService(NullLogger<ConfigurationService>.Instance, new AgentConfig());
        }

        public Task<SystemInfoResponse?> TriggerSyncAsync()
        {
            SyncTriggered = true;
            return Task.FromResult<SystemInfoResponse?>(new SystemInfoResponse { Success = true });
        }
    }
}
