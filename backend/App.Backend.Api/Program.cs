using App.Backend.Api.Configuration;
using App.Backend.Api.Hubs;
using App.Backend.Api.Security;
using App.Backend.Api.Services;
using App.Backend.Api.Services.Mqtt;
using App.Contracts.Configuration;
using App.Contracts.Mqtt;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using DotNetEnv;
using Npgsql;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

// Load .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// --- Connection String and DataSource declaration ---
var connectionString = builder.Configuration["DATABASE_URL"] 
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test"))
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
        // Dedicated port for local dev cleartext HTTP API (accessible inside container network)
        serverOptions.ListenAnyIP(5001, listenOptions =>
        {
            listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
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

// Strongly-typed Backend Feature Flags
var featureFlags = builder.Configuration.ToBackendFeatureFlags(builder.Environment.EnvironmentName);
builder.Services.AddSingleton(featureFlags);

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
builder.Services.AddScoped<ControllerRepository>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IMaintenanceTicketRepository, MaintenanceTicketRepository>();
builder.Services.AddScoped<IMachineGroupRepository, MachineGroupRepository>();
builder.Services.AddScoped<ITechnicianRepository, TechnicianRepository>();
builder.Services.AddScoped<IOpcUaGatewayService, OpcUaGatewayService>();
builder.Services.AddScoped<OpcUaGatewayService>(sp => (OpcUaGatewayService)sp.GetRequiredService<IOpcUaGatewayService>());
builder.Services.AddScoped<ICopiaIntegrationService, CopiaIntegrationService>();
builder.Services.AddScoped<CopiaIntegrationService>(sp => (CopiaIntegrationService)sp.GetRequiredService<ICopiaIntegrationService>());
builder.Services.AddScoped<IReportExportService, ReportExportService>();
builder.Services.AddScoped<ReportExportService>(sp => (ReportExportService)sp.GetRequiredService<IReportExportService>());
builder.Services.AddScoped<IPredictiveMaintenanceService, PredictiveMaintenanceService>();
builder.Services.AddScoped<PredictiveMaintenanceService>(sp => (PredictiveMaintenanceService)sp.GetRequiredService<IPredictiveMaintenanceService>());
builder.Services.AddSingleton<App.Backend.Api.Services.Plugins.IPluginService>(sp =>
    new App.Backend.Api.Services.Plugins.PluginService(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<App.Backend.Api.Services.Plugins.PluginService>>()));

// --- 3. Authentication & Authorization ---
builder.Services.AddAuthentication("BetterAuth")
    .AddScheme<BetterAuthOptions, BetterAuthHandler>("BetterAuth", options => { });

builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, DynamicSecurityGroupClaimsTransformer>();

builder.Services.AddAuthorization(options =>
{
    // God user (system_admin) and Heimdall platform administrators (heimdall_admin, legacy admin, plant_director)
    options.AddPolicy(AuthorizationPolicies.SystemAdministration, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.PlantDirector));

    // IT Infrastructure operations (AD, Entra, PKI, OU approvals)
    options.AddPolicy(AuthorizationPolicies.ItAdministration, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.ItAdmin, HeimdallRoles.ItSiteAdmin));

    // Engineering administration (Functional settings & User management)
    options.AddPolicy(AuthorizationPolicies.EngineeringAdministration, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager));

    // Functional endpoint configurations
    options.AddPolicy(AuthorizationPolicies.EndpointConfigManagement, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager, HeimdallRoles.GroupLeader, HeimdallRoles.LeadEngineer, HeimdallRoles.Engineer, HeimdallRoles.ControlsEngineer));

    // Remote execution & OT commands
    options.AddPolicy(AuthorizationPolicies.RemoteExecution, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager, HeimdallRoles.GroupLeader, HeimdallRoles.LeadEngineer, HeimdallRoles.Engineer));

    // Maintenance operations
    options.AddPolicy(AuthorizationPolicies.MaintenanceOperations, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.SeniorEngineeringManager, HeimdallRoles.GroupLeader, HeimdallRoles.LeadEngineer, HeimdallRoles.Engineer, HeimdallRoles.Technician, HeimdallRoles.OperativePlanner, HeimdallRoles.Manager));

    // Operative line stop requests and scheduling approvals
    options.AddPolicy("LineStopRequest", policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.PlantDirector, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.OperativePlanner, HeimdallRoles.GroupLeader));
});

// --- 4. Controllers & SignalR & gRPC & Swagger & CORS & RateLimiting ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ApiLimiter", opt =>
    {
        opt.PermitLimit = 120;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 20;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.AddFixedWindowLimiter("StrictLimiter", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 5;
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("HeimdallCorsPolicy", policy =>
    {
        if (featureFlags.EnableDevFeatures || builder.Environment.IsEnvironment("Test"))
        {
            policy.WithOrigins(
                    "http://localhost:3000",
                    "http://127.0.0.1:3000",
                    "http://localhost:5099",
                    "http://127.0.0.1:5099",
                    "http://localhost:5173",
                    "http://127.0.0.1:5173"
                  )
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            var configuredOrigins = builder.Configuration["ALLOWED_ORIGINS"]
                ?.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();

            if (configuredOrigins.Length > 0)
            {
                policy.WithOrigins(configuredOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        }
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// --- MQTT Telemetry Ingestion & Embedded Broker Subsystem ---
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.AddSingleton<IMqttBrokerService, MqttBrokerService>();
builder.Services.AddScoped<ITelemetryIngestionService, TelemetryIngestionService>();
builder.Services.AddSingleton<MqttIngestionHostedService>();
builder.Services.AddSingleton<IMqttIngestionService>(sp => sp.GetRequiredService<MqttIngestionHostedService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttIngestionHostedService>());

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Heimdall API",
        Version = "v1",
        Description = "Industrial PC and Fleet Monitoring Backend API"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter 'Bearer {token}' or your Better-Auth session token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

var app = builder.Build();

app.UseCors("HeimdallCorsPolicy");

if (featureFlags.EnableDevFeatures)
{
    app.UseDeveloperExceptionPage();

    // Enable middleware to serve generated Swagger as a JSON endpoint.
    app.UseSwagger();

    app.UseSwaggerUI(c => 
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Heimdall API V1");
        c.RoutePrefix = "swagger";
    });

    // Swagger route redirects
    app.MapGet("/", () => Results.Redirect("/swagger"));
    app.MapGet("/api-docs", () => Results.Redirect("/swagger"));
    app.MapGet("/api-docs/{**catchall}", () => Results.Redirect("/swagger"));
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets();

app.MapControllers();
app.MapHub<MaintenanceHub>("/hubs/maintenance");

if (!featureFlags.EnableDevFeatures)
{
    app.MapGet("/error", () => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred."));
}

app.Run();

namespace App.Backend.Api
{
    public partial class Program { }
}
