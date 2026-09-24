# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Extended Tag & Stored Value Browser in FMFD OmniSearch**:
  - Integrated an interactive **Browse Tags & Stored Values** tab in `AutoTagSuggestionDropdown.vue` alongside the traditional results autocomplete.
  - Added live category filtering, stored value counts, dedicated stored values grid with live query matching, and one-click insertion of tag pills (`tag:value`) into active search bars.
  - Added global cross-category matching of stored values directly within the omni-search input bar.
- **Color-Mode Awareness & Dark/Light Theme System-Wide Modernization**:
  - System-wide modernization of all frontend components (`pages/`, `components/analytics`, `components/tickets`, `components/dashboard`, `components/controllers`, `components/layout`, `components/admin`, `components/ui`) to be fully color-mode aware.
  - Replaced hardcoded dark palette classes (`bg-zinc-900`, `bg-slate-900`, `text-zinc-100`, etc.) with adaptive Tailwind CSS semantic tokens (`bg-card`, `bg-background`, `text-foreground`, `text-muted-foreground`, `border-border`, `bg-muted`) ensuring WCAG AA contrast compliance across both dark and light modes.
- **Customizable Close Controls for Modal Dialogs & Sheets**:
  - Added `showClose` (default: `true`) and `hideClose` (default: `false`) props to `DialogContent.vue`, `DialogScrollContent.vue`, and `SheetContent.vue` with `data-slot="dialog-close"`.
  - Allowed custom dialogs to disable default absolute-positioned close buttons when custom header close buttons or shortcuts are present.

### Fixed
- **Modal 'X' Close Button Overlays & Duplicate Close Icons**:
  - Eliminated awkward and duplicate 'X' button overlays across all dialog and sheet components (`GlobalOmniSearchModal`, `PreferredTechniciansModal`, `RemoteQuickViewModal`, `StationComponentTreeModal`, `AssetTabbedEditor`, `ClientDetailsModal`, `MapPinningDialog`, `ControllerCommandModal`, `MachineQrModal`, `organizations.vue`, `CommandDialog.vue`).
  - Aligned custom header actions cleanly alongside dedicated close buttons and removed overlaps with helper hints like "Press ESC to exit".
- **CTRL + K Hotkey Flickering, Rapid Keydown & Spamming**:
  - Refactored `useGlobalSearchModal.ts` with a dedicated `triggerSearch()` method and a `300ms` state change debounce timeout.
  - Prevented modal toggling/flickering on key repeats or button spamming when `CTRL + K` is pressed while already open, instead refocusing the input and selecting all query text via `focusTriggerSignal`.
- **Page Re-paint & Reactive DOM Replacement Search Dismissals**:
  - Guarded click-outside and pointer-down-outside handlers in `GlobalOmniSearchModal.vue` and `OmniSearchBar.vue` against detached DOM nodes (`!document.body.contains(target)`).
  - Ensured background telemetry polling, SSE updates, and reactive component re-renders do not prematurely dismiss or break the active search popup.
- **Independent Standalone Packaging Infrastructure**:
  - **Self-Contained Edge Agent Packaging**:
    - Packaged `App.Agent.Daemon` as a single-file, self-contained executable for `linux-x64` and `win-x64` with zero external runtime prerequisites.
    - Integrated native Linux systemd service notification and watchdog integration (`Microsoft.Extensions.Hosting.Systemd` / `builder.Host.UseSystemd()`) alongside native Windows Services (`UseWindowsService()`).
    - Provided production systemd unit file `packaging/agent/heimdall-agent.service` with strict sandboxing and unprivileged service user execution.
    - Created automated service installer and uninstaller scripts for Linux (`install-service.sh`, `uninstall-service.sh`) and Windows PowerShell (`install-service.ps1`, `uninstall-service.ps1`).
    - Bundled template environment configuration (`packaging/agent/agent.env.example`) and JSON configuration (`packaging/agent/default-config.json`).
  - **Multi-Stage Production Containerization**:
    - Authored multi-stage production Dockerfiles with non-root security:
      - `agent/Dockerfile` (minimal ASP.NET 10 runtime, non-root user).
      - `backend/Dockerfile` (optimized .NET 10 Web API, non-root user).
      - `frontend/web/Dockerfile` (Bun multi-stage build, standalone Nitro server).
    - Created turnkey `docker-compose.prod.yml` enabling completely independent, single-command production deployment with PostgreSQL, Redis, MQTT broker, Backend API, Frontend Web UI, and optional Agent profile.
  - **Packaging Automation CLI**:
    - Created `scripts/package.sh` and cross-platform `scripts/package.py` automating the assembly of standalone distributions and compressed archives (`.tar.gz`, `.zip`) into `dist/`.
- **Repository-Wide Code Quality, XML Documentation & Architecture Modernization**:
  - Audited and resolved all dangling `TODO` and `FIXME` comments repository-wide.
  - Added XML documentation comments to backend controllers (`ActiveDirectoryController`, `CertificateManagementController`, `ClientPcController`, `DashboardController`).
  - Safeguarded `ActiveDirectoryController` to only fall back to built-in mock OUs when `EnableDevFeatures` is explicitly enabled.
  - Hardened `ConfigurationService` with thread-safe atomic config persistence (`.tmp` write followed by atomic `File.Move`).
  - Hardened `LocalTelemetrySpooler` with crash-resistant atomic spool writes.
  - Strengthened `ExtensionRegistry` with monotonic clock expiration tracking (`Stopwatch.GetTimestamp`) to prevent premature or stuck expirations caused by system clock adjustments.
  - Refactored `SetupApiNative` to document Windows Driver Kit constants and eliminate magic numbers.
