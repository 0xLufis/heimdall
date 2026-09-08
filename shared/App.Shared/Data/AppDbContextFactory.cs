using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace App.Shared.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 1. Set up configuration to read from your backend's appsettings
        var backendPath = Path.Combine(Directory.GetCurrentDirectory(), "../../backend/App.Backend.Api");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(backendPath) ? backendPath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // 2. Get the connection string with environment parameterization
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["DATABASE_URL"] ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var efUser = Environment.GetEnvironmentVariable("EF_ADMIN_USER") ?? "ef_admin";
            var efPw = Environment.GetEnvironmentVariable("EF_ADMIN_PASSWORD");
            if (string.IsNullOrEmpty(efPw))
            {
                var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
                if (!env.Equals("Development", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("CRITICAL CONFIGURATION ERROR: ConnectionStrings:DefaultConnection or DATABASE_URL must be specified for design-time DbContext.");
                }
                efPw = "migrate"; // Local development fallback for CLI tooling
            }
            connectionString = $"Host=localhost;Port=5432;Database=heimdall_dev_db;Username={efUser};Password={efPw}";
        }

        // 3. Configure the DbContext Options
        var builder = new DbContextOptionsBuilder<AppDbContext>();

        builder.UseNpgsql(connectionString)
               .UseSnakeCaseNamingConvention();

        return new AppDbContext(builder.Options);
    }
}
