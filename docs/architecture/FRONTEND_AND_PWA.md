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
- Built-in Galois Field GF(256) matrix generator with Reed-Solomon Error Correction Level M.
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
* **Handle Linking**: Each machine block in the CAD drawing has an AutoCAD handle (e.g., `5A1F` or `L01-OP010`).
* **Station Association**: In the map editor, clicking a machine block binds its handle to a `ProductionStation.PinnedObjectHandle`.
* **Hover Interaction**: Hovering over a computer row in `/dashboard/clients` highlights its physical machine bounding box on the map canvas.

### 5.3 Real-Time Telemetry HUD Overlays & Expandable Bubbles (`StationTelemetryBubble.vue`)
To eliminate context switching between separate analytics tabs and the physical shopfloor geography, Heimdall layers an interactive HTML HUD directly over the SVG CAD map canvas:

1. **Synchronized Viewport Transform & Dynamic Boundary Clamping**:
   A reactive coordinate transformation maps CAD SVG user coordinates $(x, y)$ into viewport screen pixels using the active viewBox and container bounds:
   $$\text{screenX} = \text{offsetX} + (x - \text{viewBox.x}) \times \text{scale}$$
   $$\text{screenY} = \text{offsetY} + (y - \text{viewBox.y}) \times \text{scale}$$
   To guarantee bubbles and expanded cards remain 100% within the visible canvas viewport without being cut off by page headers, navigation toolbars, or screen edges:
   * **Horizontal Viewport Clamping**: Bubble X coordinates are clamped between $X_{\min}$ (14px) and $X_{\max} - W_{\text{card}}$ (container width - 14px), preventing horizontal overflow.
   * **Vertical Flip Strategy**: If placing the card above the station node overflows the upper boundary ($Y_0 - H - \text{gap} < Y_{\min}$, e.g. < 62px below the HUD toolbar), the placement dynamically flips below the station ($Y_0 + \text{gap}$).
   * **Strict Height Bounding**: Cards are constrained to `max-h-[min(500px,calc(100vh-140px))]` with custom smooth scrollbars, ensuring complete legibility across smaller displays.
   * **Accordion Single Focus**: Expanding an equipment card automatically collapses adjacent bubbles, eliminating card occlusion across dense workstation lines.

2. **Compact Floating Bubble**:
   * **OEE Pill**: Color-coded metric indicator ($\ge 85\%$ Emerald, $70-84\%$ Amber, $< 70\%$ Rose).
   * **Yield & Throughput**: Displays first-pass yield percentage and parts/hour rate (e.g. `99.2% · 142/h`).
   * **SLA Urgency Alert**: Shows active ticket count and warning status (e.g. `⚠️ 2 SLA`, `🚨 SLA Breached!`).

3. **Expanded Glassmorphic HUD Card**:
   Clicking any bubble expands it into an equipment inspection card:
   * **Operational Status Pill**: `RUNNING`, `IDLE`, `WARNING`, `FAULTED` with animated pulse indicators.
   * **OEE Breakdown**: Gauge and micro progress bars for Availability %, Performance %, and Quality %.
   * **First-Pass Yield**: Total Conforming Parts vs Scrap Parts.
   * **Output Cadence**: Parts / Hour vs target (150/h) and measured cycle tact time.
   * **Active SLA Incidents**: Real-time list of open maintenance tickets with priority badge, status, and direct link to ticket resolution drawer.
   * **Direct Actions**: "+ Ticket" incident creation button and "Select Node" inspector focus.

4. **HUD Map Controls Bar**:
   * **Telemetry HUD Toggle**: Master toggle to show/hide overlays with pulsing status indicator.
   * **Filter Modes**:
     * `Alerts & SLA`: Automatically focuses only on stations with active maintenance tickets, low OEE (< 85%), or SLA breaches.
     * `All Nodes`: Shows telemetry bubbles across every station in the production hall.
     * `Hover Focus`: Displays the HUD bubble only for the currently hovered or selected station.
   * **Fleet Metric Ticker**: Real-time production hall aggregate of Average OEE, Average Yield, Fleet Output Rate, and Active SLA tickets count.