- **General UI Standardizations & Design System Uniformity**:
  - Global button cursor styling: Added `cursor-pointer` to all button variants in `frontend/web/app/components/ui/button/index.ts` and registered global `cursor: pointer` on `button` and `[role="button"]` in `frontend/web/app/assets/css/tailwind.css` `@layer base`.
  - Harmonized colors and shapes in search modal, query dropdowns, file dropzones, and station tables/modals (`StationListTable.vue`, `StationDetailModal.vue`, `ControllerCommandModal.vue`) to standard semantic design tokens (`bg-card`, `bg-muted`, `border-border`, `text-primary`, `text-foreground`, `rounded-xl`/`rounded-2xl`).
  - Replaced browser `alert()` popups in `ControllerCommandModal.vue` with inline reactive error banners (`errorMessage = ref('')`).
  - Added explicit Escape key closure handler to `GlobalOmniSearchModal.vue`.
  - Enhanced accessibility: Added `role="button"`, `tabindex="0"`, and Enter/Space keyboard event listeners to file dropzones in `RootCertImportModal.vue`.
- **Diagnostics, CLI & Shell Completion Enhancements**:
  - Added `TotalSizeBytes` metric to `ITelemetrySpooler` and implemented live byte calculation across pending spool files in `LocalTelemetrySpooler.cs`.
  - Exposed `spooler.totalSizeBytes` in the edge daemon `/api/status` diagnostics endpoint in `Program.cs`.
  - Extended developer orchestrator `run_dev.sh` with `./run_dev.sh package [agent|backend|frontend|all]` delegating directly to `scripts/package.sh`.
  - Updated Bash and Zsh shell completion scripts (`tools/completions/heimdall_completion.bash` and `tools/completions/heimdall_completion.zsh`) to support the `package` command and its targets.

### Fixed
- **Compiler & Linter Warnings Elimination**:
  - Resolved `CS8601` possible null assignment warning in `PredictiveMaintenanceService.cs` (`Description = s.DisplayName ?? s.Name ?? "Stock Component"`), bringing the backend solution to 0 warnings.
  - Guarded `onMounted` with `getCurrentInstance()` in `useShortcuts.ts` to prevent Vue lifecycle warnings when composables are imported in headless or unit-test environments.
- **Search Pop-up Premature Disappearance & Blur Race Conditions**:
  - Fixed an issue where the global omni-search popup dialog prematurely closed and triggered route changes after typing only 1 or 2 characters:
    - Root cause: `OmniSearchBar.vue` emitted debounced `@search` events on keystrokes, which `GlobalOmniSearchModal.vue` handled by immediately calling `closeModal()` and navigating to `/dashboard/inventory`.
    - Fix: Decoupled intermediate debounced search query changes from explicit submission events (`@submit`), updating `GlobalOmniSearchModal.vue` to only close and navigate on user submit or item selection.
  - Fixed search suggestion dropdown suddenly disappearing during interactive clicks or clicks on dropdown elements:
    - Added `isInteractingWithDropdown` tracking, `e.composedPath()` inspection, and `@pointer-down-outside` event filtering with `data-omni-dropdown="true"`.
- **Frontend Syntax & Production Build Crashes**:
  - Extracted and implemented strict interface-implementation models across all backend, agent, and infrastructure layers:
    - `IAdsSimulationServer` and `IAdsMemoryReporter` for Beckhoff TwinCAT ADS runtime.
    - `IMinimalOpcServer` and `IMinimalOpcClient` for binary OPC UA TCP transport.
    - `IMqttAgentClient` and `ITelemetryTriggerEngine` for industrial telemetry reporting.
    - `ISecureIndustrialFileScanner` with configurable `FileSystemScannerOptions`.
    - `ITelemetryIngestionService` and `TelemetryIngestionService` supporting both gRPC and embedded MQTT telemetry pipelines.
    - `IOpcUaGatewayService`, `IPredictiveMaintenanceService`, `IReportExportService`, and `ICopiaIntegrationService`.
    - `IMachineGroupRepository` / `MachineGroupRepository` and `ITechnicianRepository` / `TechnicianRepository`.
  - Introduced strongly-typed domain enums in `App.Contracts.Enums`: `AgentCommandType`, `PlcMemoryAccessMode`, `TelemetryIngestionChannel`, `MachineGroupCategory`, `TechnicianRoleType`, and `DiagnosticSeverityLevel`.
  - Implemented runtime feature flags and dev/debug gating across backend API endpoints and frontend composables (`featureFlags.ts`, `useFeatureFlags.ts`).
  - Added dedicated unit tests: `AgentRefactoringAndFeatureFlagsTests.cs`, `BackendFeatureFlagsAndEndpointGuardsTests.cs`, `InventoryFilterAndDomainEndpointsTests.cs`, `MqttCommsTests.cs`, `FeatureFlagsAndDevGating.test.ts`, and `ComposablesReviewAndDevGating.test.ts`.
