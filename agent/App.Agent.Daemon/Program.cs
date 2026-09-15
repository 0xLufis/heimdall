using App.Agent.Daemon;
using App.Agent.Daemon.Infrastructure.Beckhoff;
using App.Agent.Daemon.Infrastructure.Opc;
using App.Agent.Daemon.Reporting;
using App.Agent.Daemon.Reporting.Triggers;
using DotNetEnv;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Load .env file
Env.Load();

// Enable cleartext HTTP/2 (h2c) support for gRPC client connections to backend
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Enable Windows Service lifecycle management when running on Windows
if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
{
    builder.Host.UseWindowsService();
}

// Configurable binding address (defaults to 0.0.0.0:5998 for container/endpoint reachability)
var agentUrls = Environment.GetEnvironmentVariable("AGENT_URLS") ?? "http://0.0.0.0:5998";
builder.WebHost.UseUrls(agentUrls);

builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<ConfigurationService>(sp => (ConfigurationService)sp.GetRequiredService<App.Agent.Daemon.Interfaces.IConfigurationService>());

builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.ITelemetrySpooler, App.Agent.Daemon.Infrastructure.Spooling.LocalTelemetrySpooler>();
builder.Services.AddSingleton<App.Agent.Daemon.Infrastructure.Spooling.LocalTelemetrySpooler>(sp => (App.Agent.Daemon.Infrastructure.Spooling.LocalTelemetrySpooler)sp.GetRequiredService<App.Agent.Daemon.Interfaces.ITelemetrySpooler>());

builder.Services.AddSingleton<App.Agent.Daemon.Extensions.IExtensionRegistry, App.Agent.Daemon.Extensions.ExtensionRegistry>();
builder.Services.AddSingleton<App.Agent.Daemon.Infrastructure.Plugins.IPluginSandboxService, App.Agent.Daemon.Infrastructure.Plugins.PluginSandboxService>();
builder.Services.AddSingleton<App.Agent.Daemon.Infrastructure.Plugins.IPluginManager, App.Agent.Daemon.Infrastructure.Plugins.PluginManager>();

builder.Services.AddSingleton<App.Shared.Drivers.IDriverDiscoveryService, App.Agent.Daemon.Infrastructure.Drivers.DriverDiscoveryService>();
builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.IFileSystemScanner, App.Agent.Daemon.Infrastructure.FileSystem.FileSystemScanner>();
builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.ICommandHandler, App.Agent.Daemon.CommandHandling.CommandHandler>();

builder.Services.AddSingleton<IComponentContributor, HardwareComponentContributor>();
builder.Services.AddSingleton<IComponentContributor, SoftwareComponentContributor>();
builder.Services.AddSingleton<IComponentContributor, PhysicalDrivesComponentContributor>();
builder.Services.AddSingleton<IComponentContributor, DriversComponentContributor>();
builder.Services.AddSingleton<IComponentContributor, EventsComponentContributor>();
builder.Services.AddSingleton<IComponentContributor, ExtensionComponentContributor>();
builder.Services.AddSingleton<IComponentContributor, IndustrialOtComponentContributor>();

builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.ISystemInfoService, SystemInfoService>();
builder.Services.AddSingleton<SystemInfoService>(sp => (SystemInfoService)sp.GetRequiredService<App.Agent.Daemon.Interfaces.ISystemInfoService>());

builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.ISystemInfoReporter, SystemInfoReporter>();
builder.Services.AddSingleton<SystemInfoReporter>(sp => (SystemInfoReporter)sp.GetRequiredService<App.Agent.Daemon.Interfaces.ISystemInfoReporter>());

// Industrial OT Subsystems
builder.Services.AddSingleton<AdsSimulationServer>();
builder.Services.AddSingleton<MinimalOpcServer>();
builder.Services.AddSingleton<MinimalOpcClient>();
builder.Services.AddSingleton<TelemetryTriggerEngine>();

// Hosted Worker Service
builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<Worker>());

var app = builder.Build();

