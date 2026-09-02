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
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    if (builder.Environment.IsDevelopment())
    {
        connectionString = "Host=localhost;Port=5432;Database=heimdall_dev_db;Username=dotnet_backend;Password=your_backend_pw";
    }
    else
    {
        throw new InvalidOperationException("CRITICAL SECURITY CONFIGURATION ERROR: DATABASE_URL or ConnectionStrings:DefaultConnection must be specified via environment variables.");
    }
}

NpgsqlDataSource? dataSource = null;

// Configure Kestrel protocols and interface bindings
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // HTTP/1.1 and HTTP/2 on primary API port
    serverOptions.ListenAnyIP(5099, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });

    if (builder.Environment.IsDevelopment())
    {
        // Dedicated port for local dev cleartext gRPC (restricted to localhost)
        serverOptions.ListenLocalhost(5001, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2;
        });
    }
    else
    {
        serverOptions.ListenAnyIP(7158, listenOptions =>
        {
            listenOptions.UseHttps();
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
        });
    }
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

// --- 2. Repositories & Services & Caching ---
builder.Services.AddMemoryCache();

var redisPassword = builder.Configuration["REDIS_PASSWORD"] ?? "heimdall_redis_dev_secret";
var redisConnectionStr = builder.Configuration["REDIS_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("Redis")
    ?? $"localhost:6379,password={redisPassword},abortConnect=false,connectTimeout=2000";

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionStr;
    options.InstanceName = "heimdall:";
});

builder.Services.AddSingleton<ICacheService, CacheService>();
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

builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, DynamicSecurityGroupClaimsTransformer>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdministration", policy =>
        policy.RequireRole("admin", "system_admin"));

    options.AddPolicy("EndpointConfigManagement", policy =>
        policy.RequireRole("admin", "system_admin", "lead_engineer", "engineer", "controls_engineer"));

    options.AddPolicy("RemoteExecution", policy =>
        policy.RequireRole("admin", "system_admin", "lead_engineer", "engineer"));

    options.AddPolicy("MaintenanceOperations", policy =>
        policy.RequireRole("admin", "system_admin", "lead_engineer", "engineer", "technician"));
});

// --- 4. Controllers & SignalR & gRPC & Swagger ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
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

if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.Run();

public partial class Program { }