- **Development Orchestrator & Process Lifecycle Modernization (`run_dev.sh` & `dev_layout.kdl`)**:
  - Implemented `setsid` daemon detachment for all background services (Backend, Frontend, Agent, Simulator) ensuring persistence across subshell terminations.
  - Added `kill_pid_tree()` recursive child process termination and `free_port()` with `fuser -k` guaranteeing clean socket release on ports 5099, 5001, 3000, 5055, and 5998.
  - Resolved Zellij dual-execution port conflict: `start_dev --zellij` halts background daemons and allocates ports exclusively to interactive Zellij multiplexer panes.
  - Updated `dev_layout.kdl` to dynamically detect active Windows Edge Agent containers and stream container logs instead of colliding on port 5998.

### Fixed
- **Frontend Syntax & Production Build Crashes**:
  - Fixed missing `<script setup lang="ts">` tag in `GlobalOmniSearchModal.vue` causing Rolldown/Vite compilation failures (`RolldownError: Invalid end tag`).
  - Fixed multiline string literal with raw newline in `OuCertificateRuleModal.vue` causing `SyntaxError: Unterminated string constant`.
  - Updated `nuxt.config.ts` `server.allowedHosts: true` to resolve 403 Forbidden errors when accessing dev servers via LAN IP or Docker container bridges.
  - Modernized Vite server HMR configuration to eliminate `server.hmr.protocol/clientPort` deprecation warnings in Vite 8.
  - Formatted `ensureAdminUser.ts` database connection errors into a clean, single-line deferred initialization warning when PostgreSQL is offline at boot.
- **Run Script Subshell PID Capture & Zombie Processes**:
  - Replaced subshell launches `(cd ... && nohup ...)` with direct `--cwd` and `--project` flags, preventing orphaned grandchild processes from holding port 3000 across runs.
  - Fixed `pkill` patterns in `stop_services` and `clean_environment` to properly match `@nuxt/cli`, `bun run dev`, `node.*nuxt`, and `dotnet exec.*App.Backend.Api`.

### Tests
- **Backend .NET Test Suite**: Expanded to **208 passing tests** (up from 162/165) with xUnit, in-memory SQLite, and WebApplicationFactory integration tests.
- **Frontend Vitest Test Suite**: Expanded to **298 passing unit tests across 39 test files** (up from 275/37 suites).
- **Python Fleet Suite**: 9/9 unit tests passing (`test_mock_cmi_runner.py`, `test_simulated_pc_integration.py`).
- **Unified Pipeline**: Full `./run_dev.sh test all` verification passes cleanly with exit code 0.
- **Production Build**: Verified `bun run build` completes cleanly with Nitro production preset.

### Added (Previous)
- **Real OPC UA Server in Agent Daemon (`MinimalOpcServer.cs`)**:
  - New lightweight OPC UA TCP server listening on port 4840 with full binary HEL/ACK handshake per OPC UA transport spec.
  - Tracks `ConnectionsHandled` counter; exposes `IsListening` / `ServerPort` properties.
  - Wired into `Worker.cs` lifecycle (Start/Stop/Dispose) and DI singleton registration in `Program.cs`.
  - `/api/status` endpoint now reports `opc.serverIsListening`, `opc.serverPort`, `opc.serverConnectionsHandled`.
- **Real CPU/RAM Telemetry — No Synthetic Fallbacks (`SystemInfoService.cs`)**:
  - Added `LiveTelemetryData` class: `CpuLoad` (string with `%`), `CpuUsagePercent` (double), `RamUsage` (string with `%`), `RamUsagePercent` (double), `Status`, `Timestamp`.
  - `GetRealCpuUsage()`: Windows measures via `GetSystemTimes` delta between two 250 ms samples; Linux reads `/proc/stat` delta.
  - `GetRealRamUsage()`: Windows uses `GlobalMemoryStatusEx`; Linux reads `/proc/meminfo`.
  - `GetLiveTelemetry()` assembles `LiveTelemetryData` from real OS metrics and exposes them in the inventory report.
  - `GetSoftwareConfig()` now populates `IPAddress` from active network interfaces (first non-loopback IPv4).
  - `EnsureIndustrialSoftwareRegistryKeys()`: self-seeds Windows Uninstall registry on first scan with Beckhoff TwinCAT 3.1, TwinCAT ADS Router, OPC UA Server Runtime, .NET Runtime 10, VC++ Redistributable 2015-2022, and Heimdall Industrial Edge Agent — making the machine visible as a properly provisioned OT workstation.
- **`LiveTelemetryComponentContributor` (`ComponentContributors.cs`)**:
  - Produces a `"Live Telemetry"` inventory component carrying real `CpuLoad`, `CpuUsagePercent`, `RamUsage`, `RamUsagePercent`, `Status`, and `Timestamp` fields.
  - Backend `SystemInfoCollectorService` recognises component name `"Live Telemetry"` and maps values into `clientPc.ResourceAverages`.
- **Windows OEM Registry Seeding (`setup.ps1`)**:
  - 6 OT packages (TwinCAT 3.1, ADS Router, OPC UA Server Runtime, .NET 10, VC++, Heimdall Agent) seeded into `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall` on first VM boot.

