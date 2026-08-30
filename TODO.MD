# Heimdall Development Roadmap & Action Items

> **Status:** Active Development (PoC / Architectural Refactoring Phase)

---

## Milestone 1: Core Domain Graph Model & Database Schema
- [x] Refactor domain model from rigid component tree to graph-relational architecture.
- [x] Establish `StationController` junction table for M:N relationships between Production Stations and IPCs/Controllers.
- [x] Separate `HardwareComponent` and `SoftwareAsset` entities with relational junction tables (`ControllerHardware`, `StationHardware`, `ControllerSoftware`).
- [x] Add specialized controller classification (`IPC`, `PLC`, `SoftPLC`, `RobotController`, `Dispenser`, `VisionController`, `AutonomousDevice`).
- [x] Implement `EquipmentInterconnect` entity for OT fieldbus topology (EtherCAT, PROFINET, OPC UA, Modbus TCP).
- [ ] Generate EF Core migration for schema updates (`backend` & `shared` projects).
- [ ] Update seed data scripts (`seed_data/inventory_seed.csv`, `incremental_seed.py`, `simulate_pcs.py`) to support M:N station-controller relationships.

---

## Milestone 2: Industrial Edge Agent & System Telemetry
- [x] Design WMI / P/Invoke telemetry contract for Beckhoff Real-Time Ethernet Drivers (`TcRTEthernet`, `TcEth`).
- [x] Implement Registry uninstall key scanner for fast installed software detection on Windows IPCs.
- [x] Add Beckhoff TwinCAT ADS System API integration and EtherCAT diagnostics (`DevState`, `ST_EcSlaveState`, `WcState`, CRC counters, SDO).
- [x] Implement Declarative Recipe Engine with Multi-Recipe Runtime Merger (CSTK deduplication, strictest min-interval, deepest scope).
- [x] Add 4-tier priority channels, Token Bucket network rate limiting, xxHash64 delta evaluation, and store-and-forward SQLite WAL spooling.
- [x] Implement PII-safe filesystem scanner with strict directory pruning (Chrome, Edge, Firefox, cookies, user docs) and process secret scrubber.
- [x] Hardware-bound configuration encryption (Windows DPAPI & Linux AES-256-GCM HKDF Machine-Binding).

---

## Milestone 3: Industrial Integration APIs (OPC UA & Copia Automation)
- [x] Architectural blueprint for Heimdall OPC UA Server & Gateway (`opc.tcp://0.0.0.0:4840/Heimdall`).
- [x] Design Copia Automation Git integration blueprint for PLC/Soft-PLC project version tracking.
- [ ] Implement Copia webhook receiver in .NET Web API (`/api/v1/integrations/copia/webhook`).
- [ ] Implement OPC UA Information Model mapping aligned with OPC 10000-100 (Devices) and OPC IA specs.

---

## Milestone 4: TISAX Security Compliance & Auditing
- [x] TISAX (VDA ISA 6.0) security assessment framework mapping.
- [x] Enforce EF Core Global Query Filters for multi-tenant isolation (`OrganizationId`).
- [x] Command signature validation for queued agent operations.
- [ ] Add automated security audit logging for configuration changes and user logins (`audit_logs`).
- [ ] Implement mTLS support for gRPC Agent telemetry channel.

---

## Milestone 5: Database Caching, Indexing & Performance Optimization
- [x] Multi-tier caching strategy (L1 `IMemoryCache` for static references, L2 Redis for telemetry state).
- [x] Data tiering specification (Instant Summary Payload vs. On-Demand Lazy Queries).
- [x] PostgreSQL GIN indexing for JSONB columns (`system_metadata`, `metadata`).
- [x] Configure Redis L2 cache provider in `App.Backend.Api`.
- [ ] Add database ETag / Conditional GET middleware (`304 Not Modified`) in Nuxt Nitro BFF.

---

## Milestone 6: Frontend Offloading & UI Refactoring
- [x] Fix Nuxt 4 `shadcn-vue` component structure (`app/components/ui/searchable-select/`).
- [x] Configure Vitest test runner with JSDOM environment and alias resolving.
- [x] Modularize `AppSidebar.vue` and `useAppSettings.ts` composables.
- [ ] Move client-side sorting, filtering, and aggregation to Nuxt Nitro server routes (`server/api/`).
- [ ] Add spatial layout viewer component with interactive DXF handle selection.

---

## Milestone 7: Telemetry Visualization & Remote Agent Configuration UI
- [ ] Implement Live Telemetry Dashboard (`/dashboard/telemetry`) with real-time SignalR telemetry feeds, station cards, and spindle/metric gauges.
- [ ] Create Over-the-Wire Agent Configuration UI (`/dashboard/agent-config`) to remotely manage recipes, update polling strategies, configure driver endpoints, inspect merged DAG execution graphs, and adjust token-bucket egress throttles.
- [ ] Build EtherCAT Topology Visualizer (`/dashboard/fieldbus/ethercat`) rendering live slave states (`INIT`..`OP`), Working Counter (`WcState`) error counters, and CRC diagnostics.
- [ ] Add TcOpen `_data` inspector component to live-browse Structured Text DUT values with Zod runtime validation.

---

## Milestone 8: Automated Data Export & Reporting Engine (XLSX & XLSB)
- [ ] Build streaming Excel export engine supporting `.xlsx` (OpenXML / ClosedXML / EPPlus) and `.xlsb` (Binary Excel format for ultra-compact high-speed mass data reporting).
- [ ] Implement Automated Scheduled Report Dispatcher (Cron-scheduled telemetry reports with customizable templates via SMTP / SendGrid / Microsoft Graph API).
- [ ] Add Frontend 1-Click Export UI to download telemetry history, ticket metrics, and inventory trees in `.xlsx`, `.xlsb`, and `.csv` formats.