// Modernized Industrial Agent Web Dashboard
app.MapGet("/", () => Results.Content(@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Heimdall Industrial Edge Node Dashboard</title>
    <style>
        :root {
            --bg-base: #090d16;
            --bg-card: #0f172a;
            --bg-card-header: #1e293b;
            --border: #334155;
            --text-primary: #f8fafc;
            --text-secondary: #94a3b8;
            --accent-indigo: #6366f1;
            --accent-cyan: #06b6d4;
            --accent-emerald: #10b981;
            --accent-amber: #f59e0b;
            --accent-rose: #f43f5e;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: var(--bg-base);
            color: var(--text-primary);
            padding: 1.5rem;
            line-height: 1.5;
        }
        .container { max-width: 1200px; margin: 0 auto; }
        header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding-bottom: 1.5rem;
            border-bottom: 1px solid var(--border);
            margin-bottom: 1.5rem;
        }
        .brand { display: flex; align-items: center; gap: 0.75rem; }
        .logo-box {
            width: 42px; height: 42px; border-radius: 12px;
            background: rgba(99, 102, 241, 0.15); border: 1px solid rgba(99, 102, 241, 0.3);
            display: flex; align-items: center; justify-content: center;
            font-weight: 900; color: var(--accent-indigo); font-size: 1.25rem;
        }
        .title h1 { font-size: 1.25rem; font-weight: 800; letter-spacing: -0.025em; }
        .title p { font-size: 0.75rem; color: var(--text-secondary); text-transform: uppercase; font-weight: 700; letter-spacing: 0.05em; }
        .pill {
            display: inline-flex; align-items: center; gap: 0.35rem;
            font-size: 0.75rem; font-weight: 700; text-transform: uppercase;
            padding: 0.25rem 0.75rem; border-radius: 9999px;
            background: rgba(16, 185, 129, 0.1); color: var(--accent-emerald); border: 1px solid rgba(16, 185, 129, 0.2);
        }
        .pill-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--accent-emerald); }
        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
            gap: 1.25rem;
            margin-bottom: 1.5rem;
        }
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 16px;
            overflow: hidden;
            box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.2);
        }
        .card-header {
            padding: 1rem 1.25rem;
            background: rgba(30, 41, 59, 0.5);
            border-bottom: 1px solid var(--border);
            display: flex; align-items: center; justify-content: space-between;
        }
        .card-title { font-size: 0.875rem; font-weight: 800; text-transform: uppercase; letter-spacing: 0.05em; color: var(--text-primary); }
        .card-body { padding: 1.25rem; font-size: 0.8125rem; }
        .data-row {
            display: flex; justify-content: space-between; align-items: center;
            padding: 0.4rem 0; border-bottom: 1px solid rgba(51, 65, 85, 0.4);
        }
        .data-row:last-child { border-bottom: none; }
        .data-label { color: var(--text-secondary); font-size: 0.75rem; text-transform: uppercase; font-weight: 700; }
        .data-val { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-weight: 600; color: #cbd5e1; }
        .btn {
            background: var(--accent-indigo); color: white; border: none;
            padding: 0.5rem 1rem; border-radius: 8px; font-size: 0.8125rem; font-weight: 700;
            cursor: pointer; transition: all 0.15s; display: inline-flex; align-items: center; gap: 0.5rem;
        }
        .btn:hover { background: #4f46e5; }
        .btn-outline {
            background: transparent; border: 1px solid var(--border); color: var(--text-primary);
        }
        .btn-outline:hover { background: rgba(51, 65, 85, 0.5); }
        .btn-sm { padding: 0.25rem 0.6rem; font-size: 0.75rem; border-radius: 6px; }
        .tag-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.5rem; margin-top: 0.75rem; }
        .tag-item {
            background: rgba(15, 23, 42, 0.8); border: 1px solid var(--border);
            padding: 0.4rem 0.6rem; border-radius: 8px; font-family: monospace; font-size: 0.75rem;
        }
        .tag-item .k { color: var(--text-secondary); font-size: 0.65rem; text-transform: uppercase; }
        .tag-item .v { font-weight: 700; color: var(--accent-cyan); }
        .input-group { margin-bottom: 1rem; }
        .input-group label { display: block; font-size: 0.75rem; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 0.35rem; }
        .input-group input {
            width: 100%; padding: 0.5rem 0.75rem; background: #090d16;
            border: 1px solid var(--border); border-radius: 8px; color: white; font-size: 0.8125rem;
        }
        .alert-box {
            padding: 0.75rem 1rem; border-radius: 8px; background: rgba(99, 102, 241, 0.1);
            border: 1px solid rgba(99, 102, 241, 0.2); font-size: 0.75rem; color: #cbd5e1; margin-bottom: 1rem;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <header>
            <div class=""brand"">
                <div class=""logo-box"">H</div>
                <div class=""title"">
                    <h1 id=""lblHostname"">Heimdall Edge Node</h1>
                    <p id=""lblSub"">Industrial OT Daemon & TwinCAT ADS Simulation</p>
                </div>
            </div>
            <div style=""display: flex; gap: 0.75rem; align-items: center;"">
                <span class=""pill""><span class=""pill-dot""></span> <span id=""lblDaemonStatus"">Online</span></span>
                <button class=""btn btn-sm btn-outline"" onclick=""triggerReport()"">🚀 Dispatch Telemetry</button>
            </div>
        </header>

        <div class=""grid"">
            <!-- 1. Node Identity -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">🖥️ Host Environment</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">Hostname</span><span class=""data-val"" id=""valHost"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">Machine UUID</span><span class=""data-val"" id=""valUuid"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">MAC Address</span><span class=""data-val"" id=""valMac"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">OS Runtime</span><span class=""data-val"" id=""valOs"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">Cores / RAM</span><span class=""data-val"" id=""valHardware"">—</span></div>
                </div>
            </div>

            <!-- 2. Beckhoff TwinCAT ADS Simulation -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">⚡ Beckhoff TwinCAT ADS (Port 48898)</span>
                    <button class=""btn btn-sm btn-outline"" id=""btnAdsToggle"" onclick=""toggleAdsState()"">Toggle State</button>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">AMS Net ID</span><span class=""data-val"" id=""valNetId"">5.80.201.44.1.1:851</span></div>
                    <div class=""data-row""><span class=""data-label"">Runtime State</span><span class=""data-val"" id=""valAdsState"" style=""color: #10b981;"">ADSSTATE_RUN (5)</span></div>
                    <div class=""data-row""><span class=""data-label"">Requests Processed</span><span class=""data-val"" id=""valAdsReq"">0</span></div>
                    <div class=""tag-grid"" id=""adsTags"">
                        <div class=""tag-item""><div class=""k"">MAIN.CycleCounter</div><div class=""v"" id=""tagCycle"">—</div></div>
                        <div class=""tag-item""><div class=""k"">MAIN.TemperatureDegC</div><div class=""v"" id=""tagTemp"">—</div></div>
                        <div class=""tag-item""><div class=""k"">MAIN.PressureBar</div><div class=""v"" id=""tagPress"">—</div></div>
                        <div class=""tag-item""><div class=""k"">MAIN.PartsProduced</div><div class=""v"" id=""tagParts"">—</div></div>
                    </div>
                </div>
            </div>

            <!-- 3. Minimal OPC UA Client -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">🔌 Minimal OPC UA Client</span>
                    <span class=""pill"" id=""pillOpc""><span class=""pill-dot""></span> Connected</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">Endpoint URL</span><span class=""data-val"" id=""valOpcEndpoint"">opc.tcp://127.0.0.1:4840</span></div>
                    <div class=""data-row""><span class=""data-label"">Driver Mode</span><span class=""data-val"" id=""valOpcMode"">Virtual Industrial Simulator</span></div>
                    <div class=""tag-grid"" id=""opcNodes"">
                        <div class=""tag-item""><div class=""k"">Line01.DriveSpeed</div><div class=""v"" id=""nodeSpeed"">1480.0 RPM</div></div>
                        <div class=""tag-item""><div class=""k"">Line01.MotorCurrent</div><div class=""v"" id=""nodeCurrent"">12.4 A</div></div>
                        <div class=""tag-item""><div class=""k"">Line01.QualityOk</div><div class=""v"" id=""nodeQuality"">True</div></div>
                        <div class=""tag-item""><div class=""k"">Line01.PartCount</div><div class=""v"" id=""nodeCount"">2450</div></div>
                    </div>
                </div>
            </div>

            <!-- 4. Reporting Trigger Engine -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">🎯 Reporting Trigger Engine</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">Active Triggers</span><span class=""data-val"">Heartbeat, Threshold, StateChange, OnDemand</span></div>
                    <div class=""data-row""><span class=""data-label"">Total Evaluations</span><span class=""data-val"" id=""valTrigEvals"">0</span></div>
                    <div class=""data-row""><span class=""data-label"">Reports Dispatched</span><span class=""data-val"" id=""valTrigDispatches"">0</span></div>
                    <div class=""data-row""><span class=""data-label"">Last Trigger Reason</span><span class=""data-val"" id=""valTrigReason"" style=""color: #a5b4fc;"">Heartbeat interval</span></div>
                </div>
            </div>
        </div>

        <!-- Configuration Settings Card -->
        <div class=""card"">
            <div class=""card-header"">
                <span class=""card-title"">⚙️ Daemon Connection &amp; Core Settings</span>
            </div>
            <div class=""card-body"">
                <div class=""alert-box"" id=""statusAlert"">
                    Agent running. All industrial OT subsystems active. Config changes take effect on save.
                </div>
                <div style=""display: grid; grid-template-columns: 1fr 1fr; gap: 1rem;"">
                    <div class=""input-group"">
                        <label>Backend URL (gRPC / HTTP)</label>
                        <input id=""inputBackendUrl"" type=""text"" />
                    </div>
                    <div class=""input-group"">
                        <label>Auth Type</label>
                        <input id=""inputAuthType"" type=""text"" readonly style=""opacity: 0.7; cursor: not-allowed;"" />
                    </div>
                </div>
                <button class=""btn"" onclick=""saveConfig()"">Save Configuration</button>
            </div>
        </div>
    </div>

    <script>
        async function fetchStatus() {
            try {
                const res = await fetch('/api/status');
                if (!res.ok) return;
                const d = await res.json();
                
                document.getElementById('lblHostname').innerText = d.hostname || 'Heimdall Edge Node';
                document.getElementById('valHost').innerText = d.hostname || '—';
                document.getElementById('valUuid').innerText = d.machineIdentifier || '—';
                document.getElementById('valMac').innerText = d.macAddress || '—';
                document.getElementById('valOs').innerText = d.osVersion || '—';
                document.getElementById('valHardware').innerText = d.hardware ? `${d.hardware.cpu || ''} (${d.hardware.ram || ''})` : '—';
                
                if (d.ads) {
                    document.getElementById('valNetId').innerText = `${d.ads.amsNetId}:${d.ads.amsPort}`;
                    document.getElementById('valAdsState').innerText = d.ads.state || 'ADSSTATE_RUN (5)';
                    document.getElementById('valAdsState').style.color = d.ads.state === 'RUN' ? '#10b981' : '#f43f5e';
                    document.getElementById('valAdsReq').innerText = d.ads.requestsHandled || 0;
                    if (d.ads.symbols) {
                        document.getElementById('tagCycle').innerText = d.ads.symbols['MAIN.CycleCounter'] ?? '—';
                        document.getElementById('tagTemp').innerText = (d.ads.symbols['MAIN.TemperatureDegC'] ?? '—') + ' °C';
                        document.getElementById('tagPress').innerText = (d.ads.symbols['MAIN.PressureBar'] ?? '—') + ' bar';
                        document.getElementById('tagParts').innerText = d.ads.symbols['MAIN.PartsProduced'] ?? '—';
                    }
                }

                if (d.opc) {
                    document.getElementById('valOpcEndpoint').innerText = d.opc.endpointUrl;
                    document.getElementById('valOpcMode').innerText = d.opc.isSimulated ? 'Virtual Industrial Simulator' : 'Live OPC UA Server';
                    if (d.opc.nodes) {
                        document.getElementById('nodeSpeed').innerText = (d.opc.nodes['ns=2;s=Line01.DriveSpeed'] ?? '—') + ' RPM';
                        document.getElementById('nodeCurrent').innerText = (d.opc.nodes['ns=2;s=Line01.MotorCurrent'] ?? '—') + ' A';
                        document.getElementById('nodeQuality').innerText = d.opc.nodes['ns=2;s=Line01.QualityOk'] ? 'Yield OK' : 'Reject Alert';
                        document.getElementById('nodeCount').innerText = d.opc.nodes['ns=2;s=Line01.PartCount'] ?? '—';
                    }
                }

                if (d.triggers) {
                    document.getElementById('valTrigEvals').innerText = d.triggers.totalEvaluations || 0;
                    document.getElementById('valTrigDispatches').innerText = d.triggers.totalReports || 0;
                    document.getElementById('valTrigReason').innerText = d.triggers.lastReason || 'None';
                }
            } catch (e) { console.debug(e); }
        }

        async function triggerReport() {
            try {
                const res = await fetch('/api/trigger-report', { method: 'POST' });
                if (res.ok) {
                    alert('Telemetry report dispatched successfully!');
                    fetchStatus();
                }
            } catch (e) { alert('Error triggering telemetry: ' + e); }
        }

        async function toggleAdsState() {
            try {
                const res = await fetch('/api/ads/toggle', { method: 'POST' });
                if (res.ok) {
                    fetchStatus();
                }
            } catch (e) { alert('Error toggling ADS state: ' + e); }
        }

        async function loadConfig() {
            try {
                const res = await fetch('/api/config');
                const config = await res.json();
                document.getElementById('inputBackendUrl').value = config.backendUrl || '';
                document.getElementById('inputAuthType').value = config.authType || 'NoAuth';
            } catch (e) {}
        }

        async function saveConfig() {
            const backendUrl = document.getElementById('inputBackendUrl').value;
            try {
                const res = await fetch('/api/config', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ backendUrl })
                });
                if (res.ok) alert('Configuration saved!');
                else alert('Error saving configuration');
            } catch (e) { alert('Error saving: ' + e); }
        }

        loadConfig();
        fetchStatus();
        setInterval(fetchStatus, 2000);
    </script>
