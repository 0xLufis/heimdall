# Heimdall System Capabilities & Technical Architecture Reference

This manual provides the authoritative, exhaustive technical reference for the **Heimdall Industrial Intelligence Platform**, synthesized from a ground-truth audit of the codebase across all four architectural pillars:
1. **Pillar 1: Industrial Edge Agent Daemon & Fieldbus Telemetry Engine**
2. **Pillar 2: Enterprise Backend Architecture, APIs, Graph-Relational Database & Caching**
3. **Pillar 3: Nuxt 4 Industrial Frontend, Spatial CAD Layouts, Dynamic Templating & PWA**
4. **Pillar 4: End-to-End Type Safety, Protobuf Contracts, Security Hardening & Test Verification**

---

## 1. Executive Platform Overview (What Heimdall Actually Does)

Heimdall is a unified industrial OT/IT operations and operational intelligence platform engineered specifically for smart manufacturing environments (Automotive, Semiconductor, High-Speed Assembly, Robotics, and Packaging).

It closes the gap between low-level shop-floor industrial controllers (**Beckhoff TwinCAT ADS, TcOpen OOP, Siemens TIA, Modbus TCP, OPC UA, EtherCAT**) and modern enterprise web software (**ASP.NET Core 9, PostgreSQL 17, Redis 7.4, Nuxt 4, SignalR, Better-Auth**) without relying on proprietary, vendor-locked cloud gateways.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                               HEIMDALL UNIFIED ECOSYSTEM                               │
│                                                                                        │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │                    Nuxt 4 / TypeScript Industrial Frontend                       │  │
│  │  - Live Telemetry Dashboards & Interactive AutoCAD DXF Spatial CAD Canvas        │  │
│  │  - Real-Time Kanban Board & Maintenance Ticketing (SignalR + IndexedDB Offline)  │  │
│  │  - Dynamic 5-Tab JSON Templating Engine & Asset DNA Inspector                    │  │
│  │  - Zod Runtime Type Guards & Strongly-Typed Telemetry Records                    │  │
│  └───────────────────────────────────┬──────────────────────────────────────────────┘  │
│                                      │ REST / SSE / SignalR WebSockets (Port 5099)     │
│                                      ▼                                                 │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │                    .NET 9 Core Enterprise Backend Web API                        │  │
│  │  - Graph-Relational Model (Machines, Controllers, Hardware, Software, Edges)     │  │
│  │  - Multi-Tier Distributed Cache (L1 MemoryCache + L2 Redis 7.4 Cluster)          │  │
│  │  - PostgreSQL 17 Relational & JSONB Storage with Global Query Filters (Multi-Ten)│  │
│  │  - SystemInfoCollector gRPC Server (Port 5001) & Command Queue Dispatcher        │  │
│  └───────────────────────────────────┬──────────────────────────────────────────────┘  │
│                                      │ TLS 1.3 / mTLS gRPC over HTTP/2                 │
│                                      ▼                                                 │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │                    Heimdall Industrial Edge Agent Daemon                         │  │
│  │  - Multi-Recipe Runtime Merger DAG (CSTK Deduplication & Strictest Scheduling)   │  │
│  │  - 4-Tier Priority Bandwidth Throttler (P0 Critical -> P3 Bulk)                  │  │
│  │  - Universal Canonical Protobuf Telemetry (telemetry.proto)                      │  │
│  │  - PII-Safe File Scanner (Streaming SHA-256) & Process Secret Scrubber           │  │
│  │  - Hardware-Bound AES-256-GCM HKDF Machine-ID Envelope Encryption                │  │
│  │  - Store-and-Forward SQLite WAL Spooling with Jittered Draining Engine           │  │
│  └──────────┬───────────────────┬───────────────────┬───────────────────┬───────────┘  │
│             │                   │                   │                   │              │
│             ▼                   ▼                   ▼                   ▼              │
│     [Beckhoff ADS/ECAT]   [TcOpen OOP]        [OPC UA Client]     [Modbus & CIM]       │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Pillar 1: Industrial Edge Agent & Protocol Drivers (`App.Agent.Daemon`)

