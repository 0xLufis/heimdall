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
- [ ] Implement live OPC UA server node manager in `backend/App.Backend.Api/Services/OpcUaGatewayService.cs`.
- [ ] Implement Copia Git webhook handler in `backend/App.Backend.Api/Services/CopiaIntegrationService.cs`.
- [ ] Build `/dashboard/telemetry` live SignalR gauge visualizer in Nuxt 4.
- [ ] Build ClosedXML / EPPlus mass `.xlsx` and `.xlsb` streaming export endpoints.

---

## Milestone 7: Security Audit Remediation & Hardening

### Critical Priority (Immediate Action Required)
- [ ] **SEC-001**: Remove hardcoded database credentials and connection strings from version-controlled configs (`appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`, `Program.cs`, `db.ts`, `AppDbContextFactory.cs`).
- [ ] **SEC-002**: Disallow hardcoded AES-256-GCM dev encryption key fallback across non-development environments and strictly enforce `HEIMDALL_ENCRYPTION_KEY` (`AppDbContext.cs`).
- [ ] **SEC-003**: Enforce `[Authorize]` attributes on all unauthenticated GET API endpoints across `ClientPcController`, `MachineController`, `InventoryController`, `MaintenanceTicketController`, and `DashboardController`.
- [ ] **SEC-004**: Restrict open CORS policy (`AllowAllLocalDev` with wildcard origins and credentials) to strictly verified development environments or explicit origin whitelists (`Program.cs`).
- [ ] **SEC-005**: Secure gRPC `SystemInfoCollectorService` endpoint with authentication/mTLS and authorization to prevent unauthenticated telemetry injection and command interception.

### High Priority
- [ ] **SEC-006**: Deprecate or gate the HTTP dev seed endpoint (`seed-admin.get.ts`) behind authenticated administrative CLI tooling to prevent unauthorized mass account generation.
- [ ] **SEC-007**: Restrict Swagger UI and OpenAPI documentation generation to `Development` environment only (`Program.cs`).
- [ ] **SEC-008**: Implement ASP.NET Core rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`) on sensitive API routes and authentication endpoints.
- [ ] **SEC-009**: Enforce least-privilege non-root execution (`USER` directive) across all Dockerfiles (`backend/Dockerfile.dev`, `frontend/web/Dockerfile.dev`, `agent/Dockerfile.dev`).
- [ ] **SEC-010**: Enforce `[Authorize]` policy on SignalR `MaintenanceHub` to prevent unauthenticated group subscription and event broadcasting.
- [ ] **SEC-011**: Sanitize gRPC exception responses in `SystemInfoCollectorService` to prevent leaking internal database schema and exception details.
- [ ] **SEC-012**: Parameterize PostgreSQL role initialization scripts (`01-init-roles.sql`) using secrets or environment variables instead of plaintext DDL passwords.

### Medium Priority
- [ ] **SEC-013**: Configure strict Content Security Policy (CSP) headers in Nuxt/Nitro server config.
- [ ] **SEC-014**: Enable HTTP Strict Transport Security (HSTS) with `app.UseHsts()` in backend production pipelines.
- [ ] **SEC-015**: Secure or disable pgAdmin container exposure on public port 5050 in default compose files.
- [ ] **SEC-016**: Pass Redis authentication via configuration file or Docker secrets rather than plaintext CLI command flags.
- [ ] **SEC-017**: Disable Nuxt devtools and debug source map exposure in production builds (`nuxt.config.ts`).
- [ ] **SEC-018**: Pin Docker container base images to immutable SHA256 digests rather than mutable tags (`latest`, `18`, `10.0`).

### Low Priority
- [ ] **SEC-019**: Replace `Math.random()` with `crypto.randomUUID()` for organization and member ID generation (`seed-admin.get.ts`).
- [ ] **SEC-020**: Restrict `AllowedHosts` in `appsettings.json` from `*` to specific production domains.
- [ ] **SEC-021**: Evaluate encryption or session-scoped isolation for offline operational data stored in browser `localStorage`.
- [ ] **SEC-022**: Integrate automated SAST, dependency auditing (`dotnet list package --vulnerable`, `bun audit`), and license compliance scanning into `.github/workflows/ci.yml`.
- [ ] **SEC-023**: Enforce strict requirement for `BETTER_AUTH_SECRET` without fallback in production runtime configuration (`auth.ts`).



