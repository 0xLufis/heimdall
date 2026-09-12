# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
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

## [0.1.0-alpha] - 2026-09-02

### Added
- Initial project baseline: ASP.NET Core .NET 9 API, Nuxt 4 Web Frontend, Edge Agent Daemon.
- Hybrid Graph-Relational inventory model with Table-per-Type (TPT) inheritance.
- GIN indexed JSONB metadata for dynamic industrial asset telemetry.
- Field-level AES-256-GCM encryption for license keys and sensitive layout assets.
- Initial seed pipeline generating 500 manufacturing stations, 500 IPCs, and associated components.
