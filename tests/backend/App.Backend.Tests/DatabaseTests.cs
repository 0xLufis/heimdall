using App.Infrastructure.Repositories;
using App.Shared.Data;
using App.Shared.Entities;
using Microsoft.EntityFrameworkCore;
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
}