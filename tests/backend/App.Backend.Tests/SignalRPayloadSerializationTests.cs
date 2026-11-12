namespace App.Backend.Tests;

using System.Buffers;
using System.Linq;
using System.Text;
using App.Backend.Api.Hubs;
using App.Shared.Entities;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Hub payloads go through SignalR's own JSON protocol, not the MVC serializer. A payload it cannot write
/// (an EF entity graph with a navigation cycle) makes SignalR abort every connection the message was sent to.
/// </summary>
public sealed class SignalRPayloadSerializationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SignalRPayloadSerializationTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public void TicketBroadcast_WithCyclicMachineGroupGraph_IsSerializedInsteadOfAbortingTheConnection()
    {
        var plant = new MachineGroup { Name = "Plant" };
        var line = new MachineGroup { Name = "Line 7", Parent = plant };
        plant.Children.Add(line);
        var ticket = new MaintenanceTicket
        {
            Title = "Spindle overheating",
            Machine = new Machine { Name = "OP060", LineGroup = line }
        };

        var protocol = _factory.Services.GetServices<IHubProtocol>().Single(p => p.Name == "json");
        var buffer = new ArrayBufferWriter<byte>();
        protocol.WriteMessage(new InvocationMessage(nameof(IMaintenanceClient.TicketCreated), new object?[] { ticket }), buffer);

        var json = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Assert.Contains("\"title\":\"Spindle overheating\"", json);
        Assert.Contains("\"name\":\"Line 7\"", json);
    }
}
