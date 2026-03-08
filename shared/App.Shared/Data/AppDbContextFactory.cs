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

        // 2. Get the connection string, with a fallback for local Docker dev
        //var connectionString = configuration.GetConnectionString("DefaultConnection")
        //    ?? "Host=localhost;Port=5432;Database=heimdall_dev_db;Username=admin;Password=admin";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
           ?? "Host=localhost;Port=5432;Database=heimdall_dev_db;Username=admin;Password=admin";

        // 3. Configure the DbContext Options
        var builder = new DbContextOptionsBuilder<AppDbContext>();

        builder.UseNpgsql(connectionString)
               .UseSnakeCaseNamingConvention();

        return new AppDbContext(builder.Options);
    }
}
