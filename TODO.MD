# Heimdall Development Roadmap & Action Items

> **Status:** Active Development (V1 Architecture & Enterprise Governance Implemented)

---

## Milestone 1: Monorepo Organization, Direct V1 API & Contracts
- [x] Standardize repository structure (`frontend/web`, `simulators/fleet/`, `shared/App.Contracts/`, `tools/cad/`, `tools/dev_manager.py`).
- [x] Version REST API controllers directly under `api/v1/` (`[Route("api/v1/[controller]")]`) without legacy fallbacks.
- [x] Version Protobuf gRPC contracts (`package heimdall.telemetry.v1;`).
- [x] Add database schema version manifest tracking (`schema_version_manifest`).

---

## Milestone 2: Enterprise Identity, Dynamic Security Groups & RBAC Governance
- [x] Add fine-grained ASP.NET Core authorization policies (`SystemAdministration`, `EndpointConfigManagement`, `RemoteExecution`, `MaintenanceOperations`).
- [x] Lock endpoint configuration updates and remote execution behind engineering and higher roles (`admin`, `system_admin`, `lead_engineer`, `engineer`, `controls_engineer`).
- [x] Implement dynamic Active Directory and Microsoft Entra ID Security Group mapping (`SecurityGroupMapping`, `DynamicSecurityGroupClaimsTransformer`).
- [x] Create Admin Security Groups Dashboard (`/dashboard/security-groups`) with interactive token claims evaluation sandbox.

---

## Milestone 3: Master System Governance & Edge Agent Encryption Policies
- [x] Create Master System Settings entity and backend management controllers (`SystemSetting`, `SystemSettingsController`).
- [x] Create Master System Governance Admin UI (`/dashboard/admin/system-settings`) covering:
  - Auth & Session Governance (Session TTL, MFA enforcement, failed login lockout).
  - OT Integrations (OPC UA Gateway endpoints, security policies, Copia Automation Git webhooks).
  - mTLS & X.509 Certificate Authority (`ClientCertificateRecord`, issuance & revocation).
  - Agent Master Policy & Encryption Templates (Hardware-bound sealing, AES-256-GCM / DPAPI spool encryption, remote execution kill-switch, token-bucket egress throttles, fleet push).
- [x] Update Daemon Agent `AgentConfig` and `Worker.cs` to enforce signed master policies and execution kill-switches.

---

## Milestone 4: Seed Data Pipeline & Industrial Simulator Suite
- [x] Create unified transactional seed generator and referential integrity validator (`seed_data/seed_pipeline.py`).
- [x] Build multi-device object-oriented Industrial Edge Fleet Simulator (`simulators/fleet/fleet_simulator.py`) supporting TwinCAT ADS, SIMATIC, Linux edge terminals, sine-wave telemetry, anomaly injection, and live CLI monitoring.
- [x] Build Dev Manager orchestrator & health checker (`tools/dev_manager.py`).

---

## Milestone 5: Specialized Subagent Roster for Future Development
The following subagents are defined for specialized execution of upcoming roadmap features:

1. **`agent_system_governance`**: Oversees RBAC policies, mTLS certificate lifecycles, and configuration synchronization between the master backend and edge daemons.
2. **`agent_ad_entra_identity`**: Implements Microsoft Graph API / SCIM group auto-sync, OIDC authentication hooks, and multi-tenant domain partitioning.
3. **`agent_ot_integrations`**: Implements the full OPC UA Information Model (OPC 10000-100 Devices) and Copia Automation Git webhook change analyzer.
4. **`agent_telemetry_ui`**: Builds high-frequency real-time telemetry dashboards (`/dashboard/telemetry`), live EtherCAT topology visualizers, and Structured Text DUT inspectors.
5. **`agent_reporting_export`**: Builds streaming mass-data export pipelines for `.xlsx` (OpenXML) and `.xlsb` (Binary Excel), with automated email/Graph dispatching.

---

## Milestone 6: Upcoming Milestones
- [x] Implement live OPC UA server node manager in `backend/App.Backend.Api/Services/OpcUaGatewayService.cs`.
- [x] Implement Copia Git webhook handler in `backend/App.Backend.Api/Services/CopiaIntegrationService.cs`.
- [x] Build `/dashboard/telemetry` live SignalR gauge visualizer in Nuxt 4.
- [x] Build ClosedXML / EPPlus mass `.xlsx` and `.xlsb` streaming export endpoints.

---

## Milestone 7: Security Audit Remediation & Hardening