### Changed
- **`useControllers.ts`: removed all `Math.random()` synthetic telemetry** — `cpuUsagePercent` / `ramUsagePercent` now map directly from real `resourceAverages` only; `ipAddress` resolved from `systemMetadata.IPAddress`; no more DOCKERW hardcoded IP/OS defaults.
- **`useAuthSession.ts`: leaner pattern-matching role logic** — replaced hardcoded role enumerations with pattern-matching; `admin` and `it_admin` roles now granted `createUserRole` permission.
- **`SystemInfoReporter.cs`: fixed contributor merge** — always initialises all 6 base contributors (`Hardware`, `Software`, `PhysicalDrives`, `Drivers`, `Events`, `LiveTelemetry`) before merging DI-injected contributors; exposed `public Contributors` property for test introspection.
- **`MinimalOpcClient.cs`: added 1 500 ms timeout** on `stream.ReadAsync` in `TryConnectAsync` — prevents indefinite hang when OPC server is unresponsive.
- **`SystemInfoCollectorService.cs`: `IndustrialOT` component merging** — parses `IndustrialOT` inventory component and merges `AdsState`, `AdsAmsNetId`, `OpcEndpoint`, `OpcConnected` into `systemMetadata` JSON.

### Tests
- **+3 backend .NET tests** (total: 165):
  - `MinimalOpcServer_HelHandshake_ReturnsAckPacket` — HEL → ACK handshake on port 49994.
  - `LiveTelemetryComponentContributor_ProducesTelemetryComponentWithMetrics` — verifies component creation with real values.
  - `SystemInfoReporter_MergesAllBaseContributorsAndInjectedContributors` — verifies contributor merge with `NullLogger`.
- All **275 frontend Vitest tests** continue to pass.

### Added
- **Interactive Development Workspace TUI (`tools/tui.py` & `run_dev.sh`)**:
  - Live, reactive full-screen terminal user interface dashboard launched by default on `./run_dev.sh` (staying alive in foreground).
  - Real-time service topology matrix (Postgres, Redis, Backend REST, gRPC stream, Nuxt frontend, Linux agent, Fleet simulator, Windows container, TwinCAT ADS, OPC UA).
  - Split live log streamer with per-service buffers, colorized logging, auto-scrolling, and fullscreen log toggle (`[l]`).
  - Interactive hotkeys for service start/stop (`[s]`), service restart with hot-reload (`[r]`), restart all (`[R]`), toggle Windows agent (`[w]`), verification test execution (`[t]`), and safe detachment to background daemons (`[b]`).
  - Safe exit prompt (`[q]`) offering options to cleanly shut down all services or detach to background daemons.
- **Enhanced Development CLI & Windows Agent Orchestration (`run_dev.sh` & `tools/dev_manager.py`)**:
  - Full Windows 10 LTSC KVM Docker container orchestration directly in `run_dev.sh` (`./run_dev.sh windows start|stop|restart|status|logs|build|launch|test`).
  - Option to include Windows agent during startup via `./run_dev.sh start --windows` (`-w`).
  - Added port bindings `48898:48898` (TwinCAT ADS) and `4840:4840` (OPC UA) to `docker-compose.yml`.
  - Machine-readable `--json` output flag in `dev_manager.py` for automated health telemetry.
- **Deep Tab Completion System (`tools/completions/`)**:
  - Comprehensive completions for Bash and Zsh covering all commands, subcommands, service names, and flags.
  - Safe fallback for bare Bash environments lacking `_init_completion`.
  - One-shot installation via `./run_dev.sh install-completions` (or `./run_dev.sh completion install`).
- **Beckhoff TwinCAT ADS Simulation Server & Industrial Protocol Engine (`App.Agent.Daemon`)**:
  - In-memory Beckhoff TwinCAT ADS server (`AdsSimulationServer.cs`) listening on port 48898 with AMS Net ID `5.80.201.44.1.1:851`.
  - Implemented binary ADS frame decoding and response synthesis for commands `0x0001` (DeviceInfo), `0x0002` (Read), `0x0003` (Write), `0x0004` (ReadState), `0x0005` (WriteControl), and `0x0009` (ReadWrite).
  - Simulated live PLC variables (`MAIN.CycleCounter`, `MAIN.TemperatureDegC`, `MAIN.PressureBar`, `MAIN.PartsProduced`, and `MAIN.MachineRunning`) and TwinCAT 3 run/stop transitions.
  - Exposed TwinCAT ADS port `48898` in Windows Docker container (`infra/windows/Dockerfile`) and configured automated firewall and TwinCAT/EtherCAT registry provisioning in `setup.ps1`.
- **Zero-Dependency Binary OPC UA Client (`MinimalOpcClient.cs`)**:
  - Native binary OPC UA client communicating over `opc.tcp://127.0.0.1:4840` without external runtime dependencies.
  - Full binary HEL/ACK protocol negotiation, monitored node catalog (`ns=2;s=Line1.Speed`, `ns=2;s=Line1.Vibration`, `ns=2;s=Line1.Status`), and automated fallback simulation.
  - Port `4840` exposed in `infra/windows/Dockerfile` and configured in edge firewall rules.
