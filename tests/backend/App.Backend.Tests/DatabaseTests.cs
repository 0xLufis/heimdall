using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using App.Backend.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class DatabaseTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_AddsClientPcToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        var repository = new ClientPcRepository(context);
        var pc = new ClientPc
        {
            Id = Guid.NewGuid(),
            Hostname = "TestHost",
            MachineIdentifier = "Test-ID",
            MacAddress = "00:11:22:33:44:55",
            InventoryItems = new List<BaseInventoryItem>
            {
                new PcHardware { Name = "Hardware" }
            }
        };

        // Act
        var result = await repository.CreateAsync(pc);

        // Assert
        var savedPc = await context.ClientPcs.FindAsync(pc.Id);
        Assert.NotNull(savedPc);
        Assert.Equal("TestHost", savedPc.Hostname);
    }

    [Fact]
    public void EncryptedStringConverter_EncryptsAndDecryptsCorrectly()
    {
        // Arrange
        string plainText = "SECRET_AES_256_GCM_LICENSE_KEY_12345";

        // Act
        string? encrypted = EncryptedStringConverter.Encrypt(plainText);
        string? decrypted = EncryptedStringConverter.Decrypt(encrypted);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public async Task GraphRelationalEntities_CanBeCreatedAndRetrieved()
    {
        // Arrange
        using var context = CreateContext();
        
        var machine = new Machine { Id = Guid.NewGuid(), Name = "Station-1", CustomIdentifier = "ST-01" };
        var clientPc = new ClientPc { Id = Guid.NewGuid(), Name = "IPC-01", MacAddress = "AA:BB:CC:DD:EE:FF" };
        
        context.Machines.Add(machine);
        context.ClientPcs.Add(clientPc);
        await context.SaveChangesAsync();

        var stationController = new StationController
        {
            MachineId = machine.Id,
            ClientPcId = clientPc.Id,
            Role = "Primary"
        };
        context.StationControllers.Add(stationController);

        var softwareAsset = new SoftwareAsset
        {
            Id = Guid.NewGuid(),
            Name = "Siemens TIA Portal",
            Version = "v18.0",
            LicenseKey = "TIAPORTAL-KEY-9999"
        };
        context.SoftwareAssets.Add(softwareAsset);

        var hardwareComp = new HardwareComponent
        {
            Id = Guid.NewGuid(),
            Name = "S7-1500 PLC",
            Revision = "Rev 2",
            ModelNumber = "6ES7515-2AM01-0AB0",
            Firmware = new List<SoftwareAsset> { softwareAsset }
        };
        context.HardwareComponents.Add(hardwareComp);

        var interconnect = new EquipmentInterconnect
        {
            SourceEquipmentId = machine.Id,
            TargetEquipmentId = hardwareComp.Id,
            InterconnectType = "PROFINET",
            PortOrAddress = "192.168.1.10"
        };
        context.EquipmentInterconnects.Add(interconnect);

        var ticket = new MaintenanceTicket
        {
            Title = "Replace PLC Fan Module",
            Description = "Fan module warning logged on S7-1500",
            MachineId = machine.Id,
            EquipmentId = hardwareComp.Id,
            Status = "Open",
            Priority = "High",
            Comments = new List<TicketComment>
            {
                new TicketComment { Author = "Technician", Content = "Ordered replacement part." }
            },
            Attachments = new List<TicketAttachment>
            {
                new TicketAttachment { FileName = "photo.jpg", StoragePath = "/storage/photo.jpg" }
            }
        };
        context.MaintenanceTickets.Add(ticket);
        await context.SaveChangesAsync();

        // Assert
        var savedTicket = await context.MaintenanceTickets
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == ticket.Id);

        Assert.NotNull(savedTicket);
        Assert.Single(savedTicket.Comments);
        Assert.Single(savedTicket.Attachments);
        Assert.Equal("High", savedTicket.Priority);
    }
}