### Critical Priority (Immediate Action Required)
- [x] **SEC-001**: Remove hardcoded database credentials and connection strings from version-controlled configs (`appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`, `Program.cs`, `db.ts`, `AppDbContextFactory.cs`).
- [x] **SEC-002**: Disallow hardcoded AES-256-GCM dev encryption key fallback across non-development environments and strictly enforce `HEIMDALL_ENCRYPTION_KEY` (`AppDbContext.cs`).
- [x] **SEC-003**: Enforce `[Authorize]` attributes on all unauthenticated GET API endpoints across `ClientPcController`, `MachineController`, `InventoryController`, `MaintenanceTicketController`, and `DashboardController`.
- [x] **SEC-004**: Restrict open CORS policy (`AllowAllLocalDev` with wildcard origins and credentials) to strictly verified development environments or explicit origin whitelists (`Program.cs`).
- [x] **SEC-005**: Secure gRPC `SystemInfoCollectorService` endpoint with authentication/mTLS and authorization to prevent unauthenticated telemetry injection and command interception.

### High Priority
- [x] **SEC-006**: Deprecate or gate the HTTP dev seed endpoint (`seed-admin.get.ts`) behind authenticated administrative CLI tooling to prevent unauthorized mass account generation.
- [x] **SEC-007**: Restrict Swagger UI and OpenAPI documentation generation to `Development` environment only (`Program.cs`).
- [x] **SEC-008**: Implement ASP.NET Core rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`) on sensitive API routes and authentication endpoints.
- [x] **SEC-009**: Enforce least-privilege non-root execution (`USER` directive) across all Dockerfiles (`backend/Dockerfile.dev`, `frontend/web/Dockerfile.dev`, `agent/Dockerfile.dev`).
- [x] **SEC-010**: Enforce `[Authorize]` policy on SignalR `MaintenanceHub` to prevent unauthenticated group subscription and event broadcasting.
- [x] **SEC-011**: Sanitize gRPC exception responses in `SystemInfoCollectorService` to prevent leaking internal database schema and exception details.
- [x] **SEC-012**: Parameterize PostgreSQL role initialization scripts (`01-init-roles.sql`) using secrets or environment variables instead of plaintext DDL passwords.

### Medium Priority
- [x] **SEC-013**: Configure strict Content Security Policy (CSP) headers in Nuxt/Nitro server config.
- [x] **SEC-014**: Enable HTTP Strict Transport Security (HSTS) with `app.UseHsts()` in backend production pipelines.
- [x] **SEC-015**: Secure or disable pgAdmin container exposure on public port 5050 in default compose files.
- [x] **SEC-016**: Pass Redis authentication via configuration file or Docker secrets rather than plaintext CLI command flags.
- [x] **SEC-017**: Disable Nuxt devtools and debug source map exposure in production builds (`nuxt.config.ts`).
- [x] **SEC-018**: Pin Docker container base images to immutable SHA256 digests rather than mutable tags (`latest`, `18`, `10.0`).

### Low Priority
- [x] **SEC-019**: Replace `Math.random()` with `crypto.randomUUID()` for organization and member ID generation (`seed-admin.get.ts`).
- [x] **SEC-020**: Restrict `AllowedHosts` in `appsettings.json` from `*` to specific production domains.
- [x] **SEC-021**: Evaluate encryption or session-scoped isolation for offline operational data stored in browser `localStorage`.
- [x] **SEC-022**: Integrate automated SAST, dependency auditing (`dotnet list package --vulnerable`, `bun audit`), and license compliance scanning into `.github/workflows/ci.yml`.
- [x] **SEC-023**: Enforce strict requirement for `BETTER_AUTH_SECRET` without fallback in production runtime configuration (`auth.ts`).


## Better UI for Telemetry, SignalR fixes, Fix the broken organizations tab
###UI
- [x] **Look abd feel**: General UI/UX experience for configuring
- [x] **Backend for the templating**: Make sure templating works for sane use cases (test for edge cases and bugs later)
- [x] **Fix the org tab**: Make sure the org tag actually uses the OU based org structure if any exists


## Decouple from Current AD Specific Feature
- [x] **Make user mgmt an interfaced function for use with non AD deployments**: Specified `IIdentityDirectoryService` interface with `DatabaseIdentityService` (Better-Auth default), `LdapOpenDirectoryService` (RFC 4511/FreeIPA), and `EntraIdGraphIdentityService` in `docs/architecture/IDENTITY_AND_COMPUTER_MANAGEMENT_DECOUPLING.md`.
- [x] **Look into alternative computer mgmt systems**: Evaluated autonomous gRPC edge self-registration (outbound port 5001), subnet mDNS/ARP discovery, EtherCAT/PROFINET bus scanning, and Ansible/SaltStack inventory sync.
- [x] **Look into how much the day to day operation relies on AD in case of downtimes or migration away from AD**: Verified zero operational impact — local Better-Auth credentials, edge telemetry streaming, real-time SignalR notifications, and maintenance ticketing function independently of AD connectivity.

---

## Milestone 8: Backend Stability, Redis Diffing & Hardware Polling Reduction

> Halted investigation goals — carry forward for implementation.

### Crash & Scaling
- [x] **STAB-001**: Investigate OOM/crash root cause — confirm EF Core 5-level Include chain with Cartesian explosion is fully mitigated after `AsNoTracking()` + 2-level Include cap applied to `AssetRepository.cs`.
- [x] **STAB-002**: Profile Redis serialization under load — ensure no cyclic object graphs reach `CacheService.SetAsync`. Added `ReferenceHandler.IgnoreCycles` and cyclic reference suppression in `CacheService.cs`.
- [x] **STAB-003**: Add Redis diffing — before writing to Redis, compare the new payload hash (SHA-256 of serialized value) against a stored hash key in `CacheService.cs`. Skip the write if unchanged. Prevents redundant full-object overwrites under high-frequency polling scenarios.

### Hardware Component Polling
- [x] **POLL-001**: Reduce hardware component telemetry polling interval from 2 seconds to a configurable minimum of 10–30 seconds for stable components (CPU clock, memory slots, disk specs). Added static hardware and OS config cache with configurable TTL in `SystemInfoService.cs`.
- [x] **POLL-002**: Gate polling interval via `HardwarePollIntervalSeconds` in `AgentConfig` and `ConfigurationService` so it can be tuned at runtime via signed config payloads.

---

## Milestone 9: Organizations Tab & Multi-Tenant Member Management

> Halted plan goals — carry forward for implementation.

- [x] **ORG-001**: `organizations.get.ts` — include real `memberCount` via LEFT JOIN + GROUP BY so OrgCard can display accurate member counts instead of hardcoded "A B C" placeholders.
- [x] **ORG-002**: `OrgCard.vue` — replace hardcoded avatar letters with a real member count badge; add delete button (emits `delete` event to parent page).
- [x] **ORG-003**: `organizations.vue` — wire up `handleDeleteOrg` using `authClient.organization.delete`.
- [x] **ORG-004**: `organizations.vue` — add invite-member form (email + role select) in the members modal; wire up `handleInviteMember` using `authClient.organization.inviteMember`.
- [x] **ORG-005**: `organizations.vue` — wire up the Trash2Icon click on each member row; implement `handleRemoveMember` using `authClient.organization.removeMember`.

---

## Milestone 10: Auth Provider Modernization

> Research / integration task.

- [x] **AUTH-001**: Evaluate [Better Auth Studio self-hosting](https://www.better-auth.studio/self-hosting) for deployment as an integrated auth management UI. Assess feature coverage vs. current custom admin panels, Docker deployment model, and compatibility with existing `better-auth` server instance in `frontend/web/server/utils/auth.ts`.
  - **Status**: Completed. Evaluated feature matrix and deployment architectures; implemented embedded Nitro route at `/admin/studio` with Drizzle event ingestion (`auth.auth_events`), role-gated studio session security, navigation links in sidebar and users dashboard, and automated test coverage.

---

## Milestone 11: Enterprise Organization Model, Inventory Segmentation & Machine Hierarchy
- [x] **ORG-GOV-001**: Expand system administrative roles to `system_admin`, `heimdall_admin`, `it_admin`, and `engineering_admin` (segregating engineering admin from line management) with dynamic claims transformation and pseudo-IT admin role inheritance.
- [x] **USER-CARD-001**: Implement user profile card and user account settings page (`/dashboard/settings`) with title, department, presence, and MFA policy configuration.
- [x] **INV-SEG-001**: Segment inventory model into Serialized Parts (`isStockItem = false`, `InMachine`, `InStorage`, `UnderRepair`) and Bulk Stock Items (`isStockItem = true`, `stockQuantity`, `minStockThreshold`).
- [x] **TREE-MODAL-001**: Replace monolithic hierarchy page with on-demand Station Component Tree visualization modal (`StationComponentTreeModal`).
- [x] **MACHINES-001**: Build Production Machines & Lines page (`/dashboard/machines`) with 3 views: Cards of Machines, Cards of Lines, and Technologies (Assembly, Test, SMT, Welding, Fastening, Dispensing, Robotics).
- [x] **OMNI-002**: Upgrade OmniSearch 2.0 with `nvim-coc` style floating auto-tag completion dropdown (`@`, `#`, `!`) and fuzzy ranking.
- [x] **DATA-DOC-001**: Align sample seed data pipeline (`seed_pipeline.py`, `inventory_seed.csv`, `incremental_seed.sql`, `tree.get.ts`, `enterprise_plant_dataset.json`) and update data model documentation in `SYSTEM_ARCHITECTURE.md`, `API_REFERENCE.md`, and `USER_GUIDE.md`.

