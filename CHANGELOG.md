# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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