### 2.1 Daemon Lifecycle, Hosting & Spooling Engine
- **Hosting & Isolation**: Executes on `.NET 9.0` with `Microsoft.NET.Sdk.Web`, hosting an internal Kestrel HTTP endpoint on `http://localhost:5998` for local edge configuration and diagnostic APIs (`GET /api/config`, `POST /api/config`).
- **Anti-Thundering Herd Startup Jitter**: Introduces a randomized startup delay $\Delta t_{\text{startup}} \sim \text{Uniform}(0, 10\,000\text{ ms})$ to prevent mass network congestion during plant-wide power restoration.
- **Cadence & Execution Loop**: 60-second baseline collection loop modulated with $\pm 10\%$ jitter ($T = 60\,000\text{ ms} \pm 6\,000\text{ ms}$).
- **Signed Remote Command Pipeline**: Validates incoming RSA-SHA256 signed server commands (`UPDATE_CONFIG`, `FILE_CHECK`, `APPLY_RECIPE`) against `ServerPublicKey` before execution.
- **Store-and-Forward SQLite WAL Spooling**: Persists `P0` and `P1` packets locally during network partitions using SQLite configured with Write-Ahead Logging (`PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;`), draining automatically upon reconnection using exponential backoff with full jitter.

---

### 2.2 Declarative Recipe Engine & Runtime Merger DAG
- **Declarative Recipe Schema (`RecipeModels.cs`)**:
  - Encapsulates 6 data categories: **Scalars**, **Lists**, **Maps**, **Nested Objects**, **Metrics**, and **Device States**.
  - Supports 9 polymorphic driver source configurations: `BeckhoffAds`, `BeckhoffEtherCat`, `SystemCim`, `SystemProcess`, `SystemDisk`, `SystemFileSystem`, `OpcUaSubscription`, `ModbusTcp`, `TcpSocket`.
  - Cryptographically signed with RFC 8785 Canonical JSON hashes (RSA-4096 / Ed25519) and optional AES-GCM encryption.
- **Multi-Recipe Runtime Merger DAG (`MultiRecipeRuntimeMerger.cs`)**:
  1. **Canonical Source Target Key (CSTK) Deduplication**: Merges overlapping queries targeting the same PLC tag, Modbus register, or process pattern into **exactly 1 physical probe node**.
  2. **Strictest Min-Interval Scheduling**: Evaluates effective interval $T_{\text{effective}} = \min(T_1, \dots, T_k)$, sampling at the highest requested frequency and decimating readings to slower subscribers.
  3. **Deepest Scope & WQL Set Union**: Automatically elevates process inspection to $\max(\text{Depth}_1, \dots, \text{Depth}_k)$ and unions projected WQL properties into a single combined query.

---

### 2.3 Deadband & Delta Evaluator (`DeadbandEvaluator.cs`)
- **Absolute Deadband**: Suppresses readings where $|V_{\text{current}} - V_{\text{previous}}| < \text{Threshold}$.
- **Percentage Deadband**: Suppresses readings where $\frac{|V_{\text{current}} - V_{\text{previous}}|}{|V_{\text{previous}}|} \times 100 < \text{Threshold}\%$.
- **High-Speed Object Layout Hashing (`xxHash64`)**: Computes 64-bit non-cryptographic `System.IO.Hashing.XxHash64` hashes over serialized byte layouts for complex arrays, maps, and DUT structs, executing delta checks in microseconds.
- **Heartbeat TTL Synchronization**: Configurable `MaxQuietPeriodMs` (default: 15 minutes) forces periodic baseline synchronization (`isHeartbeatForced = true`) to confirm edge sensor health.

---

### 2.4 4-Tier Priority Bandwidth Throttler (`PriorityBandwidthThrottler.cs`)