---

## Milestone 12: UX Refinement, Remote Control & Analytics

### UI Color Palette Overhaul
- [x] **UI-COLOR-001**: Audit and replace all neon/vibrant accent colors across the frontend with a muted, industrial palette — grey-greens, warm taupes, desaturated browns, and slate tones. No neon blues, electric greens, or vivid purples. Update Tailwind/CSS design tokens and component-level overrides site-wide.

### Remote Control Quick View
- [x] **RMC-001**: Research and prototype VNC-based in-browser remote view for monitored machines (e.g. noVNC WebSocket bridge, LibVNCServer integration). Assess feasibility for quick read-only viewport without full remote session overhead.
- [x] **RMC-002**: Investigate DameWare MRC/Mini Remote Control integration — evaluate REST/COM API availability for launching sessions directly from a machine card or alert context. Consider deep-link URI schemes (`dwmrc://`) as a fallback.
- [x] **RMC-003**: Design a unified "Quick View" panel component on machine detail pages that can host either a VNC canvas or a DameWare launch button depending on configured remote provider per endpoint.

### Predictive Maintenance, Analytics & BI Integration
- [x] **PM-001**: Research predictive maintenance approaches suitable for OT/ICS telemetry data — time-series anomaly detection, MTBF trend modeling, threshold-based degradation alerts.
- [x] **PM-002**: Implement trends tracking for key machine metrics — store rolling historical snapshots and expose trend lines on telemetry dashboards (e.g. temperature drift, cycle time deviation, error rate growth).
- [x] **PM-003**: Build a custom KPI graph builder — allow engineers to define, pin, and visualize KPIs (OEE, uptime %, fault frequency, MTTR) as configurable chart widgets on machine and line dashboards.
- [x] **PM-004**: Surface useful at-a-glance statistics panels: fleet-wide uptime summary, top-faulting machines, maintenance backlog age, stock depletion rates, and critical ticket SLA compliance.
- [x] **PM-005**: Investigate Power BI Embedded integration — evaluate service principal auth, dataset push via Power BI REST API, and embedding report tiles within Heimdall dashboard pages.
- [x] **PM-006**: Research additional BI/reporting export targets — Grafana (via Infinity datasource or InfluxDB bridge), Excel/PowerQuery live connection, and generic OData endpoint for third-party BI tools.

