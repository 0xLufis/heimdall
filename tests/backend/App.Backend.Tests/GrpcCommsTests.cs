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
            LastOnline = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };
        request.Components.Add(new App.Shared.Protos.InventoryComponent
        {
            Name = "Hardware",
            Technology = "Agent",
            Type = "hardware",
            DataJson = "{}"
        });
        request.Components.Add(new App.Shared.Protos.InventoryComponent
        {
            Name = "Software",
            Technology = "Agent",
            Type = "software",
            DataJson = "{}"
        });

        // Act
        var response = await grpcClient.ReportSystemInfoAsync(request);

        // Assert
        Assert.True(response.Success);
        Assert.Contains("TestHost", response.Message);
    }
}

// Custom WebApplicationFactory to override services for testing
public class CustomWebApplicationFactory : WebApplicationFactory<App.Backend.Api.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the environment to "Test" to prevent Program.cs from configuring NpgsqlDataSource
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Remove all existing DbContext and Npgsql registrations
            var descriptors = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(IDbContextFactory<AppDbContext>) ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType == typeof(NpgsqlDataSource)).ToList();

            foreach (var descriptor in descriptors)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            var dbName = "TestDb_" + Guid.NewGuid().ToString();
            services.AddDbContextFactory<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        });
    }
}