</body>
</html>", "text/html"));

// Diagnostics and Status API
app.MapGet("/api/status", (
    SystemInfoService sysService,
    AdsSimulationServer adsServer,
    MinimalOpcServer opcServer,
    MinimalOpcClient opcClient,
    TelemetryTriggerEngine triggerEngine,
    ConfigurationService configService) =>
{
    var sysInfo = sysService.GetSystemInfo();
    return Results.Ok(new
    {
        hostname = sysInfo.Hostname,
        machineIdentifier = sysInfo.MachineIdentifier,
        macAddress = sysInfo.MacAddress,
        osVersion = sysInfo.Software.OsVersion,
        hardware = new { cpu = sysInfo.Hardware.Cpu, ram = sysInfo.Hardware.Ram },
        disk = sysInfo.Disk,
        backendUrl = configService.Config.BackendUrl,
        ads = new
        {
            amsNetId = adsServer.AmsNetId,
            amsPort = adsServer.AmsPort,
            port = adsServer.Port,
            state = adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN ? "RUN" :
                    adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_STOP ? "STOP" :
                    $"State_{adsServer.CurrentAdsState}",
            requestsHandled = adsServer.TotalRequestsHandled,
            symbols = adsServer.SimulatedVariables
        },
        opc = new
        {
            serverPort = opcServer.Port,
            serverIsListening = opcServer.IsListening,
            serverConnectionsHandled = opcServer.TotalConnectionsHandled,
            endpointUrl = opcClient.EndpointUrl,
            isConnected = opcClient.IsConnected,
            isSimulated = opcClient.IsSimulatedMode,
            nodes = opcClient.MonitoredNodes
        },
        triggers = new
        {
            totalEvaluations = triggerEngine.TotalEvaluations,
            totalReports = triggerEngine.TotalTriggeredReports,
            lastReason = triggerEngine.LastEvaluationResult?.Reason ?? "None"
        }
    });
});