---

## Milestone 13: Codebase Cleanup, Dead File Removal, Documentation Alignment & Code-to-Sequence-Diagram Generation
- [x] **CLEAN-001**: Remove monolithic inline initialTickets array from `ticketsStore.ts` and modularize to `initialTickets.ts`.
- [x] **CLEAN-002**: Initialize `machineGroupsStore.ts` and `technicianRulesStore.ts` dynamically from canonical plant fixtures (`datasetLoader.ts`) with backwards-compatible hierarchy fallbacks.
- [x] **CLEAN-003**: Dynamic machine pool sampling in `devTicketGenerator.ts` from canonical `getPlantClientPcs()`.
- [x] **CLEAN-004**: Purge duplicate Nitro endpoint files (`machine-groups.get.ts`, `machine-groups.post.ts`, `technicians/absences.get.ts`, `technicians/absences.post.ts`, `technicians/rules.get.ts`, `technicians/rules.post.ts`).
- [x] **CLEAN-005**: Eliminate duplicate fixture copy (`frontend/web/fixtures/enterprise_plant_dataset.json`), consolidate seed generator to canonical location.
- [x] **CLEAN-006**: Move presentation deck `presentation.md` to `docs/presentation.md` and delete obsolete pre-alpha `SCHEDULE.md`.
- [x] **CLEAN-007**: Align Python fleet simulator and mock CMI runner test suites (`test_mock_cmi_runner.py`, `test_simulated_pc_integration.py`) to canonical dataset hostnames (`IPC-L01-*`) and 9/9 passing tests.
- [x] **DIAG-001**: Build Python code-to-sequence-diagram CLI generator (`tools/generate_sequence_diagrams.py`) with `--check` CI validation and `--update-docs` auto-embedding.
- [x] **DIAG-002**: Generate Master Sequence Diagram Gallery (`docs/architecture/SEQUENCE_DIAGRAMS.md`) with 6 core flow diagrams in GitHub-compatible Mermaid syntax.
- [x] **DOCS-001**: Update `README.md`, `SYSTEM_ARCHITECTURE.md`, `EDGE_AGENT_AND_PROTOCOLS.md`, `API_REFERENCE.md`, `DEV_GUIDE.md`, and `Home.md` with current metrics (56 IPCs, 100 machines, 8 production corridors, 83 backend tests, 241 frontend unit tests, 9 Python fleet tests).