5. **Sub-Second Dynamic Oscillation Streaming & 3D Digital Twin HUD Viewport**:
   * **SignalR Live Stream**: Direct integration with `/hubs/maintenance` listening to `StationTelemetryReceived` and `StationOscillationReceived` events for sub-second machine oscillations.
   * **SVG Sparkline Waveform**: Live rolling vibration RMS oscilloscope waveform with gradient fill and pulse dot rendered directly inside expanded HUD bubbles.
   * **3D Three.js Projection**: `useCadModelViewer.ts` calculates real-time 2D screen coordinates from 3D mesh bounding boxes:
     $$\text{screenX} = (v_{\text{ndc}}.x \times 0.5 + 0.5) \times W_{\text{canvas}}$$
     $$\text{screenY} = (-v_{\text{ndc}}.y \times 0.5 + 0.5) \times H_{\text{canvas}}$$
   * **3D Floating HUD Bubbles**: `Machine3dDigitalTwin.vue` displays interactive telemetry badges and expandable cards over mechanical component slots (`GANTRY-X-01`, `TOOL-HEAD-01`, `CONVEYOR-01`), supporting isometric camera resets, wireframe toggles, and health heatmaps.

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
* **IPv4 Addresses**: `\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b` → Filters by controller IP.
* **MAC Addresses**: `\b([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})\b` → Filters by hardware address.
* **Station Identifiers**: `LINE-[A-Z0-9]+-OP[0-9]+` → Filters by factory floor station code.
* **Ticket Identifiers**: `TKT-[0-9]+` → Direct navigation to maintenance ticket.
* **Technical Constraints**: `24V`, `1500RPM`, `60FPS` → Filters JSONB specs metadata.

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
* **`useGlobalContextMenu()`**: Manages application-wide right-click context menu state, viewport bounding, and target entity context data.
* **`useAssetReferenceCache()`**: Caches manufacturers, suppliers, and known metadata keys across form modals.
* **`useDashboard()`**: Provides summary KPI metrics, active edge node counts, and alert feeds.

### Core Components:
* **`InteractiveMapCanvas.vue`**: AutoCAD DXF vector map renderer with zoom/pan, context menus, and clickable anchor pins.
* **`GlobalContextMenu.vue`**: Viewport-clamped context menu providing deep entity navigation and native browser menu pass-through.
* **`TicketMetricsOverview.vue`**: Interactive hero metrics banner with accessible click-to-filter, active rings, and clear toggling.
* **`ClientDetailsModal.vue`**: Detailed inspector modal for IPC nodes displaying hardware specs, software packages, and disk storage bars.
* **`AssetTabbedEditor.vue`**: 5-tab dynamic asset creation and editing modal.
* **`QrScannerModal.vue`**: Camera-based barcode and QR code scanner for mobile floor inspections.
* **`Search.vue`**: Debounced search input with dynamic tag suggestions.

---

## 9. Global Application Context Menu & Interaction System

The platform provides a unified right-click context menu mounted at the root (`app.vue`) via `GlobalContextMenu.vue` and `useGlobalContextMenu.ts`:
- **Boundary-Aware Clamping**: Dynamically checks window inner width and height, shifting the menu upward or leftward if rendered near viewport edges to prevent clipping.
- **Deep Domain Navigation**:
  - **Go to Machine**: Routes directly to `/dashboard/machines?id=...` with automatic scrolling and search filtering.
  - **Go to Node / Controller**: Routes to `/dashboard/clients?selected=...`.
  - **Go to Owner Team / Person**: Filters incidents by team or assigned engineer (`/dashboard/tickets?team=...`).
  - **Go to Ticket**: Navigates to open tickets or triggers incident creation prefilled with equipment metadata.
  - **Live Telemetry & Diagnostics**: Navigates to `/dashboard/telemetry` for live sensor stream inspection.
  - **Inspect Component Tree**: Opens the Station Component Tree modal directly.
  - **Copy Identifier / Handle**: One-click clipboard copy with animated visual feedback.
- **Native Browser Pass-Through**:
  - Contains an explicit menu action **"Open Browser Context Menu"** to reveal native browser options.
  - **Shift + Right-Click Bypass**: Holding `Shift` while right-clicking unconditionally opens the native browser menu anywhere in the application.