- **Edge Reporting Trigger Engine & Bandwidth Optimization (`TelemetryTriggerEngine.cs`)**:
  - Configurable multi-condition trigger evaluation engine supporting `HeartbeatTrigger`, `ThresholdTrigger` (OS drive space, PLC temp limits), `StateChangeTrigger` (TwinCAT RUN/STOP changes), and `OnDemandTrigger`.
  - Selective telemetry slice filtering (`IncludeHardware`, `IncludeSoftware`, `IncludeDisk`, `IncludePlcTelemetry`, `IncludeEvents`), omitting static hardware/software inventories during high-frequency PLC updates to drastically reduce edge-to-cloud payload volume.
  - Contributor integration via `IndustrialOtComponentContributor.cs` seamlessly feeding hardware and live PLC slices into station inventory graphs.
- **Interactive Windows Edge Tray Runner & Modernized Agent Web View**:
  - PowerShell system tray runner (`HeimdallTrayRunner.ps1`) in `infra/windows/tray/` and OEM provisioning, supporting status checks, log viewing, daemon control, and quick web-view launch.
  - Overhauled Agent Web-View dashboard (`http://localhost:5998`) featuring an industrial dark theme, real-time status polling, live ADS RUN/STOP toggles, and manual telemetry dispatch triggers.
- **Dedicated Delegations & Machine Groups Management Hubs (`frontend/web`)**:
  - Standalone `/dashboard/delegations` page (`DelegationsManager.vue`) providing technician priority rules, absence coverage schedules, and escalation tiers.
  - Standalone `/dashboard/machine-groups` page (`MachineGroupManager.vue`) providing machine classification, production line assignments, and plant location grouping.
  - Modal wrappers `MachineGroupManagerModal.vue` and `PreferredTechniciansModal.vue` refactored to wrap managers cleanly with full backward compatibility.
- **4-Tier Grouped Navigation Sidebar (`menus.ts`)**:
  - Reorganized global sidebar into 4 logical enterprise domains: **Management** (Overview, Tickets, Delegations, Machines, Groups, Inventory, Clients, Map), **Data** (Analytics, Telemetry Hub, Live Stream, Templates, Dynamic Rules), **Maintenance** (System Status, Spooler Diagnostics, Audit Logs), and **Settings** (Access Control, Organizations, Security Groups, Governance, Help & Guide).
- **User-Defined Telemetry Metrics Registry & Cache Store**:
  - Custom telemetry metrics registry (`telemetryMetrics.ts`, `useTelemetryMetrics.ts`) with custom unit formulas, warning/critical thresholds, and cached datapoint retrieval via `/api/telemetry/metrics` and `telemetryCacheStore.ts`.
- **Automated Incident Alert Rules Engine**:
  - Configurable alert rule management (`AlertRulesManager.vue`, `useAlertRules.ts`) with condition evaluation, threshold triggers, and automatic maintenance ticket creation.
- **Zero-Dependency RFB/VNC Remote Desktop Canvas (`useRfbClient.ts`)**:
  - Web-native RFB protocol client rendering remote VNC frames directly onto HTML5 `<canvas>` elements with interactive keyboard and mouse control, integrated into `RemoteQuickViewModal.vue`.
- **3-State Column Sorting in Table Headers (`TableHead.vue`)**:
  - Added 3-state sorting cycle (Ascending -> Descending -> Restored default) for incident ticket lists and machine catalogs.
- **Cryptographically Signed Agent Plugins & Sandboxing (`App.Agent.Daemon` & `App.Backend.Api`)**:
  - Master RSA-2048 signing authority in `PluginService.cs` (`RSA-SHA256` with `Pkcs1` padding) and manifest verification in `PluginManager.cs`.
  - Fail-secure verification rejecting unsigned plugins in Production (`ErrorCode.PluginSignatureInvalid`) and sandboxing in Development (`sandboxes/{pluginId}`).
  - Sensitive environment variable scrubbing (`HEIMDALL_AGENT_KEY`, `HEIMDALL_MASTER_PRIVATE_KEY`, `HEIMDALL_ENCRYPTION_KEY`) and directory containment checking (`PathSanitizer.IsWithinRoot`).
- **Secured Edge Extension REST API & Component Hierarchy Integration**:
  - Minimal API endpoints in `ExtensionApiEndpoints.cs` (`/api/v1/agent/status`, `/api/v1/extensions/components`, `/api/v1/extensions/telemetry`, `/api/v1/extensions/events`, `/api/v1/agent/sync`) guarded by `ExtensionAuthFilter`.
  - Sensor attachment into the station inventory graph (`BaseInventoryItem.ParentId`) via `ExtensionComponentContributor.cs` and `SystemInfoCollectorService.cs`.
  - Station Component Tree modal (`StationComponentTreeModal.vue`) displaying trust badges (`[Signed]`, `[Dev Sandbox]`), technology pills, and JSON payload inspection.
- **Production-Grade Industrial Diagnostic Snapshot Engine**:
  - Full end-to-end `DiagnosticSnapshot` database entity with SHA-256 integrity hash verification (`PayloadHashSha256`), byte-precise measurement, and complete JSON diagnostic payload storage.
  - Audit logging and agent event recording for TISAX ISA 5.1 and NIS2 compliance.
  - Edge dispatch using `QueuedAgentCommand` with command name `TRIGGER_DIAGNOSTIC_SNAPSHOT`.
  - Frontend snapshot interface at `/dashboard/telemetry` with real async execution, SHA-256 hash badge, payload inspection modal, direct JSON file download, and historical snapshot drawer.
