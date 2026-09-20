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
using App.Contracts.Configuration;
using App.Agent.Daemon.Configuration;

// Load .env file
Env.Load();

// Enable cleartext HTTP/2 (h2c) support for gRPC client connections to backend
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var featureFlags = builder.Configuration.ToAgentFeatureFlags(builder.Environment.EnvironmentName);
builder.Services.AddSingleton(featureFlags);

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

builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.IMqttAgentClient, App.Agent.Daemon.Infrastructure.MqttAgentClient>();
builder.Services.AddSingleton<App.Agent.Daemon.Infrastructure.MqttAgentClient>(sp => (App.Agent.Daemon.Infrastructure.MqttAgentClient)sp.GetRequiredService<App.Agent.Daemon.Interfaces.IMqttAgentClient>());

builder.Services.AddSingleton<App.Agent.Daemon.Interfaces.ISystemInfoReporter, SystemInfoReporter>();
builder.Services.AddSingleton<SystemInfoReporter>(sp => (SystemInfoReporter)sp.GetRequiredService<App.Agent.Daemon.Interfaces.ISystemInfoReporter>());

builder.Services.AddSingleton<App.Agent.Daemon.Infrastructure.Beckhoff.IAdsMemoryReporter, App.Agent.Daemon.Infrastructure.Beckhoff.AdsMemoryReporter>();

