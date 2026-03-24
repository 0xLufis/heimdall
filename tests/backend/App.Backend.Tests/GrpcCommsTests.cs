using App.Backend.Api.Services; // Use the service from the API project
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using App.Backend.Api; // Reference the Program class from App.Backend.Api
using App.Shared.Protos; // Add this using directive
using Npgsql; // Added for NpgsqlDataSourceBuilder
using Microsoft.AspNetCore.Hosting; // Added for IWebHostBuilder

namespace App.Backend.Tests;

public class GrpcCommsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GrpcCommsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
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
        // Use SystemInfoCollectorClient from App.Backend.Api.Services
        var grpcClient = new App.Shared.Protos.SystemInfoCollector.SystemInfoCollectorClient(channel);

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

// Custom WebApplicationFactory to override services for testing
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the environment to "Test" to prevent Program.cs from configuring NpgsqlDataSource
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove any existing DbContext registration
            var dbContextOptionsDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextOptionsDescriptor != null) services.Remove(dbContextOptionsDescriptor);

            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(AppDbContext));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            // Remove NpgsqlDataSource if it was added
            var npgsqlDataSourceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(NpgsqlDataSource));
            if (npgsqlDataSourceDescriptor != null) services.Remove(npgsqlDataSourceDescriptor);

            // Add in-memory database for testing
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString()); // Use unique name for each test run
            }, ServiceLifetime.Singleton); // Use Singleton to ensure it's the same instance across test services
        });
    }
}