| Priority Tier | Buffer Type | Target Latency | Flush Condition | Egress Behavior |
| :--- | :--- | :--- | :--- | :--- |
| **`P0_CriticalAlarm`** | `Unbounded` | **$< 10\text{ ms}$** | **Immediate (0 delay)** | E-Stops, EtherCAT link drop, thermal alarms. Bypasses all rate delays. |
| **`P1_HighOperational`** | `Bounded (5,000)` | **$< 200\text{ ms}$** | $\ge 10\text{ items}$ or $200\text{ ms}$ | Soft-PLC state changes, drive error codes. |
| **`P2_MediumMetrics`** | `Bounded (20,000)` | **$1\text{ s} - 5\text{ s}$** | $\ge 100\text{ items}$ or $2\,000\text{ ms}$ | Spindle speed RPM, motor current, pressure telemetry. |
| **`P3_LowInventory`** | `Bounded (50,000)` | **$5\text{ m} - 15\text{ m}$** | $\ge 1\,000\text{ items}$ or $5\text{ mins}$ | Installed software inventory, SMART disk health, OS patches. |

- **Dynamic Alarm Escalation**: Metric breaching an alarm threshold (e.g. Spindle Temp $> 85^\circ\text{C}$) dynamically promotes to **`P0`**, flushing immediately.
- **Token Bucket Rate Limiter**: Enforces a configurable network egress ceiling (default: 256 KB/s) based on true elapsed wall-clock time.
- **Adaptive Compression**: Automatically compresses batches $\ge 256\text{ bytes}$ using **Zstandard (`zstd` Level 3)** / Gzip.

---

### 2.5 Industrial Protocol Adapters & Diagnostics

#### 1. Beckhoff TwinCAT ADS & EtherCAT Diagnostics
- **Cross-Platform ADS (.NET 9)**: Utilizes `Beckhoff.TwinCAT.Ads` with in-process `AmsTcpIpRouter` for Linux/Docker deployments without proprietary TwinCAT runtime installations.
- **ADS Sum Commands (`0xF080`)**: Bundles hundreds of tag reads/writes into **1 single TCP round-trip packet**.
- **System API Port 10000 & PLC Runtime Port 851**: Monitors Run/Config/Stop modes, licensing, and real-time task cycles.
- **EtherCAT Master & Slave Diagnostics (Port `0xFFFF`)**:
  - Master State (`INIT`, `PREOP`, `SAFEOP`, `OP`) and `DevState` bitmasks (`0x0001` Link Error, `0x0008` Missing Frames, `0x0800` Slave Error, `0x1000` DC Sync Loss).
  - Slave topology state table via `0x00000009` (`ST_EcSlaveState`).
  - Working Counter (`WcState`) error detection, CoE SDO Upload/Download (`0xF302`), and per-port hardware CRC error counters (`0x0300..0x0307`).

#### 2. TcOpen OOP Integration & Standalone TwinCAT Bridge
- **`ITcoHeimdallTelemetry` Interface (`docs/plc/ITcoHeimdallTelemetry.TcIO`)**: Extends `TcoCore.ITcoComponent`, standardizing the **`_data`** output payload contract.
- **Zero Dynamic Pointer Risk**: The agent reads `<SymbolPath>._data` as pure IEC types without Ring-0 memory access risks, completely eliminating remote pointer dereferencing across networks.
- **Standalone `FB_HeimdallTelemetryBridge` (`docs/plc/FB_HeimdallTelemetryBridge.TcPOU`)**: Drop-in TwinCAT 3 Function Block providing lock-free, atomic double-buffering (`stTelemetryBuffer[0..1]`) with alternating sequence counters (`nSequenceCounter`) for vanilla TwinCAT projects.

#### 3. OPC UA Foundation (`OPCFoundation.NetStandard.Opc.Ua`)
- Secure sessions (`Basic256Sha256`, `Aes128_Sha256_RsaOaep`), transparent keepalive reconnect loop, continuation-point pagination for large address spaces, monitored items with analog deadbands, and batched reads.