- **Statistical Process Control (SPC) KPI Goals & Grafana Mass Telemetry Workspace**:
  - Configurable SPC limits in `KpiGraphBuilder.vue` (Target Mean, Upper Control Limit UCL, Lower Control Limit LCL).
  - Radial compliance gauges, bullet charts, and trend overlays with automatic status evaluation (`Within Limits`, `UCL Exceeded`, `Below LCL`).
  - Dedicated Grafana mass telemetry embed at `/dashboard/analytics` (`GrafanaMassTelemetryEmbed.vue`) with kiosk mode, quick-copy Prometheus scrape (`/api/v1/ReportExport/grafana/metrics`), and OData stream endpoints.
- **Application Right-Click Context Menu (`GlobalContextMenu.vue` & `useGlobalContextMenu.ts`)**:
  - Application-wide context menu mounted at root with boundary-safe viewport clamping.
  - Deep domain navigation: "Go to Machine", "Go to Node / Controller", "Go to Owner Team / Person", "Go to Ticket / Report Incident", "Live Telemetry", "Inspect Component Tree".
  - Native browser context menu pass-through item and `Shift + Right-Click` bypass.
  - Integrated across CAD Map (`InteractiveMapCanvas.vue`), Controller Grid (`ControllerGrid.vue`), and Machinery Catalog (`machines.vue`).
- **Interactive Hero Metrics Filtering & Intuitive Header Resets**:
  - Hero cards on `/dashboard/tickets` (`TicketMetricsOverview.vue`) with accessible button semantics, click-to-filter, toggle-off, active colored rings, and dynamic filter chip bar.
  - Consistent page header click actions across dashboard pages (`tickets.vue`, `machines.vue`, `clients.vue`, `analytics.vue`, `map.vue`, `telemetry/stream.vue`) to reset filters/queries and refresh live state.
  - Summary badges on `machines.vue` and backlog age distribution cards on `FleetAnalyticsSummary.vue` made interactive.
- **Milestone 14 Roadmap Definition (`TODO.MD`)**:
  - Defined `SIM-GATE-001` through `SIM-GATE-003` for environment variable gating (`ENABLE_SIMULATION`, `NUXT_PUBLIC_ENABLE_SIMULATION`) across frontend buttons and backend/Nitro mock endpoints.
  - Established `PERF-001` through `PERF-004` performance and stability investigations with explicit timebox budgets (8h–16h), stability soak test durations (4h–24h), and latency SLA targets.
- **Predictive Maintenance & Fleet Analytics Engine (`backend/App.Backend.Api/Services/PredictiveMaintenanceService.cs`)**:
  - Statistical Z-score anomaly detector calculating rolling mean and standard deviation over sliding windows ($|z| > 2.5\sigma$ warning, $|z| > 3.0\sigma$ critical).
  - MTBF (Mean Time Between Failures) and MTTR (Mean Time to Repair) reliability modeling.
  - Remaining Useful Life (RUL) 0–100% health score index dynamic degradation calculations.
  - `AnalyticsController.cs` REST API with endpoints `/api/v1/Analytics/fleet-summary`, `/trends`, `/kpis`, and `/powerbi/config`.
  - Nuxt 4 Analytics Hub at `/dashboard/analytics` with 4 interactive tabs: `FleetAnalyticsSummary.vue`, `TelemetryTrendVisualizer.vue`, `KpiGraphBuilder.vue` (with local storage pinning), and `PowerBiTileEmbed.vue` (with simulated fallback and one-click Grafana/PowerQuery endpoints).
- **Live Telemetry & Enterprise Export Pipelines (`backend/App.Backend.Api/Services/`)**:
  - Live OPC UA node manager in `OpcUaGatewayService.cs` with dynamic tag subscription and node writing.
  - Copia Automation Git webhook integration in `CopiaIntegrationService.cs` with HMAC-SHA256 signature validation.
  - ClosedXML streaming mass `.xlsx` report generator in `ReportExportService.cs` and `ReportExportController.cs`.
  - Live SignalR gauge visualizer in `/dashboard/telemetry/index.vue`.
- **RBAC UI Permissions Hardening & Tooltip System**:
  - Created `<RbacButton>` and `<RbacTooltip>` with Radix-Vue pointer-events wrapper to prevent click-through while displaying informative hover tooltips when permissions are lacking.
  - Hardened navigation links in `AppSidebar.vue` and `SidebarNavLink.vue` (`hideIfUnauthorized` / `disableIfUnauthorized`).
  - Enforced RBAC across Controllers (`EXEC_COMMAND`, `RESTART_AGENT`), Machines (`UPDATE_MACHINE`), Governance settings (`SYSTEM_ADMIN`), Users & Security Groups, and Telemetry Configuration.
- **Remote Quick View & Industrial Palette Overhaul (`frontend/web/`)**:
  - `RemoteQuickViewModal.vue` providing embedded HTML5 VNC viewport and DameWare (`dwmrc://`) deep-link protocol launch.
  - Overhauled color palette in `tailwind.css` replacing neon accents with muted industrial tones (grey-greens, warm taupes, desaturated slates).
