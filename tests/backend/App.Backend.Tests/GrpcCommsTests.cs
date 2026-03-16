using App.Shared.Protos;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;

namespace App.Backend.Tests;

public class GrpcCommsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GrpcCommsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real DB with In-Memory for testing
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb");
                });
            });
        });
    }

    [Fact]
    public async Task ReportSystemInfo_ReturnsSuccess()
    {
        // Create gRPC client
        var client = _factory.CreateDefaultClient();
        var channel = GrpcChannel.ForAddress(client.BaseAddress!, new GrpcChannelOptions
        {
            HttpClient = client
        });
        var grpcClient = new SystemInfoCollector.SystemInfoCollectorClient(channel);

        // Prepare request
        var request = new SystemInfoRequest
        {
            Hostname = "TestHost",
            MachineIdentifier = "Test-ID",
            MacAddress = "00:11:22:33:44:55",
            LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            HardwareConfig = new App.Shared.Protos.HardwareConfig
            {
                Cpu = "Test CPU",
                Ram = "16 GB"
            },
            SoftwareConfig = new App.Shared.Protos.SoftwareConfig
            {
                OsVersion = "Test OS"
            }
        };

        // Act
        var response = await grpcClient.ReportSystemInfoAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("TestHost", response.Message);
    }
}