#### 4. Modbus TCP & Socket Stream Probes
- 4-way stack-allocated endianness converter (`CDAB` word-swapped IEEE-754 float default, `ABCD` Big-Endian, `BADC`, `DCBA`).
- Contiguous register block batch optimizer (merges requests with gaps $\le 5$ up to 120 registers).
- Zero-copy stream framing via `System.IO.Pipelines` with socket-level `TcpKeepAlive`.

#### 5. Windows CIM (WMIv2 / SetupAPI Native) & Linux sysfs/proc Engine
- Native `setupapi.dll` and `cfgmgr32.dll` P/Invoke querying `GUID_DEVCLASS_NET` for Beckhoff Real-Time Ethernet drivers (`TcRTEthernet`, `TcEth`).
- Windows 4-hive 32/64-bit Registry uninstaller key scanner.
- Linux `/proc/cpuinfo`, `/proc/meminfo`, `/sys/class/net/`, and systemd D-Bus parsing.
- Process Delta CPU % engine with microsecond delta sampling and secret scrubbing.

---

## 3. Pillar 2: Enterprise Backend Architecture, APIs & Database (`App.Backend.Api`, `App.Infrastructure`, `App.Shared`)

### 3.1 Graph-Relational Domain Model & Database Schema
PostgreSQL 17 schema utilizing Table-per-Type (TPT) inheritance for physical/logical equipment hierarchies combined with explicit graph edge tables and GIN-indexed JSONB documents:

- **`BaseInventoryItem` (Table: `backend.inventory_items`)**: Abstract base for all physical and software assets. Contains `OrganizationId` (multi-tenant key), `CostInHUF` (capital cost in Hungarian Forint), `SerialNumber`, `ParentId` (recursive tree hierarchy), `ResponsibleTeams` (M:N junction), and `Metadata` (PostgreSQL `jsonb` with GIN index).
- **`Machine` (Table: `backend.stations`)**: Concrete production station inheriting from `BaseInventoryItem`. Contains `CustomIdentifier` (`"LINE-A-OP10"`), `PinnedObjectHandle` (AutoCAD DXF handle), and `Controllers` (M:N with `ClientPc` via `StationController`).
- **`ClientPc` (Table: `backend.client_pcs`)**: Industrial edge controller / IPC. Contains `MacAddress` (unique index), `Hostname`, `LastOnline`, `PinnedObjectHandle`, `FreeDiskSpace` (JSONB drive map), `SystemMetadata` (JSONB OS/IP info with GIN index), `MonitoringConfig`, and `ResourceAverages`.
- **`StationController` (Table: `backend.StationControllers`)**: Explicit graph edge linking `Machine` and `ClientPc` with role definitions (`"Primary"`, `"Secondary"`, `"Gateway"`, `"Safety"`).
- **`EquipmentInterconnect` (Table: `backend.equipment_interconnects`)**: Graph edge representing industrial communications (OPC UA, PROFINET, Modbus TCP, EtherNet/IP) between equipment items.
- **`HardwareComponent` & `SoftwareAsset`**: Concrete asset models with hardware revisions and AES-256-GCM encrypted license keys (`LicenseKey`).
- **`MaintenanceTicket` (Table: `backend.maintenance_tickets`)**: Incident graph entity linking tickets to assets, edge PCs, and stations with priority levels (`Low` to `Critical`), status workflows, comments, and attachments.
- **`FloorPlan` (Table: `backend.floor_plans`)**: Plant floor layouts with AES-256-GCM encrypted SVG content and JSONB anchor coordinates.
- **Better-Auth Identity Schema (`auth` Schema)**: Multi-tenant user identity, active organizations (`AuthOrganization`), memberships (`AuthMember`), and session tokens.

---

### 3.2 Backend REST API Endpoints

