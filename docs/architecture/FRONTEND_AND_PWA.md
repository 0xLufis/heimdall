# Frontend, Real-Time PWA & Spatial UI Architecture

This document details the architecture, component design, reactive state composables, offline synchronization, and spatial CAD map engine of the Heimdall Web Frontend (`frontend/web`).

---

## 1. Application Topology & Nitro BFF

The frontend is implemented as a Nuxt application with a server-side **Nitro Backend-for-Frontend (BFF)** layer. This design offloads data aggregation, session validation, and header injection from the browser client.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                             BROWSER / MOBILE PWA                            │
│                                                                             │
│  ┌───────────────────────┐  ┌───────────────────────┐  ┌──────────────────┐ │
│  │   Vue 3 Components    │  │ Reactive Composables  │  │ IndexedDB Offline│ │
│  │   (Kanban, CAD Map,   │◄─┤ (useMaintenance,      │◄─┤ Multi-Store Cache│ │
│  │   Asset DNA Editor)   │  │  useOmniSearch)       │  │ (idb v8)         │ │
│  └───────────────────────┘  └───────────┬───────────┘  └──────────────────┘ │
└─────────────────────────────────────────┼───────────────────────────────────┘
                                          │ HTTP / WebSockets
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           NITRO BFF SERVER LAYER                            │
│                                                                             │
│  * Reverse Proxy Route: /api/proxy/*                                        │
│  * Session Authentication & Token Verification (Better-Auth)                │
│  * Multi-Tenant Header Injection: X-Organization-Id                         │
└─────────────────────────────────────────┬───────────────────────────────────┘
                                          │ HTTP REST (Port 5099)
                                          ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CENTRAL BACKEND API                               │
│                         (ASP.NET Core / PostgreSQL)                         │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.1 Nitro BFF Reverse Proxy (`server/api/proxy/[...path].ts`)
Browser clients do not connect directly to internal backend microservice ports. All API communication flows through the Nitro BFF reverse proxy:
1. Validates the active user session cookie via Better-Auth.
2. Extracts the user's active tenant organization ID from session state.
3. Injects `Authorization: Bearer <token>` and `X-Organization-Id: <org_id>` request headers.
4. Forwards the request to the central backend API and streams the response back to the browser.

---

## 2. Live Maintenance Ticketing & 8-Stage Kanban Architecture

The maintenance ticketing module (`/dashboard/tickets`) coordinates real-time equipment incident handling, field telemetry snapshots, error template auto-fill, and photographic audit trails across an 8-column Kanban lifecycle.

```
┌──────────┐   ┌─────────────┐   ┌───────────────┐   ┌───────────┐
│   Open   │──►│ In Progress │──►│ Pending Parts │──►│ Resolved  │
└────┬─────┘   └──────┬──────┘   └───────────────┘   └─────▲─────┘
     │                │                                    │
     │                ├────────────────────────────────────┤
     ▼                ▼                                    │
┌──────────┐   ┌────────────────────┐            ┌─────────┴─────────┐
│Escalated │   │ Escalated External │            │  Closure Pending  │
│ (Safety) │   │  (Vendor / SAP)    │            │ (AOK Calibration) │
└──────────┘   └────────────────────┘            └─────────┬─────────┘
                                                           │
                                                           ▼
                                                 ┌───────────────────┐
                                                 │ Closed Unresolved │
                                                 └───────────────────┘
```

### 2.1 Kanban Status Columns & Operational Roles
* **`Open`**: Newly reported incidents awaiting review.
* **`In Progress`**: Active diagnostics or repair by designated technician.
* **`Pending Parts`**: Paused awaiting hardware components or seals.
* **`Escalated`**: Safety-critical hardware lockouts (SIL desynchronization, light curtain muting faults).
* **`Escalated External`**: Escalated to external enterprise teams or OEM vendors (e.g., SAP MES RFC dropout).
* **`Closure Pending`**: Work complete, awaiting formal calibration verification or outside AOK sign-off.
* **`Resolved`**: Verified operational, fully documented, and closed with MTTR metrics.
* **`Closed Unresolved`**: Ticket retired without corrective action (e.g., duplicate, obsolete station).

### 2.2 Error Template Engine (`utils/errorTemplateEngine.ts`)
Standardizes incident reporting across 4 industrial categories: `Prevention`, `Error`, `Improvement`, and `ETC`. Selecting a template automatically populates:
- Incident title and detailed diagnostic description.
- Standardized error codes (e.g., `E-MOT-01`, `E-SAFE-01`, `P-CAL-01`).
- Function block states (e.g., `FB_AxisControl` in `ERROR_STOP`).
- Telemetry keys to sample (e.g., `following_error_mm`, `motor_current_A`).
- SFC serial tagging (`#SFC-...`).

### 2.3 Zero-Dependency SVG Action QR Renderer (`utils/qrSvgRenderer.ts`)
To prevent SSR container crashes from native Node/canvas QR dependencies, Heimdall implements an internal pure TypeScript QR generator:
- Built-in Galois Field $GF(256)$ generator with Reed-Solomon Error Correction Level M.
- Synchronously renders vector SVG data URLs (`data:image/svg+xml;utf8,...`) without native canvas or C++ bindings.
- Fully compatible with browser, Bun, Node.js, and Nitro SSR.
- Supports composable URI actions (`report-incident`, `inspect-machine`, `claim-ticket`) via `utils/qrActionGenerator.ts`.

### 2.4 Technician Delegation & Attendance Engine (`utils/technicianInheritance.ts`)
Resolves preferred technicians dynamically through a 4-tier hierarchy:
1. **Machine Override**: Specific technician assigned directly to an individual machine.
2. **Line / Group Rule**: Technician dedicated to an entire production cell or assembly line.
3. **Technology Rule**: Engineer dedicated to a machine type (e.g., `Milling`, `AOI`, `Pressing`).
4. **Shift Absence Fallback**: If the primary technician is marked absent (`Sick`, `Vacation`, `Emergency`) or out of office, tickets automatically route to designated backup personnel.

---

## 3. Real-Time SignalR WebSocket Push Integration

To deliver instantaneous alerts without wasteful client polling, the frontend integrates with the backend's SignalR hub (`/hubs/maintenance`):

### 3.1 Lifecycle & Reconnection Strategy
The maintenance service initializes a `HubConnection` with automatic reconnection:
```typescript
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/maintenance', {
    accessTokenFactory: () => authSessionToken.value
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build();
```

### 3.2 Real-Time Events
* `TicketCreated`: Appends the newly submitted ticket to the active board and triggers a device vibration pattern (`navigator.vibrate([100, 50, 100])` on mobile devices).
* `TicketStatusUpdated`: Updates the status column of the target ticket in real time across all connected technician screens.
* `NewTicketComment`: Injects new technician notes into the open ticket conversation drawer.
* `CriticalAlertRaised`: Displays an alert banner when an edge controller reports an emergency stop or critical hardware fault.

---

## 4. Progressive Web App (PWA) & Offline Storage

Industrial plants frequently have areas with weak or shielded Wi-Fi coverage. Heimdall implements an **Offline-First** strategy allowing technicians to view cached tickets and record actions while completely disconnected.

### 4.1 IndexedDB Multi-Store (`OfflineQueueMaintenanceProvider.ts`)
The offline layer uses IndexedDB database `heimdall-maintenance-db` with two stores:
1. **`cached-tickets`**:
   * Stores complete JSON representations of all fetched tickets.
   * Enables immediate read availability upon app launch, even with zero network connectivity.
2. **`offline-mutation-queue`**:
   * Captures actions executed while offline: ticket creation, status updates, technician notes.
   * Records are serialized with client timestamp and retry counters:
     ```typescript
     interface QueuedMutation {
       id: string;
       action: 'CREATE_TICKET' | 'UPDATE_STATUS' | 'ADD_COMMENT';
       payload: Record<string, any>;
       timestamp: number;
     }
     ```

### 4.2 Automated Replay on Reconnection
When network connectivity returns (`window.addEventListener('online')`):
1. The provider locks the queue to prevent concurrent modifications.
2. Mutations are drained and replayed against the server in chronological FIFO order.
3. The local cache is refreshed with authoritative server state.

### 4.3 Service Worker (`public/sw.js`)
* **Pre-caches**: Core application shell, stylesheets, icons, and fonts.
* **Network-First Strategy**: Applies to dynamic `/api/proxy/*` data routes, falling back to cached responses when offline.
* **Cache-First Strategy**: Applies to static assets, SVG icons, and plant DXF layout files.

---

## 5. Interactive Plant Map Engine (`/dashboard/map`)

Heimdall renders factory floor AutoCAD DXF drawings as interactive, vector-based SVG layouts on an HTML5 canvas.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          INTERACTIVE PLANT MAP                              │
│                                                                             │
│  [Zoom: 120%] [Center View] [Layer: Production Floor A]                    │
│ ┌─────────────────────────────────────────────────────────────────────────┐ │
│ │                                                                         │ │
│ │   ┌───────────────┐                  ┌───────────────┐                  │ │
│ │   │  LINE-01-OP10 │                  │  LINE-01-OP20 │                  │ │
│ │   │  [Robot Cell] │                  │  [Press Sta.] │                  │ │
│ │   │   Status: OK  │                  │  Status: WARN │                  │ │
│ │   └───────┬───────┘                  └───────┬───────┘                  │ │
│ │           │                                  │                          │ │
│ │           ▼                                  ▼                          │ │
│ │   (Pinned: IPC-01)                   (Pinned: IPC-02)                   │ │
│ │                                                                         │ │
│ └─────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 5.1 DXF Parsing & SVG Pipeline
1. Plant floor layout files (`production_hall.dxf`, `LINE-A.dxf` through `LINE-J.dxf`) are loaded from `public/sample/`.
2. The parser extracts DXF `ENTITIES` sections (lines, polylines, circles, blocks, text labels).
3. Entities are converted into SVG `<path>` and `<g>` elements, preserving CAD layers and entity handles.
4. The canvas applies a pan-and-zoom transformation matrix with smooth mouse wheel and touch gesture support.

### 5.2 Spatial Asset Pinning
* **Handle Linking**: Each machine block in the CAD drawing has an AutoCAD handle (e.g., `5A1F`).
* **Station Association**: In the map editor, clicking a machine block binds its handle to a `ProductionStation.PinnedObjectHandle`.
* **Hover Interaction**: Hovering over a computer row in `/dashboard/clients` highlights its physical machine bounding box on the map canvas.

---

## 6. Dynamic 5-Tab Asset Template Engine (`AssetTabbedEditor.vue`)

Managing diverse industrial equipment (sensors, servo drives, IPCs, software licenses, dispensing nozzles) requires flexible data input without hardcoding rigid database columns.

`AssetTabbedEditor.vue` organizes asset configuration across 5 tabs:

| Tab | Purpose | Fields & Functionality |
| :--- | :--- | :--- |
| **1. Identity** | Core Identification | Asset Name, Display Label, Equipment Type, Manufacturer (with inline modal creation), Model Number, Serial Number (with auto-generation button), Responsible Teams. |
| **2. Topology** | Spatial & Network Graph | Assigned Station (`LINE-A-OP10`), Managing IPC / Controller, Parent Assembly (recursive tree), Lateral Interconnects. |
| **3. Commercial** | Financial Asset Tracking | Procurement Cost in HUF (`costInHUF`), Quantity, Supplier Vendor, Purchase Date, Calculated Total Valuation. |
| **4. Specs** | Semi-Structured Attributes | Dynamic key-value pairs stored in PostgreSQL JSONB `metadata`. Supports types: `string`, `number`, `boolean`, `json`. Autocomplete suggests keys from `useAssetReferenceCache`. |
| **5. Templates** | Templating & Batch Provisioning | Reusable property templates categorized by equipment domain (`Sensor`, `Vision`, `Motion`, `Dispenser`, `Safety`). Supports variable interpolation and JSON import/export. |

### 6.1 Template Variable Interpolation & Filter Pipeline
Templates support dynamic token replacement during asset provisioning:
* Syntax: `{{variable | filter}}`
* Supported Filters:
  * `uppercase`: Converts string to uppercase.
  * `slugify`: Converts text to URL/system slug format.
  * `padzero(length)`: Left-pads numeric strings with zeroes.
* Dynamic System Tokens:
  * `$uuid`: Generates a fresh RFC 4122 v4 UUID.
  * `$timestamp`: Inserts current ISO 8601 UTC timestamp.
  * `$randomSerial(prefix)`: Generates an alphanumeric serial string.

---

## 7. OmniSearch & AutoTagging Engine

The global search interface (`useOmniSearch.ts`, `AutoTagEngine.ts`) supports multi-attribute fuzzy search and entity extraction across all assets, controllers, and tickets.

### 7.1 Entity Extraction
The tokenizer detects structured patterns in search queries:
* **IPv4 Addresses**: `\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b` $\to$ Filters by controller IP.
* **MAC Addresses**: `\b([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})\b` $\to$ Filters by hardware address.
* **Station Identifiers**: `LINE-[A-Z0-9]+-OP[0-9]+` $\to$ Filters by factory floor station code.
* **Ticket Identifiers**: `TKT-[0-9]+` $\to$ Direct navigation to maintenance ticket.
* **Technical Constraints**: `24V`, `1500RPM`, `60FPS` $\to$ Filters JSONB specs metadata.

### 7.2 Fuzzy Matching (Damerau-Levenshtein Distance)
Tolerates typing mistakes, character transpositions, and partial prefixes across asset models, manufacturer names, and serial numbers.

### 7.3 Tag Queries
Supports explicit key-value queries:
`manufacturer:beckhoff type:ipc status:online`

---

## 8. Frontend Component & Composable Catalog

### Core Composables:
* **`useMaintenance()`**: Manages Kanban tickets, SignalR subscription lifecycle, status mutation queues, and offline fallback.
  * `tickets`: Reactive list of maintenance tickets.
  * `updateTicketStatus(id, status)`: Transitions a ticket status with optimistic UI update.
  * `addComment(id, text)`: Appends a comment to a ticket.
* **`useOmniSearch()`**: Manages the global search modal, debounced querying, and filter recommendations.
  * `query`: Current search string.
  * `results`: Search result items with match scoring.
  * `performSearch(text)`: Executes fuzzy search query.
* **`useAssetReferenceCache()`**: Caches manufacturers, suppliers, and known metadata keys across form modals.
* **`useDashboard()`**: Provides summary KPI metrics, active edge node counts, and alert feeds.

### Core Components:
* **`InteractiveMap.vue`**: SVG vector map renderer with zoom/pan and clickable anchor pins.
* **`ClientDetailsModal.vue`**: Detailed inspector modal for IPC nodes displaying hardware specs, software packages, and disk storage bars.
* **`AssetTabbedEditor.vue`**: 5-tab dynamic asset creation and editing modal.
* **`QrScannerModal.vue`**: Camera-based barcode and QR code scanner for mobile floor inspections.
* **`Search.vue`**: Debounced search input with dynamic tag suggestions.
