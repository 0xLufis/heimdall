using System;
using System.Linq;
using App.Backend.Api;
using App.Contracts.Mqtt;
using App.Shared.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace App.Backend.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<App.Backend.Api.Program>
{
    public int MqttTestPort { get; } = 18880 + Random.Shared.Next(1, 1000);

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

            // Configure test MQTT options with isolated dynamic port
            services.Configure<MqttOptions>(opts =>
            {
                opts.EmbeddedBrokerEnabled = false; // Disable background broker in WebAppFactory to prevent port collisions
                opts.BrokerPort = MqttTestPort;
            });
        });
    }
}