| Controller | Base Route | Operations & Capabilities |
| :--- | :--- | :--- |
| **`ClientPcController`** | `/api/ClientPc` | Retrieves all IPCs with nested stations, updates metadata, MAC addresses, DXF handles, and machine links. |
| **`MachineController`** | `/api/Machine` | Manages production stations, DXF pinned handles, and multi-controller associations. |
| **`MaintenanceTicketController`** | `/api/MaintenanceTicket` | CRUD for incident tickets, write-through caching, L1/L2 invalidation, and SignalR live event broadcasts. |
| **`InventoryController`** | `/api/inventory` | 5-level deep hierarchy tree, dynamic JSONB key extraction (`jsonb_object_keys`), unified tag search, and polymorphic asset creation. |
| **`DashboardController`** | `/api/Dashboard` | Aggregates high-level metrics (Total Users, Active Clients, Pending Alerts, Avg. Uptime) cached for 30s. |
| **`AgentCommandController`** | `/api/AgentCommand` | Queues cryptographically signed commands (`UPDATE_CONFIG`, `FILE_CHECK`) for agent polling. |
| **`OrganizationController`** | `/api/Organization` | Multi-tenant organization unit provisioning and membership administration. |

---

### 3.3 Multi-Tier Hybrid Caching Layer (`CacheService.cs`)
- **L1 In-Memory Cache (`IMemoryCache`)**: Ultra-low latency memory store with key tracking in `ConcurrentDictionary<string, byte>` for pattern invalidation.
- **L2 Distributed Cache (`StackExchange.Redis`)**: Distributed JSON cache across server nodes.
- **Resilient Offline Bypass**: Catches Redis connectivity failures and seamlessly falls back to L1 in-memory caching with zero downtime or request interruption.
- **Pattern Invalidation (`RemoveByPatternAsync`)**: Invalidates L1 regex patterns and executes `server.Keys("heimdall:{pattern}")` batch deletions in Redis.

---

### 3.4 gRPC Telemetry Ingestion Server (`SystemInfoCollectorService.cs`)
High-throughput HTTP/2 gRPC server (Port 5001) implementing:
- `ReportSystemInfo`: Upserts client PC records by MAC address, resolving collisions and archiving stale hostnames.
- `ReportTelemetryBatch`: Ingests compressed time-series batches (`zstd`, `gzip`, uncompressed).
- `StreamAgentEvents`: Real-time streaming channel for critical hardware and fieldbus alarms.
- `SyncRecipes`: Distributes signed recipe bundles and processes revocation lists.

---

## 4. Pillar 3: Nuxt 4 Frontend, Spatial CAD & Reactive UI (`frontend/heimdall-web-frontend`)

### 4.1 Page Architecture & Navigation
- **`/dashboard`**: Executive KPI overview with 4s telemetry polling, global OmniSearch modal, client preview cards, and activity feeds.
- **`/dashboard/clients`**: Fleet manager for edge IPCs with dual Grid/CAD views, live CPU/RAM gauges, TwinCAT telemetry drawers, and signed command dispatch modals.
- **`/dashboard/inventory`**: 3-tab repository (Hardware, Software, Graph Hierarchy) with HUF capital valuation, column visibility controls, and dynamic provisioning modals.
- **`/dashboard/inventory/[id]`**: Asset DNA inspector with recursive parent-child hierarchy visualizer and extended JSONB specifications.
- **`/dashboard/map`**: Dedicated plant layout explorer parsing 12 factory floor AutoCAD DXF drawings (`production_hall.dxf`, `LINE-A.dxf` to `LINE-J.dxf`) with zoom/pan canvas and spatial pinning.
- **`/dashboard/tickets`**: Maintenance incident tracker featuring a native HTML5 drag-and-drop Kanban board (`Open`, `In_Progress`, `Pending_Parts`, `Resolved`), QR barcode scanner modal, and SignalR live push reactivity.
- **`/dashboard/users` & `/dashboard/organizations`**: Role-based access control (`system_admin` down to `technician`), session revocation, user impersonation, and multi-tenant unit provisioning.

---

