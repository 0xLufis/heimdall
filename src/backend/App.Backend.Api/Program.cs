using App.Backend.Api.Configuration;
using App.Backend.Api.Hubs;
using App.Backend.Api.Security;
using App.Backend.Api.Services;
using App.Backend.Api.Services.Mqtt;
using App.Contracts.Configuration;
using App.Contracts.Mqtt;
using App.Contracts.Security;
using App.Infrastructure.Repositories;
using App.Infrastructure.Services;
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

if (builder.Environment.IsDevelopment())
{
    // dotnet watch restarts the backend on every save and waits for the old instance to exit (justfile):
    // lingering connections must not hold that for the default 30 s.
    builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));
}

// Ensure Environment Variables are included in configuration
builder.Configuration.AddEnvironmentVariables();

// Rotating file log (Logging:File) and runtime log levels (api/v1/admin/logging). Added after the
// environment variables so that runtime overrides take precedence over them.
App.Backend.Api.Logging.HeimdallLoggingExtensions.AddHeimdallLogging(builder);

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
builder.Services.AddScoped<IComponentInventoryBridge, ComponentInventoryBridge>();
builder.Services.AddScoped<IDelegationRecipeService, DelegationRecipeService>();
builder.Services.AddScoped<IMachineRaisedTicketService, MachineRaisedTicketService>();
builder.Services.AddScoped<IGenericTemplateService, GenericTemplateService>();
// Scoped: the DbContext factory is registered scoped. The singleton PluginService resolves it per call.
builder.Services.AddScoped<App.Backend.Api.Services.Pki.PkiService>(sp =>
    new App.Backend.Api.Services.Pki.PkiService(
        sp.GetRequiredService<IDbContextFactory<App.Shared.Data.AppDbContext>>(),
        sp.GetRequiredService<ILogger<App.Backend.Api.Services.Pki.PkiService>>(),
        sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<App.Backend.Api.Services.Plugins.IPluginService>(sp =>
    new App.Backend.Api.Services.Plugins.PluginService(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<App.Backend.Api.Services.Plugins.PluginService>>()));
builder.Services.AddScoped<App.Shared.Lua.Bridge.ILuaPlantDataProvider, App.Infrastructure.Services.DbPlantDataProvider>();
builder.Services.AddSingleton<App.Backend.Api.Services.ILuaAutomationService>(sp =>
    new App.Backend.Api.Services.LuaAutomationService(
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<App.Backend.Api.Services.LuaAutomationService>>()));

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
        policy.RequireRole(
            HeimdallRoles.SystemAdmin,
            HeimdallRoles.HeimdallAdmin,
            HeimdallRoles.Admin,
            HeimdallRoles.PlantDirector,
            HeimdallRoles.EngineeringAdmin,
            HeimdallRoles.PlantEngineeringManager,
            HeimdallRoles.SeniorEngineeringManager,
            HeimdallRoles.GroupLeader,
            HeimdallRoles.ShiftLeader,
            HeimdallRoles.LeadEngineer,
            HeimdallRoles.ControlsEngineer,
            HeimdallRoles.Engineer,
            HeimdallRoles.Technician,
            HeimdallRoles.OperativePlanner,
            HeimdallRoles.Operator,
            HeimdallRoles.Manager,
            HeimdallRoles.User));

    // Operative line stop requests and scheduling approvals
    options.AddPolicy("LineStopRequest", policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.PlantDirector, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.OperativePlanner, HeimdallRoles.GroupLeader));

    // Functional settings management
    options.AddPolicy(AuthorizationPolicies.FunctionalSettingsManagement, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager));

    // Delegation management
    options.AddPolicy(AuthorizationPolicies.DelegationManagement, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.GroupLeader, HeimdallRoles.LeadEngineer));

    // Skills qualification management
    options.AddPolicy(AuthorizationPolicies.SkillsManagement, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.GroupLeader));

    // Machine component lifecycle & part assignment
    options.AddPolicy(AuthorizationPolicies.ComponentManagement, policy =>
        policy.RequireRole(HeimdallRoles.SystemAdmin, HeimdallRoles.HeimdallAdmin, HeimdallRoles.Admin, HeimdallRoles.EngineeringAdmin, HeimdallRoles.PlantEngineeringManager, HeimdallRoles.LeadEngineer, HeimdallRoles.ControlsEngineer, HeimdallRoles.Engineer, HeimdallRoles.Technician));

    // Organization management (create; rename, members, invitations of the caller's own
    // organizations; Heimdall admins: every organization). Heimdall admins and middle managers and up
    // by role (OrganizationManagementRules.Roles), anyone else through the explicit per-user grant.
    options.AddPolicy(AuthorizationPolicies.OrganizationManagement, policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.HasClaim(HeimdallClaimTypes.Permission, HeimdallPermissions.OrganizationsManage) ||
            OrganizationManagementRules.Roles.Any(ctx.User.IsInRole)));
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

    // HTTP bearer scheme: paste the bare Better-Auth session token, Swagger UI adds the "Bearer " prefix
    // the backend's auth handler expects.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Better-Auth session token (without the 'Bearer ' prefix). In Development an unauthenticated request runs as the dev administrator.",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "Better-Auth session token"
    });

    // The reference must be bound to the document, otherwise it serializes as an empty requirement
    // and the token entered under "Authorize" is never sent.
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
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
// Activates the EF Core multi-tenant query filters (AppDbContext.CurrentOrganizationId) per request.
app.UseMiddleware<App.Backend.Api.Security.TenantScopeMiddleware>();

app.UseWebSockets();

app.MapControllers();
app.MapHub<MaintenanceHub>("/hubs/maintenance").RequireCors("HeimdallCorsPolicy");

if (!featureFlags.EnableDevFeatures)
{
    app.MapGet("/error", () => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred."));
}

if (app.Environment.IsDevelopment())
{
    // A restart can find the previous instance still releasing 5099/5001/1883 (DevelopmentPortWait).
    DevelopmentPortWait.WaitForFreePorts(DevelopmentPortWait.BackendPorts(app.Configuration), TimeSpan.FromSeconds(10), app.Logger);
}

app.Run();

namespace App.Backend.Api
{
    public partial class Program { }
}
