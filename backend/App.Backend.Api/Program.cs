using App.Backend.Api.Hubs;
using App.Backend.Api.Security;
using App.Backend.Api.Services;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using DotNetEnv;
using Npgsql;

// Load .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// --- Connection String and DataSource declaration ---
var connectionString = builder.Configuration["DATABASE_URL"] 
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=heimdall_dev_db;Username=ef_admin;Password=migrate";
NpgsqlDataSource? dataSource = null;

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // HTTP/1.1 and HTTP/2 (for TLS) on the main ports
    serverOptions.ListenAnyIP(5099, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    serverOptions.ListenAnyIP(7158, listenOptions =>
    {
        listenOptions.UseHttps();
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    // Dedicated port for cleartext gRPC (HTTP/2 only)
    serverOptions.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
    });
});

// Ensure Environment Variables are included in configuration
builder.Configuration.AddEnvironmentVariables();

// --- 1. Database ---
if (!builder.Environment.IsEnvironment("Test"))
{
    var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.EnableDynamicJson();

    if (!connectionString.Contains("Maximum Pool Size", StringComparison.OrdinalIgnoreCase))
    {
        dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 250;
    }

    dataSource = dataSourceBuilder.Build();

    // Register DbContext and DbContextFactory
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseNpgsql(dataSource!).UseSnakeCaseNamingConvention();
    });
    builder.Services.AddDbContextFactory<AppDbContext>(options =>
    {
        options.UseNpgsql(dataSource!).UseSnakeCaseNamingConvention();
    }, ServiceLifetime.Scoped);
}

// --- 2. Repositories & Services ---
builder.Services.AddScoped<IStationRepository, StationRepository>();
builder.Services.AddScoped<IControllerRepository, ControllerRepository>();
builder.Services.AddScoped<IClientPcRepository, ClientPcRepository>();
builder.Services.AddScoped<ClientPcRepository>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IMaintenanceTicketRepository, MaintenanceTicketRepository>();
builder.Services.AddScoped<OpcUaGatewayService>();
builder.Services.AddScoped<CopiaIntegrationService>();

// --- 3. Authentication & Authorization ---
builder.Services.AddAuthentication("BetterAuth")
    .AddScheme<BetterAuthOptions, BetterAuthHandler>("BetterAuth", options => { });

builder.Services.AddAuthorization();

// --- 4. Controllers & SignalR & gRPC & Swagger ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddSignalR();
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Heimdall API V1");
    c.RoutePrefix = "api-docs";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MaintenanceHub>("/hubs/maintenance");
app.MapGrpcService<SystemInfoCollectorService>();
app.MapHub<MaintenanceHub>("/hubs/maintenance");

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.Run();

public partial class Program { }