### 4.2 Dynamic 5-Tab JSON Templating Engine (`AssetTabbedEditor.vue`)
- **Identity Tab**: Name, Display Alias, Item Type, OEM Manufacturer (with inline creation), Model, Tech Stack, Serial Number (auto-generator), Responsible Teams.
- **Topology Tab**: Station Assignment, Reporting IPC, Recursive Parent Assembly, Lateral Linked Components.
- **Commercial Tab**: HUF Unit Cost, Quantity, Vendor/Supplier, Commission Date, Computed Total Capital Valuation.
- **Specs Tab**: Dynamic key-value pairs stored in JSONB metadata with type selection (`string`, `number`, `boolean`, `json`) and suggestions from `useAssetReferenceCache`.
- **Templates Tab**: Built-in & custom template library with category filtering (`Controller`, `Sensor`, `Vision`, `Motion`, `Software`, `Dispensing`, `Safety`); variable interpolation (`{{var | filter}}`); filter chains (`uppercase`, `slugify`, `padzero`); dynamic system tokens (`$uuid`, `$timestamp`, `$randomSerial`); raw JSON editor with syntax error validation; template export/import.

---

### 4.3 Advanced OmniSearch & AutoTagging Engine (`useOmniSearch.ts`, `AutoTagEngine.ts`)
- **Regex Entity Extraction**: Detects IPv4 addresses, MAC addresses, industrial station codes (`LINE-A-OP10`), ticket identifiers (`TKT-101`), and technical specifications with engineering units (`24V`, `60FPS`, `1500RPM`).
- **Damerau-Levenshtein Distance Analysis**: Tolerates typing mistakes and transposed characters across manufacturers, categories, and model numbers.
- **Tag-Based Key:Value Parsing**: Supports explicit queries (`manufacturer:beckhoff`, `team:maintenance`, `type:ipc`).

---

### 4.4 PWA & IndexedDB Offline Architecture
- **IndexedDB Multi-Store (`OfflineQueueMaintenanceProvider.ts`)**: Database `heimdall-maintenance-db` (v1) using `idb` v8 with dual stores:
  1. `cached-tickets`: Caches fetched tickets for instant offline read availability.
  2. `offline-mutation-queue`: Queues offline ticket creations, status transitions, and comments, automatically replaying them upon network restoration (`window.addEventListener('online')`).
- **Service Worker (`public/sw.js`)**: Pre-caches core app shell, enforces network-first caching for API routes, and implements Background Sync (`sync-tickets`).
- **Nitro BFF Reverse Proxy**: Proxies `/api/proxy/*` to `.NET` backend with automatic session token and `X-Organization-Id` injection.

---

## 5. Pillar 4: End-to-End Type Safety, Security Hardening & Test Verification

### 5.1 Universal Protobuf Contracts & Type Pipeline
- **`telemetry.proto`**: Universal strongly-typed contract with `QualityCode` (OPC UA aligned), `DataTypeClassifier` (19 IEC 61131-3 types), `TelemetryValue` (`oneof` union for scalars, `StructValue` maps, `ListValue` arrays, and `DeviceStateValue` FSMs), and `TypeDescriptor`.
- **Deterministic Identifier Sanitizer (`PlcTypeSanitizer.cs`)**: Source-generated regexes stripping pointers/references and normalizing symbol paths (`MAIN.Station1.Telemetry._data[1].fActualSpeed#1` $\to$ `MAIN_Station1_Telemetry_data_1_fActualSpeed_1`).
- **Zero-Alloc Struct Binder (`StructTelemetryBinder.cs`)**: `MemoryMarshal.Read<T>` casting binary ADS byte buffers directly into C# records without managed heap allocations.
- **Frontend Zod Runtime Schemas (`telemetry.types.ts`)**: `TelemetryDataPointSchema` and `Station1TelemetrySchema` validating incoming WebSocket payloads in Vue composables.

---