// Industrial OT Subsystems
builder.Services.AddSingleton<IAdsSimulationServer, AdsSimulationServer>();
builder.Services.AddSingleton<AdsSimulationServer>(sp => (AdsSimulationServer)sp.GetRequiredService<IAdsSimulationServer>());
builder.Services.AddSingleton<IMinimalOpcServer, MinimalOpcServer>();
builder.Services.AddSingleton<MinimalOpcServer>(sp => (MinimalOpcServer)sp.GetRequiredService<IMinimalOpcServer>());
builder.Services.AddSingleton<IMinimalOpcClient, MinimalOpcClient>();
builder.Services.AddSingleton<MinimalOpcClient>(sp => (MinimalOpcClient)sp.GetRequiredService<IMinimalOpcClient>());
builder.Services.AddSingleton<ITelemetryTriggerEngine, TelemetryTriggerEngine>();
builder.Services.AddSingleton<TelemetryTriggerEngine>(sp => (TelemetryTriggerEngine)sp.GetRequiredService<ITelemetryTriggerEngine>());

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
    <title>Heimdall Industrial Edge Node</title>
    <style>
        :root {
            --bg-base: #0c0e12;
            --bg-card: #15181e;
            --bg-card-header: #1e232b;
            --border: #232730;
            --text-primary: #f4f5f6;
            --text-secondary: #8b949e;
            --accent-steel: #495464;
            --accent-sage: #57715b;
            --accent-emerald: #10b981;
            --accent-amber: #a37238;
            --accent-rose: #992828;
            --code-bg: #0d1117;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background: var(--bg-base);
            color: var(--text-primary);
            padding: 1.5rem;
            line-height: 1.5;
        }
        .container { max-width: 1280px; margin: 0 auto; }
        header {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding-bottom: 1.25rem;
            border-bottom: 1px solid var(--border);
            margin-bottom: 1.5rem;
            flex-wrap: wrap;
            gap: 1rem;
        }
        .brand { display: flex; align-items: center; gap: 0.85rem; }
        .logo-box {
            width: 44px; height: 44px; border-radius: 10px;
            background: rgba(73, 84, 100, 0.25); border: 1px solid rgba(73, 84, 100, 0.5);
            display: flex; align-items: center; justify-content: center;
            font-weight: 900; color: #cbd5e1; font-size: 1.3rem; letter-spacing: -0.05em;
        }
        .title h1 { font-size: 1.25rem; font-weight: 800; letter-spacing: -0.025em; color: #ffffff; }
        .title p { font-size: 0.75rem; color: var(--text-secondary); text-transform: uppercase; font-weight: 700; letter-spacing: 0.05em; }
        .header-actions { display: flex; gap: 0.6rem; align-items: center; flex-wrap: wrap; }
        .pill {
            display: inline-flex; align-items: center; gap: 0.4rem;
            font-size: 0.7rem; font-weight: 700; text-transform: uppercase;
            padding: 0.25rem 0.65rem; border-radius: 9999px;
            background: rgba(87, 113, 91, 0.2); color: #86efac; border: 1px solid rgba(87, 113, 91, 0.4);
        }
        .pill-dot { width: 7px; height: 7px; border-radius: 50%; background: #4ade80; }
        .pill-muted {
            background: rgba(73, 84, 100, 0.2); color: #94a3b8; border: 1px solid rgba(73, 84, 100, 0.4);
        }
        .pill-muted .pill-dot { background: #64748b; }
        .pill-amber {
            background: rgba(163, 114, 56, 0.2); color: #fcd34d; border: 1px solid rgba(163, 114, 56, 0.4);
        }
        .pill-amber .pill-dot { background: #f59e0b; }
        .grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(380px, 1fr));
            gap: 1.25rem;
            margin-bottom: 1.5rem;
        }
        .card {
            background: var(--bg-card);
            border: 1px solid var(--border);
            border-radius: 12px;
            overflow: hidden;
        }
        .card-header {
            padding: 0.85rem 1.15rem;
            background: var(--bg-card-header);
            border-bottom: 1px solid var(--border);
            display: flex; align-items: center; justify-content: space-between;
        }
        .card-title { font-size: 0.8rem; font-weight: 800; text-transform: uppercase; letter-spacing: 0.05em; color: #e2e8f0; }
        .card-body { padding: 1.15rem; font-size: 0.8125rem; }
        .data-row {
            display: flex; justify-content: space-between; align-items: center;
            padding: 0.35rem 0; border-bottom: 1px solid rgba(35, 39, 48, 0.8);
        }
        .data-row:last-child { border-bottom: none; }
        .data-label { color: var(--text-secondary); font-size: 0.75rem; text-transform: uppercase; font-weight: 700; }
        .data-val { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-weight: 600; color: #cbd5e1; }
        .btn {
            background: var(--accent-steel); color: white; border: none;
            padding: 0.45rem 0.85rem; border-radius: 6px; font-size: 0.78rem; font-weight: 700;
            cursor: pointer; transition: all 0.15s; display: inline-flex; align-items: center; gap: 0.4rem;
        }
        .btn:hover { background: #374151; }
        .btn-outline {
            background: transparent; border: 1px solid var(--border); color: var(--text-primary);
        }
        .btn-outline:hover { background: rgba(35, 39, 48, 0.6); }
        .btn-sm { padding: 0.25rem 0.55rem; font-size: 0.72rem; }
        .tag-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 0.5rem; margin-top: 0.75rem; }
        .tag-item {
            background: var(--code-bg); border: 1px solid var(--border);
            padding: 0.4rem 0.6rem; border-radius: 6px; font-family: monospace; font-size: 0.75rem;
        }
        .tag-item .k { color: var(--text-secondary); font-size: 0.65rem; text-transform: uppercase; }
        .tag-item .v { font-weight: 700; color: #93c5fd; }
        .input-group { margin-bottom: 0.85rem; }
        .input-group label { display: block; font-size: 0.72rem; font-weight: 700; text-transform: uppercase; color: var(--text-secondary); margin-bottom: 0.3rem; }
        .input-group input {
            width: 100%; padding: 0.5rem 0.7rem; background: #0c0e12;
            border: 1px solid var(--border); border-radius: 6px; color: white; font-size: 0.8125rem; font-family: monospace;
        }
        .alert-box {
            padding: 0.65rem 0.9rem; border-radius: 6px; background: rgba(73, 84, 100, 0.15);
            border: 1px solid rgba(73, 84, 100, 0.3); font-size: 0.75rem; color: #cbd5e1; margin-bottom: 1rem;
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
                    <p id=""lblSub"">Industrial Edge Daemon &bull; OT Simulator Suite</p>
                </div>
            </div>
            <div class=""header-actions"">
                <span class=""pill"" id=""badgeDaemon""><span class=""pill-dot""></span> <span id=""lblDaemonStatus"">Online</span></span>
                <span class=""pill pill-muted"" id=""badgeDevMode""><span class=""pill-dot""></span> <span id=""lblDevMode"">Production</span></span>
                <button class=""btn btn-sm btn-outline"" onclick=""triggerReport()"">&#9654; Dispatch Telemetry</button>
            </div>
        </header>

        <div class=""grid"">
            <!-- 1. Node Identity -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">Host Environment</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">Hostname</span><span class=""data-val"" id=""valHost"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">Machine UUID</span><span class=""data-val"" id=""valUuid"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">MAC Address</span><span class=""data-val"" id=""valMac"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">OS Runtime</span><span class=""data-val"" id=""valOs"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">Cores / RAM</span><span class=""data-val"" id=""valHardware"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">Disk Free / Total</span><span class=""data-val"" id=""valDisk"">—</span></div>
                </div>
            </div>

            <!-- 2. Beckhoff TwinCAT ADS Simulation -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">TwinCAT ADS Subsystem</span>
                    <button class=""btn btn-sm btn-outline"" id=""btnAdsToggle"" onclick=""toggleAdsState()"">Toggle State</button>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">AMS Net ID</span><span class=""data-val"" id=""valNetId"">5.80.201.44.1.1:851</span></div>
                    <div class=""data-row""><span class=""data-label"">Runtime State</span><span class=""data-val"" id=""valAdsState"" style=""color: #86efac;"">RUN</span></div>
                    <div class=""data-row""><span class=""data-label"">Requests Handled</span><span class=""data-val"" id=""valAdsReq"">0</span></div>
                    <div class=""tag-grid"" id=""adsTags"">
                        <div class=""tag-item""><div class=""k"">MAIN.CycleCounter</div><div class=""v"" id=""tagCycle"">—</div></div>
                        <div class=""tag-item""><div class=""k"">MAIN.TemperatureDegC</div><div class=""v"" id=""tagTemp"">—</div></div>
                        <div class=""tag-item""><div class=""k"">MAIN.PressureBar</div><div class=""v"" id=""tagPress"">—</div></div>
                        <div class=""tag-item""><div class=""k"">MAIN.PartsProduced</div><div class=""v"" id=""tagParts"">—</div></div>
                    </div>
                </div>
            </div>

            <!-- 3. Minimal OPC UA Client & Server -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">Minimal OPC UA Subsystem</span>
                    <span class=""pill"" id=""pillOpc""><span class=""pill-dot""></span> Connected</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">Endpoint URL</span><span class=""data-val"" id=""valOpcEndpoint"">opc.tcp://127.0.0.1:4840</span></div>
                    <div class=""data-row""><span class=""data-label"">Driver Mode</span><span class=""data-val"" id=""valOpcMode"">Virtual Simulator</span></div>
                    <div class=""tag-grid"" id=""opcNodes"">
                        <div class=""tag-item""><div class=""k"">Line01.DriveSpeed</div><div class=""v"" id=""nodeSpeed"">1480.0 RPM</div></div>
                        <div class=""tag-item""><div class=""k"">Line01.MotorCurrent</div><div class=""v"" id=""nodeCurrent"">12.4 A</div></div>
                        <div class=""tag-item""><div class=""k"">Line01.QualityOk</div><div class=""v"" id=""nodeQuality"">Yield OK</div></div>
                        <div class=""tag-item""><div class=""k"">Line01.PartCount</div><div class=""v"" id=""nodeCount"">2450</div></div>
                    </div>
                </div>
            </div>

            <!-- 4. MQTT Client & Offline Spooler -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">Edge Egress &amp; Spool Buffer</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">MQTT Transport</span><span class=""data-val"" id=""valMqttStatus"">Active</span></div>
                    <div class=""data-row""><span class=""data-label"">Broker Address</span><span class=""data-val"" id=""valMqttBroker"">—</span></div>
                    <div class=""data-row""><span class=""data-label"">Spooled Telemetry</span><span class=""data-val"" id=""valSpoolPending"">0 queued</span></div>
                    <div class=""data-row""><span class=""data-label"">Extensions Active</span><span class=""data-val"" id=""valExtensions"">0</span></div>
                    <div class=""data-row""><span class=""data-label"">Installed Plugins</span><span class=""data-val"" id=""valPlugins"">0</span></div>
                </div>
            </div>

            <!-- 5. Reporting Trigger Engine -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">Reporting Trigger Engine</span>
                </div>
                <div class=""card-body"">
                    <div class=""data-row""><span class=""data-label"">Active Triggers</span><span class=""data-val"">Heartbeat, Threshold, StateChange</span></div>
                    <div class=""data-row""><span class=""data-label"">Total Evaluations</span><span class=""data-val"" id=""valTrigEvals"">0</span></div>
                    <div class=""data-row""><span class=""data-label"">Reports Dispatched</span><span class=""data-val"" id=""valTrigDispatches"">0</span></div>
                    <div class=""data-row""><span class=""data-label"">Last Trigger Reason</span><span class=""data-val"" id=""valTrigReason"" style=""color: #cbd5e1;"">Heartbeat interval</span></div>
                </div>
            </div>

            <!-- 6. Daemon Connection Settings -->
            <div class=""card"">
                <div class=""card-header"">
                    <span class=""card-title"">Connection Settings</span>
                </div>
                <div class=""card-body"">
                    <div class=""alert-box"" id=""statusAlert"">
                        Configuration changes are persisted locally to agentconfig.json.
                    </div>
                    <div class=""input-group"">
                        <label>Backend URL (MQTT / HTTP)</label>
                        <input id=""inputBackendUrl"" type=""text"" />
                    </div>
                    <div class=""input-group"">
                        <label>Auth Type</label>
                        <input id=""inputAuthType"" type=""text"" readonly style=""opacity: 0.6; cursor: not-allowed;"" />
                    </div>
                    <button class=""btn"" onclick=""saveConfig()"">Save Configuration</button>
                </div>
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
                document.getElementById('valHardware').innerText = d.hardware ? (d.hardware.cpu || '') + ' (' + (d.hardware.ram || '') + ')' : '—';
                if (d.disk) {
                    document.getElementById('valDisk').innerText = (d.disk.free || '—') + ' / ' + (d.disk.total || '—');
                }
                
                // Mode badges
                const devBadge = document.getElementById('badgeDevMode');
                const lblDev = document.getElementById('lblDevMode');
                if (d.devFeaturesEnabled) {
                    devBadge.className = 'pill pill-amber';
                    lblDev.innerText = 'Dev Mode Active';
                } else {
                    devBadge.className = 'pill pill-muted';
                    lblDev.innerText = 'Production Mode';
                }

                if (d.ads) {
                    document.getElementById('valNetId').innerText = (d.ads.amsNetId || '') + ':' + (d.ads.amsPort || 851);
                    document.getElementById('valAdsState').innerText = d.ads.state || 'RUN';
                    document.getElementById('valAdsState').style.color = d.ads.state === 'RUN' ? '#86efac' : '#f87171';
                    document.getElementById('valAdsReq').innerText = d.ads.requestsHandled || 0;
                    if (d.ads.symbols) {
                        document.getElementById('tagCycle').innerText = d.ads.symbols['MAIN.CycleCounter'] ?? '—';
                        document.getElementById('tagTemp').innerText = (d.ads.symbols['MAIN.TemperatureDegC'] ?? '—') + ' °C';
                        document.getElementById('tagPress').innerText = (d.ads.symbols['MAIN.PressureBar'] ?? '—') + ' bar';
                        document.getElementById('tagParts').innerText = d.ads.symbols['MAIN.PartsProduced'] ?? '—';
                    }
                }

                if (d.opc) {
                    document.getElementById('valOpcEndpoint').innerText = d.opc.endpointUrl || 'opc.tcp://127.0.0.1:4840';
                    document.getElementById('valOpcMode').innerText = d.opc.isSimulated ? 'Virtual Simulator' : 'Live Industrial Server';
                    if (d.opc.nodes) {
                        document.getElementById('nodeSpeed').innerText = (d.opc.nodes['ns=2;s=Line01.DriveSpeed'] ?? '—') + ' RPM';
                        document.getElementById('nodeCurrent').innerText = (d.opc.nodes['ns=2;s=Line01.MotorCurrent'] ?? '—') + ' A';
                        document.getElementById('nodeQuality').innerText = d.opc.nodes['ns=2;s=Line01.QualityOk'] ? 'Yield OK' : 'Reject Alert';
                        document.getElementById('nodeCount').innerText = d.opc.nodes['ns=2;s=Line01.PartCount'] ?? '—';
                    }
                }

                if (d.mqtt) {
                    document.getElementById('valMqttStatus').innerText = d.mqtt.isConnected ? 'Connected' : 'Disconnected';
                    document.getElementById('valMqttBroker').innerText = d.mqtt.brokerHost + ':' + d.mqtt.brokerPort;
                }

                if (d.spooler) {
                    document.getElementById('valSpoolPending').innerText = (d.spooler.pendingRecords || 0) + ' queued';
                }

                if (d.extensionsCount !== undefined) {
                    document.getElementById('valExtensions').innerText = d.extensionsCount + ' active';
                }
                if (d.pluginsCount !== undefined) {
                    document.getElementById('valPlugins').innerText = d.pluginsCount + ' installed';
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
                    fetchStatus();
                }
            } catch (e) { console.error('Error triggering telemetry:', e); }
        }

        async function toggleAdsState() {
            try {
                const res = await fetch('/api/ads/toggle', { method: 'POST' });
                if (res.ok) {
                    fetchStatus();
                }
            } catch (e) { console.error('Error toggling ADS state:', e); }
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
                if (res.ok) {
                    alert('Configuration saved successfully.');
                }
            } catch (e) { alert('Error saving: ' + e); }
        }

        loadConfig();
        fetchStatus();
        setInterval(fetchStatus, 2500);
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
    ConfigurationService configService,
    AgentFeatureFlags flags,
    App.Agent.Daemon.Extensions.IExtensionRegistry extensionRegistry,
    App.Agent.Daemon.Infrastructure.Plugins.IPluginManager pluginManager,
    App.Agent.Daemon.Interfaces.IMqttAgentClient mqttClient,
    App.Agent.Daemon.Interfaces.ITelemetrySpooler spooler) =>
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
        devFeaturesEnabled = flags.EnableDevFeatures,
        debugFeaturesEnabled = flags.EnableDebugFeatures,
        ads = flags.EnableDevFeatures ? new
        {
            amsNetId = adsServer.AmsNetId,
            amsPort = adsServer.AmsPort,
            port = adsServer.Port,
            state = adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_RUN ? "RUN" :
                    adsServer.CurrentAdsState == AdsSimulationServer.ADSSTATE_STOP ? "STOP" :
                    $"State_{adsServer.CurrentAdsState}",
            requestsHandled = adsServer.TotalRequestsHandled,
            symbols = flags.EnableDebugFeatures ? (IReadOnlyDictionary<string, object>)adsServer.SimulatedVariables : new Dictionary<string, object>()
        } : null,
        opc = flags.EnableDevFeatures ? new
        {
            serverPort = opcServer.Port,
            serverIsListening = opcServer.IsListening,
            serverConnectionsHandled = opcServer.TotalConnectionsHandled,
            endpointUrl = opcClient.EndpointUrl,
            isConnected = opcClient.IsConnected,
            isSimulated = opcClient.IsSimulatedMode,
            nodes = flags.EnableDebugFeatures ? opcClient.MonitoredNodes : (IReadOnlyDictionary<string, object>)new Dictionary<string, object>()
        } : new
        {
            serverPort = 0,
            serverIsListening = false,
            serverConnectionsHandled = 0L,
            endpointUrl = opcClient.EndpointUrl,
            isConnected = opcClient.IsConnected,
            isSimulated = false,
            nodes = flags.EnableDebugFeatures ? opcClient.MonitoredNodes : (IReadOnlyDictionary<string, object>)new Dictionary<string, object>()
        },
        mqtt = new
        {
            isConnected = mqttClient.IsConnected,
            brokerHost = mqttClient.BrokerHost,
            brokerPort = mqttClient.BrokerPort
        },
        spooler = new
        {
            pendingRecords = spooler.PendingCount
        },
        extensionsCount = extensionRegistry.GetActiveComponents().Count,
        pluginsCount = pluginManager.GetInstalledPlugins().Count,
        triggers = new
        {
            totalEvaluations = triggerEngine.TotalEvaluations,
            totalReports = triggerEngine.TotalTriggeredReports,
            lastReason = triggerEngine.LastEvaluationResult?.Reason ?? "None"
        }
    });
});