---

## 10. Dashboard Route Architecture & Interactive Hero Filtering

### 10.1 Descriptive Computer Science Naming (Zero `index.vue`)
To eliminate ambiguous file tabs in development environments, all dashboard pages use clean, descriptive CS names while preserving 100% URL route parity via Nuxt 4 routing and aliases:
- `/dashboard` ➔ `pages/dashboard/overview.vue` (with `alias: ['/dashboard']`)
- `/dashboard/analytics` ➔ `pages/dashboard/analytics.vue`
- `/dashboard/clients` ➔ `pages/dashboard/clients.vue`
- `/dashboard/telemetry` ➔ `pages/dashboard/telemetry/stream.vue` (with `alias: ['/dashboard/telemetry']`)
- `/dashboard/admin` ➔ `pages/dashboard/admin/system-settings.vue` (with `alias: ['/dashboard/admin']`)

### 10.2 Interactive Hero Metrics Filtering (`TicketMetricsOverview.vue`)
- Incident metric cards (Total Open, Critical, Pending Parts, Overdue SLA, Resolved, SLA Health) serve as accessible toggle buttons (`role="button"`, `tabindex="0"`, Enter/Space keybindings).
- Clicking applies the corresponding filter with an active colored ring (`ring-2 ring-indigo-500`, `ring-rose-500`, etc.) and slight elevation.
- Clicking an active card toggles the filter off.
- An active filter chip bar renders below the cards displaying matched incident counts and a direct "Clear Filter" button.

### 10.3 Intuitive Page Header Click-to-Reset Actions
Clicking any page header title or icon block across dashboard views (`tickets.vue`, `machines.vue`, `clients.vue`, `analytics.vue`, `map.vue`, `stream.vue`) acts as an intuitive reset action:
- Clears active search queries and filter chips.
- Cleans URL query parameters (e.g. `?id=...`, `?selected=...`).
- Restores default view modes (e.g. list, grid).
- Triggers fresh live data fetching and SignalR re-synchronization.

---

## 11. 4-Tier Navigation Hierarchy & Dedicated OT Workspaces

To ensure intuitive discoverability and eliminate modal popup congestion, the platform organizes its frontend navigation into four explicit operational tiers:

```
┌────────────────────────────────────────────────────────────────────────┐
│                      4-TIER SIDEBAR NAVIGATION                         │
├─────────────────┬──────────────────┬─────────────────┬─────────────────┤
│   MANAGEMENT    │       DATA       │   MAINTENANCE   │    SETTINGS     │
├─────────────────┼──────────────────┼─────────────────┼─────────────────┤
│ * Dashboard     │ * Live Telemetry │ * Tickets       │ * Users & Roles │
│ * Inventory     │ * Fleet Analytics│ * Delegations   │ * Organizations │
│ * Machines      │ * Templates      │                 │ * Security Grps │
│ * Machine Groups│ * Telemetry Cfg  │                 │ * Governance    │
│ * Client PCs    │                  │                 │ * Identity Studio│
│ * Plant Map     │                  │                 │ * User Settings │
│                 │                  │                 │ * Help & Support│
└─────────────────┴──────────────────┴─────────────────┴─────────────────┘
```

### 11.1 Dedicated Delegations & Roster Workspace (`/dashboard/delegations`)
Elevated from an overburdened modal into a first-class responsive workspace:
* **Shift Attendance Tab**: Live clock-in tracking, absence registration (Sick, Vacation, Training), designated backup support, and Microsoft Teams Out-of-Office (OOO) status synchronization.
* **Technician Dedication Tab**: Declarative assignment rules mapping technicians to specific Machine Technologies (Milling, Screwing, AOI), Line Envelopes, or individual machines, guarded by multi-tier RBAC authority tiers (`Self`, `Shift Leader`, `Group Leader`, `Manager`).
* **Machine Group Clusters Tab**: Recursive clustering of factory automation cells and technology groups.
* **Live Role Simulator Bar**: Integrated persona bar (`DEMO_PERSONAS`) enabling instant verification of role-based governance policies.