- **Code-to-Sequence-Diagram Generator CLI (`tools/generate_sequence_diagrams.py`)**: Automated Python CLI tool parsing C# controllers, services, gRPC contracts, and SignalR hubs to generate and validate Mermaid sequence diagrams with `--check` and `--update-docs` flags.
- **Master Sequence Diagram Gallery (`docs/architecture/SEQUENCE_DIAGRAMS.md`)**: Comprehensive visual reference gallery documenting 6 end-to-end flows: telemetry ingestion, incident ticketing lifecycle, Active Directory OU discovery, PKI mTLS enrollment, station component graph traversal, and agent command execution loops.
- **Canonical Plant Dataset Alignment**: Refactored `ticketsStore.ts` (extracted `initialTickets.ts`), `machineGroupsStore.ts`, `technicianRulesStore.ts`, `devTicketGenerator.ts`, and `organizations.get.ts` to dynamically source entities from `fixtures/enterprise_plant_dataset.json` with robust backwards-compatible fallbacks.
- **Telemetry Policy Engine Drag-and-Drop**: Visual rule priority reordering for Organizational Unit (OU) and Tag recipe assignments in `/dashboard/telemetry/configure` with `GripVertical` drag handles, real-time 1-based (`#1`, `#2`, ...) priority re-indexing, drop indicators, and automatic policy persistence.
- **Point-of-Click Drag Anchoring (`reorderList.ts`)**: `setDragImageAtClickPoint` utility with global `pointerdown` tracking to overcome Linux Chromium's `clientX = 0` dragstart bug, using offscreen DOM clone rendering to force Blink engines to honor exact click offsets. Applied across telemetry rule cards and Kanban incident ticket cards.
- **FMFD ("Find My Field Data") Search & Shortcuts**:
  - Rebranded OmniSearch to FMFD (Find My Field Data / internal Find My Fucking Data).
  - Exported `useFmfd` composable alias alongside `useOmniSearch`.
  - Added keyboard triggers: `Ctrl+Space` for IntelliSense tag suggestions, `/` global search focus, `Ctrl+P` modal quick open, `ArrowDown` to reopen recommendations, and `Tab` tag autocompletion.
- **CAD Map Auto-Scroll & Spatial Anchor Selection**: Clicking any entity on the interactive factory floor plan (`/dashboard/map`) automatically and smoothly scrolls the Spatial Anchors sidebar to center and highlight the matching controlling Client PC card.
- **Admin User Auto-Provisioning**: Nuxt server lifecycle plugin (`ensureAdminUser.ts`) and dev endpoint (`/api/dev/seed-admin`) automatically initializing default credentials (`admin@heimdall.dev` / `admin:AdminPassword123!`) on startup.
- **Windows Edge Agent Launch Utility**: Added `tools/launch_agent_win.py` helper script for launching the Windows edge agent daemon with environment configuration.
- **Frontend Unit Test Suite Expansion**: Added unit test suites `RuleReorderingAndDragDrop.test.ts`, `AllPagesAndRouting.test.ts`, and `SearchLookupBehavior.test.ts`, bringing total coverage to 30 test files and 223 passing unit tests.
- Multi-tenancy global query filters (`HasQueryFilter`) in `AppDbContext` for `ClientPc`, `BaseInventoryItem`, `MaintenanceTicket`, `AgentEvent`, and `AuditLog`.
- `AuditLog` entity for immutable tracking of user actions, role assignments, and configuration changes (TISAX ISA 5.1 / NIS2 compliance).
- `MalformedTelemetryRecord` dead-letter quarantine table storing unparseable or rejected telemetry events (Guideline 36).
- `LocalTelemetrySpooler` in `App.Agent.Daemon` providing offline telemetry buffering with FIFO quota eviction (Guidelines 21, 22, 23).
- Continuous Integration workflow `.github/workflows/ci.yml` running backend (.NET 9) and frontend (Nuxt/Bun) test suites.
- `.editorconfig` enforcing standard formatting rules across C# and TypeScript/Vue codebases.
- EF Core migration `SystemGovernanceAndPki` for governance, PKI, and audit entities.
- Dual-Licensing model: GNU Affero General Public License v3.0 (AGPL-3.0) for open source with Commercial Enterprise Licensing for proprietary enterprise deployments.
- Unit tests for multi-tenant query filters, fail-secure signature verification, and offline telemetry spooler.

### Changed
- **Dashboard File Naming & Route Consolidation**:
  - Purged redundant `index.vue` files across `pages/dashboard/` into descriptive Computer Science names (`overview.vue`, `analytics.vue`, `clients.vue`, `stream.vue`, `system-settings.vue`) while preserving 100% URL route parity via Nuxt file routes and aliases.
  - Decoupled `SystemInfoService` into modular contributors (`IComponentContributor`, `ExtensionComponentContributor`).
