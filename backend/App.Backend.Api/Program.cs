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
// Conditionally build NpgsqlDataSource only if a connection string is provided
if (!string.IsNullOrEmpty(connectionString))
{
    var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.EnableDynamicJson();
    dataSource = dataSourceBuilder.Build();
    
    // Register the DbContextFactory
    builder.Services.AddDbContextFactory<AppDbContext>(options =>
    {
        options.UseNpgsql(dataSource!).UseSnakeCaseNamingConvention();
    });
}
else if (builder.Environment.IsEnvironment("Test"))
{
    // Configure for testing if needed
}

// --- 2. Repositories ---
builder.Services.AddScoped<ClientPcRepository>();

// --- 3. Authentication & Authorization ---
// Register our custom Better-Auth handler
builder.Services.AddAuthentication("BetterAuth")
    .AddScheme<BetterAuthOptions, BetterAuthHandler>("BetterAuth", options => { });

builder.Services.AddAuthorization();

// --- 4. Controllers & gRPC & Swagger ---
builder.Services.AddControllers();
builder.Services.AddGrpc();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Enable middleware to serve generated Swagger as a JSON endpoint.
app.UseSwagger();

// Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
// specifying the Swagger JSON endpoint.
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Heimdall API V1");
    c.RoutePrefix = "api-docs";
});

// app.UseHttpsRedirection(); // Commented out for development to allow cleartext gRPC

// These two must be in this exact order, right before MapControllers!
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGrpcService<SystemInfoCollectorService>();

app.Run();

public partial class Program { }