### 11.2 Dedicated Machine Groups & Factory Hierarchy (`/dashboard/machine-groups`)
Dedicated workspace for managing production envelopes:
* **Hierarchy Tree Explorer**: Multi-level tree visualizing Plant Level &rarr; Production Lines &rarr; Automation Cells &rarr; Workstations.
* **Technology Clusters A through X**: Real-time assignment of equipment specializations (Optical Inspection, Screwing, Soldering, Milling, Pressing, Manipulators).
* **Engineering Ownership**: Dedicated lead engineer metadata, operational descriptions, and line capacity notes.
* **Full CRUD Lifecycle**: Integrated with `/api/machine-groups` REST endpoints for real-time creation, editing, and reparenting.

---

## 12. Universal Tag-Context Omnisearch Engine

The platform incorporates an omnipresent, tag-contextual fuzzy search architecture accessible via `Cmd+K` / `Ctrl+K` across all views:

### 12.1 Scoping Syntaxes & Auto-Tagging
* **Prefix Scoping Symbols**:
  * `@station` or `@machine`: Scopes search strictly to machines and automated workstations (`[MCH]`).
  * `#ticket`: Scopes to maintenance and incident tickets (`[TCK]`).
  * `!controller` or `!host`: Scopes to IPC edge nodes and Soft-PLCs (`[IPC]`).
  * `*part` or `*inventory`: Scopes to discrete spare parts and consumable stock (`[PRT]`).
  * `%alarm`: Scopes to machines and tickets in warning, alarm, or line stop status.
* **Key-Value Expressions**:
  * Supports exact, negated, and multi-value tag syntax: `mfr:Siemens`, `tech:Robotics,Assembly`, `-status:offline`, `line:Line01`.
* **Fuzzy Token Matching**:
  * Powered by FZF-inspired Damerau-Levenshtein distance and N-gram scoring.
  * Dynamically harvests known metadata keys and values across registered provider schemas via `searchRegistry.ts`.
  * Suggests autocomplete chips with colored entity indicators (`[MCH]`, `[IPC]`, `[TCK]`, `[PRT]`, `[USR]`).

### 12.2 Search Registry & Provider Extensibility (`searchRegistry.ts`)
* Implements a decoupled provider architecture (`SearchProvider<T>`) registering data tables, search columns, metadata extractors, and routing links.
* Defaults include `machines`, `client_pcs`, `tickets`, `inventory`, `users`, `templates`, and `telemetry`.
* Supports cross-table search ranking: primary workspace items are scored with high relevance, while correlated secondary items appear in the "Cross-Domain Results" section.

---

## 13. Enterprise Notification Center, Optimistic UI & Unified Audit Trail

### 13.1 Notification Center Popover & Toast Overlay
* **`NotificationCenterPopover.vue`**: Mounted in the top header, displaying unread badges, multi-source alerts (machine alarms, ticket escalations, Copia Git staging, system policies), and one-click "Mark All as Read" actions.
* **`NotificationToastOverlay.vue`**: Fixed top-right toast notification manager providing non-blocking visual feedback, duration timeouts, action triggers, and undo capabilities.
* **Out-of-App Web Notifications**: Subscribes to HTML5 / Android Web Notification API for critical line-stop alarms when the browser tab is backgrounded.

### 13.2 Optimistic Actions with Rollback (`runOptimisticAction`)
* Industrial operations (e.g. ticket reservation, status change, equipment transfer) update the Vue reactive state immediately.
* If the asynchronous network request fails or is rejected by backend RBAC:
  1. Executes the provided `rollbackFn()` to restore previous state.
  2. Surfaces a high-priority error toast alerting the operator.
  3. Dispatches a system audit notification detailing the rollback reason.

### 13.3 Unified Audit Log Visualizer (`AuditLogsVisualizer.vue`)
* Integrated into `/dashboard/analytics` under the "Audit Logs" tab.
* Ingests and renders unified audit records spanning tickets, machines, inventory stock movements, policy changes, and machine PLC auto-closures.
* Provides search, entity filtering, severity badges, and an interactive cryptographic diff viewer displaying full JSON payloads.