- **UI/UX Design System Unification**: Standardized color palette, header typography, card styling, and status badge color conventions across all dashboard pages (`system-settings.vue`, `security-groups.vue`, `tickets.vue`, `users.vue`, `machines.vue`, `inventory.vue`, `organizations.vue`, `help.vue`, `telemetry/configure.vue`, `telemetry/templates.vue`, etc.).
- **Incident Kanban Drag Mechanics**: Upgraded `TicketKanbanBoard.vue` drag-and-drop handling using `setDragImageAtClickPoint` to preserve exact cursor anchor positions during status transitions.
- **Repository SQL Tracking & Gitignore**: Configured `.gitignore` to ignore generated `*.sql` database dumps while strictly preserving EF Core and Drizzle schema migrations. Untracked `seed_data/incremental_seed.sql` from git history while preserving local disk copy.
- Switched backend runtime (`Program.cs`, `appsettings.json`) and frontend BFF (`server/utils/db.ts`) connection defaults from `ef_admin` to least-privilege DML accounts (`dotnet_backend`, `nuxt_frontend`).
- Hardened agent command signature verification to fail-secure mode when `ServerPublicKey` is absent, unless `AllowUnsignedCommands` is explicitly enabled.
- Reconciled table names and column definitions between Python seed pipeline (`seed_pipeline.py`) and EF Core entities (`client_certificates`, `schema_version_manifest`).
- Added Redis authentication (`requirepass`) and restricted PostgreSQL query logging (`log_statement=ddl`) in `infra/database/docker-compose.yml`.
- Configured max database connections to 300 in PostgreSQL container to accommodate Npgsql's connection pool size of 250.
- Deduplicated `MaintenanceTicket` entity configuration in `AppDbContext.cs`.
- Added missing B-tree indexes on `maintenance_tickets(status, priority, created_at, assigned_to, organization_id)` and `agent_events(client_pc_id, timestamp)`.

### Removed
- **Mock Diagnostic Snapshot**: Removed mock `setTimeout` simulation in `/dashboard/telemetry`.
- **Redundant Admin Redirect**: Deleted 14-line `pages/dashboard/admin/index.vue` redirect in favor of alias in `system-settings.vue`.
- **Duplicate Nitro API Route Handlers**: Purged 6 redundant flat-file routes (`machine-groups.get.ts`, `machine-groups.post.ts`, `technicians/absences.get.ts`, `technicians/absences.post.ts`, `technicians/rules.get.ts`, `technicians/rules.post.ts`) in favor of directory-based route standards.
- **Redundant Dataset Fixtures**: Deleted duplicate copy at `frontend/web/fixtures/enterprise_plant_dataset.json` and unified fixture output to root `fixtures/enterprise_plant_dataset.json`.
- **Obsolete Documentation**: Deleted pre-alpha `SCHEDULE.md` and relocated defense slide deck to `docs/presentation.md`.
- **Orphaned Bytecode Caches**: Purged unversioned root and seed `__pycache__` directories.

### Fixed
- **Edge Agent Safe Lifecycle & Double Disposal Prevention (`AdsSimulationServer.cs`, `MinimalOpcClient.cs`, `Worker.cs`)**: Prevented process aborts (`SIGABRT 134`) caused by unhandled `ObjectDisposedException` during ASP.NET Core singleton shutdown. Added thread-safe idempotency guards and defensive disposal handling across Beckhoff TwinCAT ADS and Minimal OPC UA subsystems.
- **Windows Edge Agent Runtime Isolation & Dev Routing (`run_dev.sh`, `tools/tui.py`, `tools/dev_manager.py`)**: Prevented host port collisions on `5998` and `48898`. Routed `./run_dev.sh restart agent` and `stop agent` to the active Windows container when enabled. Deduplicated port 5998 in `dev_manager.py` topology monitor and added real-time `STANDBY` state visualization in `tools/tui.py` for host Linux agent fallback.
- **Edge Fleet Simulator Unbuffered Logging & Direct PID Lifecycle (`fleet_simulator.py`, `run_dev.sh`)**: Configured unbuffered output (`python -u`) and rate-limited line logging in `fleet_simulator.py` when redirected to files. Ensured reliable PID tracking and process detaching across service restarts.
- **Backend DI Captive Dependency Resolution**: Fixed startup crash (exit code 134) in `App.Backend.Api` caused by singleton `PluginService` consuming scoped `AppDbContext`. Refactored `PluginService` to inject `IServiceScopeFactory` and manage transient scopes during database transactions while preserving test constructor overloads. Added `DependencyInjectionIntegrityTests` to validate service descriptor graph on every test build.
- **Python Fleet Simulator Test Alignment**: Resolved legacy hostname discrepancies (`CPC-*` -> `IPC-L01-*`) and updated hardware assertions across `test_mock_cmi_runner.py` and `test_simulated_pc_integration.py` (9/9 tests passing).
- **Active Directory OU Organization Tagging**: Connected plant organizations tab to Active Directory OU discovery so tenant cards dynamically render matching OU paths and VLAN tags.

## [0.1.0-alpha] - 2026-09-02

### Added
- Initial project baseline: ASP.NET Core .NET 9 API, Nuxt 4 Web Frontend, Edge Agent Daemon.
- Hybrid Graph-Relational inventory model with Table-per-Type (TPT) inheritance.
- GIN indexed JSONB metadata for dynamic industrial asset telemetry.
- Field-level AES-256-GCM encryption for license keys and sensitive layout assets.
- Initial seed pipeline generating 500 manufacturing stations, 500 IPCs, and associated components.
