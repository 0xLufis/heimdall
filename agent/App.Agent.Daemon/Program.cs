using App.Agent.Daemon;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Load .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Ensure it only listens on localhost for security
builder.WebHost.UseUrls("http://localhost:5998");

builder.Services.AddSingleton<ConfigurationService>();
builder.Services.AddSingleton<SystemInfoService>();
builder.Services.AddSingleton<SystemInfoReporter>();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// Configurator UI
app.MapGet("/", () => Results.Content(@"
<!DOCTYPE html>
<html>
<head>
    <title>Heimdall Agent Configurator</title>
    <style>
        body { font-family: sans-serif; background: #1a1a1a; color: #eee; padding: 2rem; }
        .container { max-width: 600px; margin: 0 auto; background: #2a2a2a; padding: 2rem; border-radius: 8px; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }
        h1 { color: #3b82f6; }
        .field { margin-bottom: 1rem; }
        label { display: block; margin-bottom: 0.5rem; color: #9ca3af; }
        input { width: 100%; padding: 0.5rem; background: #3f3f46; border: 1px solid #52525b; color: white; border-radius: 4px; box-sizing: border-box; }
        button { background: #3b82f6; color: white; border: none; padding: 0.5rem 1rem; border-radius: 4px; cursor: pointer; }
        button:hover { background: #2563eb; }
        .status { margin-top: 1rem; padding: 1rem; border-radius: 4px; background: #374151; }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Heimdall Agent</h1>
        <div class='field'>
            <label>Backend URL</label>
            <input id='backendUrl' type='text' />
        </div>
        <div class='field'>
            <label>Auth Type</label>
            <input id='authType' type='text' readonly />
        </div>
        <button onclick='saveConfig()'>Save Configuration</button>
        <div class='status' id='status'>
            Waiting for status...
        </div>
    </div>
    <script>
        async function loadConfig() {
            const res = await fetch('/api/config');
            const config = await res.json();
            document.getElementById('backendUrl').value = config.backendUrl;
            document.getElementById('authType').value = config.authType;
        }
        async function saveConfig() {
            const backendUrl = document.getElementById('backendUrl').value;
            const res = await fetch('/api/config', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ backendUrl })
            });
            if (res.ok) alert('Configuration saved!');
            else alert('Error saving configuration');
        }
        loadConfig();
    </script>
</body>
</html>
", "text/html"));

app.MapGet("/api/config", (ConfigurationService configService) => configService.Config);

app.MapPost("/api/config", (ConfigurationService configService, AgentConfig newConfig) => {
    var config = configService.Config;
    config.BackendUrl = newConfig.BackendUrl;
    configService.SaveConfig(config);
    return Results.Ok();
});

app.Run();
