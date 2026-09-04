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