---

## Milestone 14: Environment-Gated Simulation Controls, Performance & Stability Investigations

### Simulation & Placeholder Feature Gating
- [ ] **SIM-GATE-001**: Gate all frontend simulation controls and placeholder action buttons behind `NUXT_PUBLIC_ENABLE_SIMULATION` / `ENABLE_SIMULATION` environment variables.
  - Hide or disable "Fleet Sim" / `SimulatorControlModal` in `tickets.vue`.
  - Hide or disable "Simulate Traffic", dynamic anomaly injection, and synthetic load generator triggers across telemetry and analytics dashboards.
  - Disable "Simulate Role Persona" switcher in `useAuthSession.ts`, `PreferredTechniciansModal.vue`, and `/dashboard/settings`.
  - Hide simulated directory group claims testing panel in `/dashboard/security-groups` when simulation is disabled.
- [ ] **SIM-GATE-002**: Gate backend & Nitro simulation API endpoints behind `ENABLE_SIMULATION` / `HEIMDALL_ENABLE_SIMULATION`.
  - Return `403 Forbidden` or `404 Not Found` on `/api/simulator/*` when simulation is disabled.
  - Disable `/api/ad-mock/*` and Teams OOO simulation endpoints (`/api/integrations/teams/ooo.post.ts`) in non-simulation environments.
  - Prevent background `devTicketGenerator.ts` poller from initializing unless `ENABLE_SIMULATION=true`.
  - Disable backend demonstration mock fallbacks (e.g. `AnalyticsController.cs` mock auth fallback) in production runtime configurations.
- [ ] **SIM-GATE-003**: Centralize environment variable configuration and documentation in `.env.example`, `DEV_GUIDE.md`, and production Docker Compose manifests (`ENABLE_SIMULATION=false` by default in production).

### Performance & Stability Investigations
- [ ] **PERF-001**: Telemetry Ingestion Throughput & Long-Run Memory Profiling.
  - **Estimated Investigation Time**: 16 hours (2 Days).
  - **Stability Soak Duration**: 24-hour continuous stream test under simulated 50–100 edge nodes emitting at 10 Hz.
  - **Target Metrics & Times**: Ingestion P99 latency < 25ms, zero cumulative managed/unmanaged heap growth (memory leak detection), GC Gen 2 pause times < 15ms.
- [ ] **PERF-002**: Database Connection Pool Saturation & Query Execution Latency Audit.
  - **Estimated Investigation Time**: 12 hours (1.5 Days).
  - **Stability Soak Duration**: 4-hour sustained concurrent read/write test (200 simulated concurrent users).
  - **Target Metrics & Times**: P95 query execution time < 50ms, maximum connection acquisition wait time < 10ms, zero connection pool exhaustion events (`TimeoutException`).
- [ ] **PERF-003**: Frontend Long-Session Memory Retention & Stream Render Performance.
  - **Estimated Investigation Time**: 8 hours (1 Day).
  - **Stability Soak Duration**: 8-hour continuous live dashboard session (`/dashboard/telemetry`, `/dashboard/overview`).
  - **Target Metrics & Times**: Chrome memory growth < 100 MB delta over 8 hours, steady 60 FPS UI rendering without dropped frames during high-frequency SignalR message bursts.
- [ ] **PERF-004**: Edge Daemon Resource Footprint & Disconnect Recovery Timeboxing.
  - **Estimated Investigation Time**: 8 hours (1 Day).
  - **Stability Soak Duration**: 12-hour intermittent network flapping test (simulated 10m, 30m, 2h disconnects).
  - **Target Metrics & Times**: Linux edge daemon RSS memory < 45 MB, CPU utilization < 2.5%, spool-to-cloud backlog drain time < 5 seconds upon link restoration.