### 5.2 Security & PII Protection Hardening
- **TISAX VDA ISA 6.0 & GDPR Compliance**: Privacy by design with zero personal data or browser history ingestion.
- **PII-Safe File Scanner (`SecureIndustrialFileScanner.cs`)**: Whitelists industrial file types (`.tszip`, `.pro`, `.tpy`, `.plcproj`, `.ap18`, `.json`, `.xml`, `.ini`, `.csv`) and strictly prunes browser profiles (Chrome, Edge, Firefox, Brave), cookies, history, user personal files, and SSH keys before disk descent.
- **Process Secret Scrubber (`ProcessSecretScrubber.cs`)**: Redacts passwords, API tokens, JWTs, and user home account usernames (`/home/username` $\to$ `[USER_ACCOUNT]`).
- **Hardware-Bound AES-256-GCM Cryptographic Storage (`CrossPlatformSecureStorage.cs`)**: Windows DPAPI + AES-256-GCM envelope encryption with 256-bit keys derived via HKDF-SHA256 from immutable machine IDs (`/etc/machine-id` or Windows `MachineGuid`) and a protected master seed (`0600` permissions).

---

### 5.3 Complete Automated Test Verification Matrix (120 Tests / 100% Pass)

```
========================================================================================
                          HEIMDALL AUTOMATED TEST VERIFICATION
========================================================================================

  Backend & Agent (.NET xUnit):               53 / 53 Passed (100%)
  Frontend Vitest Suites (Nuxt 4 / JSDOM):    67 / 67 Passed across 13 Suites (100%)
  --------------------------------------------------------------------------------------
  TOTAL VERIFIED TEST SUITES:                120 / 120 Passed (100% Zero Failures)
========================================================================================
```

#### Backend Test Suites Breakdown (53 Tests):
- `PlcTypeSanitizerAndProtobufTests`: Normalization, 15 IEC type mappings, `TelemetryValue` serialization, zero-alloc struct unmarshalling.
- `PiiSafeScannerAndSecurityTests`: Browser directory pruning, credential/JWT/user path scrubbing, AES-256-GCM authenticated encryption/decryption roundtrip.
- `DeadbandAndThrottlingTests`: Absolute/Percentage deadbands, heartbeat TTL, CDAB float decoding, contiguous register batch optimization.
- `AgentRecipeMergerTests`: CSTK deduplication, min-interval scheduling, deepest inspection depth, dynamic orphan cleanup.
- `CacheAndRealTimeTicketingTests`: Hybrid L1/L2 Redis caching, atomic factory execution, write-through ticket invalidation, `MaintenanceHub` SignalR broadcasts.
- `DatabaseTests`: TPT entity persistence, `EncryptedStringConverter` AES-256-GCM roundtrip, graph-relational edges.
- `InventorySearchTests`: Compound tag-based search (`name:`, `manufacturer:`, `type:`).
- `GrpcCommsTests`: In-memory `ReportSystemInfo` gRPC integration testing.

#### Frontend Vitest Suites Breakdown (67 Tests):
- `TelemetryTypesAndZod.test.ts`: Zod telemetry data point & Station 1 schema validation.
- `AssetReferenceCache.test.ts`: OEM/importer deduplication, tech stack harvesting, dynamic reactive cache registration.
- `JsonTemplatingEngineAndAssetEditor.test.ts`: Variable interpolation, filter pipes, system variables, 5-tab UI switching, edit record hydration.
- `OmniSearchAndAutoTagging.test.ts`: Damerau-Levenshtein distance, IP/MAC/Station code extraction, unit constraints, tag parsing.
- `Tickets.test.ts`: Kanban drag-and-drop, priority badges, detail drawer status workflow, `useMaintenance` live SignalR reactivity.
- `AllPagesAndRouting.test.ts`: DXF CAD map entity & pin handle rendering, `InventoryTreeTable`, `UserTable`, `ControllerGrid`.
- `MaintenanceInterfaceAndAdapters.test.ts`, `DataFetchingAndDisplay.test.ts`, `BffHandlersAndOffline.test.ts`, `QrScanner.test.ts`, `auth.test.ts`, `AuthAndTenantContext.test.ts`, `SidebarTests.test.ts`.

---
*Document complete. Reflects the authoritative, verified state of the Heimdall codebase.*
