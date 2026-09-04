# Heimdall Documentation Index

Welcome to the Heimdall technical documentation suite. The documentation is organized into focused, comprehensive reference manuals covering all aspects of the industrial edge platform.

---

## Architecture & Design

* [System Architecture & Data Model](architecture/SYSTEM_ARCHITECTURE.md)
  * Manufacturing plant topology and the graph-relational $M:N$ domain model.
  * Complete entity definitions, constraints, indexes, and database schema isolation (`backend` vs. `auth`).
  * Backend service layers, repository patterns, and hybrid L1/L2 caching with offline resilience.
  * Multi-tenant query isolation via Entity Framework Core Global Query Filters.

* [Edge Agent & Protocol Drivers](architecture/EDGE_AGENT_AND_PROTOCOLS.md)
  * Edge daemon lifecycle, scheduling jitter, and background worker loops.
  * Declarative Recipe JSON Schema and Multi-Recipe Runtime Merger DAG algorithm (CSTK deduplication).
  * Deadband & Delta Evaluator (Absolute, Percentage, `xxHash64` memory hashing, heartbeat TTL).
  * 4-tier dynamic bandwidth throttler, token bucket rate limiter, and adaptive Zstandard compression.
  * Store-and-forward local SQLite WAL spooling with exponential backoff jitter.
  * Comprehensive protocol driver specifications: Beckhoff TwinCAT ADS, EtherCAT master/slave diagnostics, TcOpen OOP `_data` payload standard, standalone `FB_HeimdallTelemetryBridge`, OPC UA, Modbus TCP (endianness conversions), and native OS probes.

* [Security, Encryption & Compliance](architecture/SECURITY_AND_COMPLIANCE.md)
  * Industrial threat model and defense-in-depth security architecture.
  * Field-level authenticated encryption at rest via AES-256-GCM (96-bit nonces, 128-bit authentication tags).
  * Edge cryptographic envelope storage: Windows DPAPI and Linux HKDF-SHA256 machine binding.
  * Signed remote command execution pipeline with fail-secure validation.
  * PII and personal data exclusion engine: directory blacklists, sensitive file filters, process argument scrubber.
  * TISAX (VDA ISA 6.0 Level 3) and GDPR compliance control mappings and verification methods.
  * System audit trails, security event logging, and dead-letter telemetry quarantine.

* [Frontend, Real-Time PWA & Spatial UI](architecture/FRONTEND_AND_PWA.md)
  * Nuxt application architecture and Nitro BFF reverse proxying with automatic tenant header injection.
  * Live maintenance ticketing Kanban board with native HTML5 drag-and-drop mechanics.
  * Real-time SignalR WebSocket hub integration (`/hubs/maintenance`) with mobile vibration notifications.
  * Offline-first Progressive Web App architecture with dual-store IndexedDB caching and background sync replay.
  * Interactive AutoCAD DXF spatial floor plan engine, vector SVG rendering, and machine handle pinning.
  * Dynamic 5-tab asset template editor with variable interpolation pipes and dynamic system tokens.
  * OmniSearch multi-attribute engine with regex entity extraction and Damerau-Levenshtein fuzzy matching.
  * Component catalog and composable API reference (`useMaintenance`, `useOmniSearch`, etc.).

---

## Interface & API Reference

* [API & Interface Reference](api/API_REFERENCE.md)
  * REST Web API (`/api/v1/*`): Complete endpoints, query parameters, request bodies, and response JSON schemas for Stations, Controllers, Maintenance Tickets, Inventory, Dashboard, and Commands.
  * Copia Automation Git webhook integration specification.
  * gRPC Telemetry Ingestion Service (`heimdall.telemetry.v1.SystemInfoCollector`): Complete Protobuf definition and RPC methods.
  * SignalR WebSocket Hub method signatures and client callback events.
  * Shared Data Transfer Objects (DTOs) and contract models.
  * OPC UA Server & Gateway address space hierarchy and node identifier mapping.

---

## Operational Guides

* [Developer & Operations Guide](guide/DEV_GUIDE.md)
  * Monorepo directory structure and development prerequisites.
  * Local development workflow: starting PostgreSQL/Redis, running EF Core database migrations, launching the API and frontend.
  * Running the edge fleet simulator (smoke tests and continuous simulation).
  * Full-stack Docker Compose deployment instructions, service port allocations, and healthchecks.
  * Automated test execution: xUnit backend tests, Vitest frontend unit suites, Playwright browser tests.

* [User & Operator Guide](guide/USER_GUIDE.md)
  * Authentication, session management, and organization switching.
  * Navigating and interacting with the plant floor CAD map.
  * Monitoring edge fleet health and dispatching signed operational commands.
  * Filing, tracking, and resolving incident tickets on the Kanban board.
  * Using the mobile camera QR code scanner for rapid equipment inspection.
  * Managing equipment components and software assets in the inventory repository.

---

## PLC Program Assets

* [TwinCAT 3 POU & Interfaces](plc/)
  * `FB_HeimdallTelemetryBridge.TcPOU`: Standalone TwinCAT 3 Function Block providing lock-free atomic double-buffering.
  * `ITcoHeimdallTelemetry.TcIO`: TcOpen OOP component telemetry interface contract.