// Verbose diagnostic dump endpoint (guarded strictly by EnableDebugFeatures)
app.MapGet("/api/diagnostics/dump", (
    SystemInfoService sysService,
    AdsSimulationServer adsServer,
    MinimalOpcServer opcServer,
    MinimalOpcClient opcClient,
    TelemetryTriggerEngine triggerEngine,
    ConfigurationService configService,
    AgentFeatureFlags flags) =>
{
    if (!flags.EnableDebugFeatures)
    {
        return Results.Json(
            new { error = "Diagnostic dump routines are disabled in production mode. Set HEIMDALL_ENABLE_DEBUG=true to enable." },
            statusCode: StatusCodes.Status403Forbidden);
    }

    var sysInfo = sysService.GetSystemInfo();
    return Results.Ok(new
    {
        timestampUtc = DateTime.UtcNow,
        system = sysInfo,
        configuration = configService.Config,
        adsSimulation = flags.EnableDevFeatures ? new
        {
            adsServer.Port,
            adsServer.AmsNetId,
            adsServer.AmsPort,
            adsServer.CurrentAdsState,
            adsServer.TotalRequestsHandled,
            Variables = adsServer.SimulatedVariables
        } : null,
        opcSimulation = new
        {
            opcServer.Port,
            opcServer.IsListening,
            opcServer.TotalConnectionsHandled,
            opcClient.EndpointUrl,
            opcClient.IsConnected,
            opcClient.IsSimulatedMode,
            Nodes = opcClient.MonitoredNodes
        },
        triggers = new
        {
            triggerEngine.TotalEvaluations,
            triggerEngine.TotalTriggeredReports,
            LastResult = triggerEngine.LastEvaluationResult
        }
    });
});

// Trigger immediate telemetry report endpoint
app.MapPost("/api/trigger-report", (Worker worker) =>
{
    worker.RequestImmediateReport("Manual on-demand trigger requested from agent web dashboard");
    return Results.Ok(new { success = true, message = "Immediate telemetry dispatch requested." });
});

// Toggle TwinCAT ADS Run/Stop state endpoint (guarded strictly by EnableDevFeatures)
app.MapPost("/api/ads/toggle", (AdsSimulationServer adsServer, Worker worker, AgentFeatureFlags flags) =>
{
    if (!flags.EnableDevFeatures)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

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
