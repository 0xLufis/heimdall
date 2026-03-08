using App.Backend.Api.Security;
using App.Infrastructure.Repositories;
using App.Shared.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Database ---
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
});

// --- 2. Repositories ---
builder.Services.AddScoped<ClientPcRepository>();

// --- 3. Authentication & Authorization ---
// Register our custom Better-Auth handler
builder.Services.AddAuthentication("BetterAuth")
    .AddScheme<BetterAuthOptions, BetterAuthHandler>("BetterAuth", options => { });

builder.Services.AddAuthorization();

// --- 4. Controllers ---
builder.Services.AddControllers();

var app = builder.Build();

app.UseHttpsRedirection();

// These two must be in this exact order, right before MapControllers!
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
