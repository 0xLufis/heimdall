using App.Backend.Api.Controllers.V1;
using App.Infrastructure.Repositories;
using App.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace App.Backend.Tests;

public class FakeControllerRepository : IControllerRepository
{
    public List<ClientPc> Items { get; set; } = new();

    public Task<List<ClientPc>> GetAllAsync() => Task.FromResult(Items.ToList());

    public Task<ClientPc?> GetByIdAsync(Guid id) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task<ClientPc> CreateAsync(ClientPc pc)
    {
        Items.Add(pc);
        return Task.FromResult(pc);
    }

    public Task<ClientPc> UpsertByMacAddressAsync(ClientPc pc)
    {
        var existing = Items.FirstOrDefault(p => p.MacAddress == pc.MacAddress);
        if (existing != null)
        {
            Items.Remove(existing);
        }
        Items.Add(pc);
        return Task.FromResult(pc);
    }

    public Task<ClientPc?> UpdateAsync(Guid id, string? name, string? hostname, string? macAddress, string? pinnedObjectHandle, List<Guid>? controlledMachineIds)
    {
        var item = Items.FirstOrDefault(p => p.Id == id);
        if (item == null) return Task.FromResult<ClientPc?>(null);
        if (name != null) item.Name = name;
        if (hostname != null) item.Hostname = hostname;
        if (macAddress != null) item.MacAddress = macAddress;
        if (pinnedObjectHandle != null) item.PinnedObjectHandle = pinnedObjectHandle;
        return Task.FromResult<ClientPc?>(item);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var item = Items.FirstOrDefault(p => p.Id == id);
        if (item != null)
        {
            Items.Remove(item);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> GetCountAsync() => Task.FromResult(Items.Count);
    public Task<int> GetActiveCountAsync(TimeSpan activeThreshold) => Task.FromResult(Items.Count);
    public Task<List<ClientPc>> GetRecentClientsAsync(int count) => Task.FromResult(Items.Take(count).ToList());
}

public class ClientPcControllerTests
{
    private readonly FakeControllerRepository _fakeRepo;
    private readonly ClientPcController _controller;

    public ClientPcControllerTests()
    {
        _fakeRepo = new FakeControllerRepository();
        _controller = new ClientPcController(_fakeRepo, NullLogger<ClientPcController>.Instance);
    }

    [Fact]
    public async Task GetClientPcs_ReturnsOkResult_WithClientPcList()
    {
        // Arrange
        var testPc = new ClientPc
        {
            Id = Guid.NewGuid(),
            Name = "IPC-TEST-01",
            Hostname = "ipc-test-01.factory.corp",
            MacAddress = "00:11:22:33:44:55",
            OrganizationId = "Plant-A"
        };
        _fakeRepo.Items.Add(testPc);

        // Act
        var result = await _controller.GetClientPcs();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IEnumerable<App.Backend.Api.Dtos.ClientPcDto>>(okResult.Value);
        var item = Assert.Single(dtos);
        Assert.Equal(testPc.Id, item.Id);
        Assert.Equal("Plant-A", item.OrganizationId);
    }

    [Fact]
    public async Task GetClientPc_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var result = await _controller.GetClientPc(missingId);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetClientPc_WhenFound_ReturnsOkResult()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var testPc = new ClientPc
        {
            Id = testId,
            Name = "IPC-TEST-02",
            Hostname = "ipc-02",
            MacAddress = "AA:BB:CC:DD:EE:FF",
            OrganizationId = "Plant-B"
        };
        _fakeRepo.Items.Add(testPc);

        // Act
        var result = await _controller.GetClientPc(testId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var pc = Assert.IsType<ClientPc>(okResult.Value);
        Assert.Equal(testId, pc.Id);
        Assert.Equal("Plant-B", pc.OrganizationId);
    }

    [Fact]
    public async Task DeleteClientPc_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var missingId = Guid.NewGuid();

        // Act
        var result = await _controller.DeleteClientPc(missingId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task DeleteClientPc_WhenDeleted_ReturnsNoContent()
    {
        // Arrange
        var testId = Guid.NewGuid();
        _fakeRepo.Items.Add(new ClientPc { Id = testId, Name = "PC-To-Delete", MacAddress = "11:22:33:44:55:66" });

        // Act
        var result = await _controller.DeleteClientPc(testId);

        // Assert
        Assert.IsType<NoContentResult>(result);
    }
}