// Trigger immediate telemetry report endpoint
app.MapPost("/api/trigger-report", (Worker worker) =>
{
    worker.RequestImmediateReport("Manual on-demand trigger requested from agent web dashboard");
    return Results.Ok(new { success = true, message = "Immediate telemetry dispatch requested." });
});

// Toggle TwinCAT ADS Run/Stop state endpoint
app.MapPost("/api/ads/toggle", (AdsSimulationServer adsServer, Worker worker) =>
{
    adsServer.CurrentAdsState = adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN
        ? AdsSimulationServer.ADSSTATE_STOP
        : AdsSimulationServer.ADSSTATE_RUN;

    worker.RequestImmediateReport($"ADS State changed to {(adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN ? "RUN" : "STOP")}");
    return Results.Ok(new { success = true, state = adsServer.CurrentAdsState });
});

app.MapGet("/api/config", (ConfigurationService configService) => configService.Config);

app.MapPost("/api/config", (ConfigurationService configService, AgentConfig newConfig) => {
    var config = configService.Config;
    config.BackendUrl = newConfig.BackendUrl;
    configService.SaveConfig(config);
    return Results.Ok();
});

App.Agent.Daemon.Extensions.ExtensionApiEndpoints.MapExtensionApi(app);

app.Run();

namespace App.Agent.Daemon
{
    public partial class Program { }
}
