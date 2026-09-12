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
- [] **Fix the org tab**: Make sure the org tag actually uses the OU based org structure if any exists


## Decouple from Current AD Specific Feature
- [ ] **Make user mgmt an interfaced function for use with non AD deployments**
- [ ] **Look into alternative computer mgmt systems**
- [ ] **Look into how much the day to day operation relies on AD in case of downtimes or migration away from AD**

---

## Milestone 8: Backend Stability, Redis Diffing & Hardware Polling Reduction

> Halted investigation goals — carry forward for implementation.

### Crash & Scaling
- [ ] **STAB-001**: Investigate OOM/crash root cause — confirm EF Core 5-level Include chain with Cartesian explosion is fully mitigated after `AsNoTracking()` + 2-level Include cap applied to `AssetRepository.cs`.
- [ ] **STAB-002**: Profile Redis serialization under load — ensure no cyclic object graphs reach `CacheService.SetAsync`. Add a pre-serialization cycle-break step or a custom Newtonsoft `ReferenceLoopHandling.Ignore` resolver.
- [ ] **STAB-003**: Add Redis diffing — before writing to Redis, compare the new payload hash (SHA-256 of serialized value) against a stored hash key. Skip the write if unchanged. Prevents redundant full-object overwrites under high-frequency polling scenarios.

### Hardware Component Polling
- [ ] **POLL-001**: Reduce hardware component telemetry polling interval from 2 seconds to a configurable minimum of 10–30 seconds for stable components (CPU clock, memory slots, disk specs). Keep high-frequency (2 s) only for dynamic metrics (CPU load, disk I/O, network throughput).
- [ ] **POLL-002**: Gate polling interval via `SystemSetting` master config entry so it can be tuned at runtime without a deploy.

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

