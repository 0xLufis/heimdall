1. System Architecture & Tech Stack+-----------------------------------------------------------------------------------------------------------------+
   | Browser Presentation Layer |
   | +--------------------+ +----------------------+ +---------------------+ +--------------------------------+ |
   | | Enterprise Portal | | Shopfloor Web Kiosk | | 3D / CAD Viewport | | Widget & Dashboard Canvas | |
   | +--------------------+ +----------------------+ +---------------------+ +--------------------------------+ |
   +-----------------------------------------------------------------------------------------------------------------+
   | | |
   +----------------------------------------------------+--+--+------------------------------------------------------+
   | Frontend Application Core |
   | +-------------------+ +--------------------+ +-----------------------+ +---------------------------------+ |
   | | React 19 / TS 5.x | | TanStack Query v5 | | State (Zustand) | | Layout Engine & UI Toolkit | |
   | | (Vite / Next.js) | | (Server State/Ops) | | (Session/Drafts/Cols) | | (Cards, Grids, Kanban, Canvas) | |
   | +-------------------+ +--------------------+ +-----------------------+ +---------------------------------+ |
   | +-----------------------------------------------------------------------------------------------------------+ |
   | | Real-Time Ingestion Client: MQTT over WebSockets / WebTransport / gRPC-Web (Binary Protobuf Streams) | |
   | +-----------------------------------------------------------------------------------------------------------+ |
   | +-----------------------------------------------------------------------------------------------------------+ |
   | | Hybrid Search Engine Client: Scoped Contextual Filter <----> Global Omnibox (IndexedDB + Meilisearch) | |
   | +-----------------------------------------------------------------------------------------------------------+ |
   +-----------------------------------------------------------------------------------------------------------------+
   | |
   v (HTTPS / WSS / gRPC) v (MQTT / ADS Bridge)
   +---------------------------------------------------+ +----------------------------------------------------------+
   | Backend Services | | Edge Gateways & PLCs |
   | - Identity / AD Sync (OIDC / SCIM) | | - TwinCAT ADS over MQTT Broker |
   | - Ticketing, Escalation & Delegation Engines | | - OPC UA High-Frequency PubSub |
   | - OOP Part Inventory & Asset DB | | - Edge PC Daemon (Hardware, Processes, Recipe Diffs) |
   | - AI Analytics (Local Ollama / Cloud LLM) | | - Active Directory Enrollment Provider |
   +---------------------------------------------------+ +----------------------------------------------------------+
   Core Frontend StackFramework: React 19 (TypeScript 5.x) built with Vite (SPA mode) for low-overhead edge kiosk rendering and rapid HMR, or Next.js App Router for server-rendered analytical views.State Management:Server State & Caching: TanStack Query v5 (optimistic mutations, aggressive garbage collection for telemetry feeds).Client/Transient State: Zustand with slice partitioning (UI layouts, search filters, canvas interactions).High-Frequency Real-time Stream: Direct ref-based canvas updates and RxJS event pipelines bypassed from the React render cycle to prevent UI thrashing at >100 Hz update rates.Data Transport: MQTT over WebSockets (mqtt.js), WebTransport/gRPC-Web for binary telemetry payloads (Protobuf), REST/GraphQL for CRUD operations.Visualization & Rendering:2D Telemetry & Charts: Apache ECharts (Canvas/SVG hybrid) or Chart.js with WebGL-accelerated plugins.3D / CAD Visualizer: Three.js / React Three Fiber (R3F) with three-stdlib for glTF/STEP parsing and OpenCascade.js WASM for raw CAD formats.Styling & Components: Tailwind CSS, Radix UI primitives, Lucide icons, @tanstack/react-table (virtualized tables), @hello-pangea/dnd or @dnd-kit (Kanban boards).2. Module 1: Telemetry Hub & Edge Asset Monitoring2.1 PC Telemetry EngineCaptures operating parameters from IPCs (Industrial PCs) and client terminals running Windows/Linux:Host Metrics: CPU usage per core, thread states, thermal profiles, RAM allocation (committed vs. physical), swap activity, storage drive wear levels (SMART attributes, NVMe endurance), read/write throughput, and NIC packet loss/jitter.Process Watcher & Binary Auditing:Running software processes, PIDs, memory usage, CPU affinity, and crash logs.Binary and recipe auditing: Real-time SHA-256 fingerprinting of running executables, TwinCAT solution files (.tszip, .sln), config files (.json, .xml, .ini), and machine recipe files.Embedded Hex/Text Diff Inspector: Side-by-side view comparing deployed configs against golden master versions in the central repository, flagging drift and unauthorized shopfloor modifications.2.2 PLC Telemetry (TwinCAT ADS & OPC UA)Ingestion Protocols: Beckhoff TwinCAT ADS over MQTT / WebSockets, and OPC UA over MQTT (JSON / binary PubSub).Interactive I/O Tree Viewer:Hierarchical tree view replicating Beckhoff TwinCAT System Manager / I/O topology: I/O Devices -> Master -> Couplers (EK1100) -> Terminals (EL1xxx, EL2xxx, EL3xxx) -> Sub-channels.Visual status indicators: EtherCAT State Machine indicators (INIT, PREOP, SAFEOP, OP), CRC error counts per port, missing frames, and WKC (Working Counter) failures.Variable Watcher & State Machine Tracker:Real-time variable subscription table with dynamic search by AMS Net ID, Port (e.g., 851 for TwinCAT 3 PLC runtime), and symbol path (e.g., MAIN.fbStation1.stAxisStatus).Live oscilloscope/strip-chart widget capable of tracking high-frequency float/boolean/integer arrays.2.3 Visual Telemetry Templating & Channel GroupingAcquisition Schema Designer: Visual node-based schema builder allowing administrators to define telemetry schemas by acquisition method:TwinCAT ADS Bridge (Polling rate, AMS Net ID, Port, Symbol address).OPC UA Client (NodeID, Namespace index, Sampling interval, Deadband).MQTT Edge Broker (Broker URL, Topic wildcard pattern, JSON Path extraction).OS Edge Agent (System metrics, Heartbeat intervals, File watch patterns).Tag-Based Propagation Rules: Apply acquisition templates automatically to clients using tag expressions (e.g., Env == "Production" AND Controller == "Beckhoff-C6030" AND Line == "Powertrain-01").Component Binding: Drag-and-drop linking between raw telemetry channels and specific structural components in the machine assembly tree.3. Module 2: Industrial Ticketing, Maintenance & Escalation Engine3.1 Taxonomy & Root-Cause Attribution MatrixEvery ticket enforces strict structural categorization to feed both shopfloor triage and MTBF/MTTR analytics:CategoryPrimary TriggerLine Stop FlagAttribution DomainRoot Cause OptionsCorrective ErrorPLC Fault / Operator E-StopBoolean (True / False)Physical MachineSpindle failure, pneumatic leak, sensor misalignment, servo overloadOperational ErrorQuality Gate / Vision RejectBoolean (True / False)SAP / ERP / MESMaterial master mismatch, incorrect recipe parameters, routing blockSystem ErrorIPC Crash / Network TimeoutBoolean (True / False)IT / InfrastructureSubnet packet drop, Active Directory LDAP timeout, IPC disk fullPreventativeRuntime Counter / Sensor DriftDefault FalsePlant Operations1,000-cycle lubrication, filter replacement, calibrationKaizen / CIEfficiency / Ergonomic SuggestionDefault FalseEngineeringCycle time reduction, clamp redesign, software logic improvementTypeScriptexport type RootCauseDomain = 'MACHINE_HARDWARE' | 'PLC_LOGIC' | 'INFRASTRUCTURE' | 'ERP_MES' | 'SAP_PRODUCT_SETTING';

export interface Ticket {
id: string;
referenceNumber: string; // e.g., "TCK-2026-00492"
type: 'CORRECTIVE' | 'PREVENTATIVE' | 'IMPROVEMENT';
causesLineStop: boolean;
attributedDomain: RootCauseDomain;
status: 'OPEN' | 'IN_TRIAGE' | 'ASSIGNED' | 'ESCALATED' | 'IN_PROGRESS' | 'RESOLVED' | 'CLOSED';
severity: 'SEV1_CRITICAL' | 'SEV2_MAJOR' | 'SEV3_MODERATE' | 'SEV4_MINOR';
machineId: string;
componentPath?: string; // Target node in CAD/BOM
reportedBy: string; // User ID or Edge Kiosk Machine ID
assignedTeamIds: string[];
assignedUserIds: string[];
escalationLevel: number;
slaBreachTime: string;
createdAt: string;
}
3.2 Dynamic Escalation MatrixConfigurable rule engine driving automated multi-tier ticket escalation:Escalation Triggers:Elapsed time in current state without acknowledgment (e.g., SEV1 unassigned after 10 minutes).Line-stop condition active for >15 minutes.Direct manual trigger by shift lead.Escalation Modes:Informative (Broadcast): Push notifications, desktop banners, and SMS/Teams webhooks to stakeholders (e.g., Plant Manager, Quality Assurance) while keeping ownership intact.Collaborative (Swarming): Adds secondary functional groups (e.g., Software Automation Engineers added to on-site Mechanical Technicians) with unified incident channels and shared logs.Hand-off (Transfer of Ownership): Revokes primary ownership from Tier-1 operators and transfers write permissions to Tier-2 specialists or maintenance engineering leads.3.3 Rule-Based Delegation & Auto-DispatchDispatch engine evaluates ticket domain, machine location, and current shift schedules.Rule Format: IF Ticket.attributedDomain == 'MACHINE_HARDWARE' AND Machine.technology == 'HYDRAULICS' AND Machine.lineGroup == 'Line-B' THEN DispatchTo(Skill: 'Hydraulics-Level2', Shift: 'ActiveShift').Handles on-call shifts, operator presence status (clocked-in via badge/AD), and existing ticket load balancing.4. Module 3: Plant Topology, Organizational Matrix & Identity Management4.1 Hierarchical Grouping ArchitectureDual-tree structure supporting cross-dimensional indexing between operational assets and personnel:[Plant Topology] [Personnel Topology]
Enterprise Global Operations
└── Business Unit (e.g., E-Mobility) └── Business Unit Directors
└── Line Group (e.g., Stator Assembly) └── Technology Leads (Mechanical, Automation, IT)
└── Production Line (e.g., Line 01) └── Shift Teams (Shift A, B, C)
└── Workstation / Machine (Cell 10) └── Cross-functional Operators
└── Sub-assembly / Component ├── Skill Badges (e.g., TwinCAT ADS, KUKA KRL)
(Linked to BOM & CAD Nodes) └── Assigned Machine Matrix
4.2 Active Directory / Entra ID IntegrationUser Onboarding & RBAC:Automatic directory polling and SCIM 2.0 provisioning.Security group mapping: AD group SG_PLANT_AUTOMATION_ENG automatically assigns roles ROLE_TELEMETRY_EDITOR, ROLE_RECIPE_DEPLOYER, and grants access to Technology: PLC_TWINCAT.Management hierarchy extraction: Direct reports and department structures in AD auto-populate the escalation matrix chain of command.Machine / Client Enrollment via AD OUs:Zero-touch provisioning for edge PCs. When a new IPC joins an Active Directory Organizational Unit (e.g., OU=EdgePCs,OU=Line01,OU=Production,DC=plant,DC=internal), the telemetry client auto-registers with the frontend.Inherits line, station, and telemetry ingestion templates based on its OU path and AD computer object attributes.5. Module 4: Digital Twin, CAD & 2D/3D Spatial Canvas5.1 Architecture & FormatsRenderer Engine: React Three Fiber (R3F) running WebGL2 / WebGPU pipelines.Asset Support:Optimized glTF/GLB files for spatial navigation and real-time visualization.Server-side conversion pipeline for STEP/IGES/BIM files to lightweight, LOD-partitioned glTF streams using OpenCascade WASM workers.Interactive Features: Orthographic and perspective cameras, section planes, assembly explosion sliders, component isolates, and raycasted element picking.5.2 CAD Node Data BindingHierarchical Node Mapping: Internal 3D object nodes (e.g., node_ServoMotor_AxisZ) map 1:1 with:Hardware BOM Part Number and serial number.TwinCAT ADS / OPC UA telemetry tags.Active tickets targeting that component.Viewport Overlays & Visual States:Thermal / Vibration Heatmaps: Mesh vertex shaders interpolate live telemetry data (e.g., motor temperatures) into dynamic color gradients across the 3D model.Interactive Badges: Dynamic HTML anchors (via @react-three/drei Html) pinned to components displaying real-time metrics, active ticket counters, and MTBF health bars.Fault Highlighting: Components with active line-stop tickets pulse red, pulsing amber for pending maintenance warnings. Clicking an object opens a context panel with telemetry, historical swaps, and open tickets.6. Module 5: Object-Oriented Inventory, BOM & Spare Parts Lifecycle6.1 OOP Part Class & Template SystemSpare parts and inventory entities are structured using Object-Oriented Programming (OOP) inheritance models:[PartClass: BaseMaterial]

- Fields: partNumber, internalSku, standardCost, leadTimeDays, minStockLevel, unitOfMeasure
  │
  ├── [PartClass: ElectromechanicalComponent extends BaseMaterial]
  │ - Fields: operatingVoltage, powerRatingWatts, ipRating, operatingTempRange
  │ │
  │ ├── [PartClass: ServoMotor extends ElectromechanicalComponent]
  │ │ - Fields: ratedTorqueNm, maxRpm, encoderProtocol, shaftDiameterMm
  │ │ │
  │ │ └── [Instance: ServoMotor_Siemens_1FK7042]
  │ │ - Concrete Data: Serial #, Batch #, Installed Location: Line 1 / Stator Cell 10
  │ │
  │ └── [PartClass: PneumaticValve extends ElectromechanicalComponent]
  │ - Fields: portThreadSize, operatingPressureBar, responseTimeMs
  Custom Schema Designer: Admins can define abstract part classes, declare field inheritance, set default constraints, and instantiate concrete parts with custom extended key-value attributes.6.2 BOM Integration & Alternative Parts EngineMachine BOM Mapping: Every machine in the plant topology maintains an indented multi-level Bill of Materials (BOM).Direct Stock Linkage: Each BOM entry points to an instantiated PartClass in the central inventory warehouse.Alternative / Superseded Parts Matrix:Supports drop-in replacements, compatible alternatives, and superseded revisions.Provides visual indicators for form-fit-function compatibility, wiring variations, and firmware prerequisites.Part Swapping & Repair Audit Trail:When a technician logs a maintenance ticket, they record part replacement events.Captures: Removed Serial Number, Installed Serial Number, Failure Mode, Operating Hours at Failure, and links directly to the inventory stock decrement transaction.Inventory Control: Real-time stock counts across plant storage cribs, tracking minimum safety stock thresholds, automatic reorder point flags, and supplier lead-time estimations.7. Module 6: Dashboarding, Custom Widget Builder & Grafana Analysis7.1 Architecture & Evaluation: Custom Widget Builder vs. Grafana+--------------------------------------------------------------------------------------------------------------------+
  | Grafana Integration vs. Custom Native Builder |
  +--------------------------------------------------------------------------------------------------------------------+
  | Feature / Factor | Embedded Grafana (Iframe / Scenes API) | Native React Widget Canvas Engine |
  +---------------------------+-------------------------------------------------+--------------------------------------+
  | Licensing Considerations | AGPLv3 License: Self-hosting and modifying | MIT / Apache 2.0 (Internal IP): |
  | | creates strict copyleft obligations if extended.| Zero corporate exposure or copyleft |
  | | Commercial license required for clean OEM | triggers; complete freedom for |
  | | distribution without source disclosure. | proprietary industrial deployment. |
  +---------------------------+-------------------------------------------------+--------------------------------------+
  | Authentication & Context | Multi-tenant iframe session bridging; requires | Native OAuth/OIDC context; zero auth |
  | Synchronization | synchronized cookies, reverse proxy auth, or | hop, shared Zustand store, direct |
  | | Grafana Org/Role syncing overhead. | access to client-side data pipelines.|
  +---------------------------+-------------------------------------------------+--------------------------------------+
  | Real-time Performance | Standard dashboard refresh loops or Grafana Live| WebGL/Canvas direct renders at 60Hz; |
  | | (WebSocket); high DOM overhead for complex | ultra-low footprint for edge IPCs and|
  | | kiosk environments with 50+ real-time panels. | embedded kiosk clients. |
  +---------------------------+-------------------------------------------------+--------------------------------------+
  | Domain Integration | Isolated from 3D CAD canvas, plant topology, | Native cross-talk: clicking a chart |
  | | and ticket creation workflows. | data point isolates the 3D CAD mesh |
  | | | and filters the open ticket board. |
  +--------------------------------------------------------------------------------------------------------------------+
  Architectural VerdictBuild a Native React Widget Canvas Engine with Optional Grafana Read-Only Embedding.A pure Grafana approach introduces AGPLv3 copyleft compliance risks when integrated with proprietary plant software. It also creates authentication overhead and prevents bidirectional interactions between charts and the 3D CAD/BOM canvas.The system will use a Native React Grid/Canvas Builder (powered by @react-grid-layout and Apache ECharts/Canvas) as its primary engine, while providing a sandbox iframe plugin for legacy Grafana dashboards where organizations already run enterprise Grafana instances.7.2 Native Widget Builder CapabilitiesDrag-and-drop grid canvas with snapping, auto-reflow, and responsive breakpoint controls.Pre-built industrial widget library:Real-time Multi-Pen Strip Chart (ADS/OPC UA variables).Digital Twin 3D Viewport Widget.Machine State Gantt Timeline (RUNNING, STARVED, BLOCKED, FAULTED).OEE Breakdown Gauges (Availability, Performance, Quality).I/O Bus Terminal Health Matrix.Active Escalation Kanban Bucket.Direct PLC Action Pushbuttons (with dual-confirmation security modals).8. Module 7: Shopfloor Web Kiosk TerminalThe Web Kiosk interface is designed for touch-first operation on industrial panel PCs mounted directly to production cells:+--------------------------------------------------------------------------------------------------------------------+
  | [KIOSK: Cell-10 Stator Line] Station: OP-200 | Operator: Badge In (RFID) Status: [LINE STOP: SERVO FAULT] |
  +--------------------------------------------------------------------------------------------------------------------+
  | [ CURRENT MACHINE METRICS ] | [ ACTIVE ISSUES / TICKETS ] | [ BROADCAST NOTICES & SHIFT LOG ] |
  | - Cycle Time: 14.2s (Target: 12.0s)| #TCK-492: Axis Z Torque Exceeded | "Safety Audit: Shift 2 Team meeting at |
  | - Feed Pressure: 5.8 bar | [Escalated to Tier-2 Maintenance] | 14:00 at Station 1 Main Whiteboard." |
  | - Part Count (Shift): 412 / 500 | Status: Collaborating (2 Techs) | |
  | - Spindle Temp: 64°C (WARNING) | ETA: 8 mins | "Notice: SAP Product Variant C change |
  | | | scheduled for 15:30. Verify recipe." |
  +------------------------------------+-------------------------------------+-----------------------------------------+
  | [ QUICK-ACTION ACTION BAR ] |
  | [!] REPORT LINE STOP [+] NEW MAINTENANCE TICKET [*] SPARE PART LOOKUP [?] CALL SHIFT LEAD |
  +--------------------------------------------------------------------------------------------------------------------+
  Key Technical CharacteristicsLocal Hardware & Metadata Sync: Edge daemon automatically sends client telemetry (OS health, touchscreen latency, IP, MAC, TwinCAT AMS Net ID) without operator interaction.Touch-Optimized UX: Minimum 48px hit targets, virtual on-screen keyboards for text input, high-contrast themes for industrial glare conditions.Authentication: Fast operator check-in via badge scanning (RFID/NFC emulated as HID USB input), barcode scanning, or quick PIN, tied to Active Directory identity.Offline Resilience: Service Worker caches shell assets; IndexedDB buffers telemetry samples and draft tickets locally during plant network drops, automatically synchronizing upon reconnection.9. Module 8: Local & Cloud AI Analytics & Reporting EngineThe frontend integrates a dual-mode AI engine supporting both Local On-Premises LLMs (e.g., Ollama/vLLM serving Llama-3/Mistral on local plant GPUs for data privacy) and Cloud LLMs (e.g., Claude/OpenAI via enterprise endpoints):9.1 Predictive Anomaly & Alert EngineConnects to high-frequency telemetry streams.Visualizes real-time anomaly scores (e.g., sensor drift, recurring micro-stoppages).Auto-generates pre-populated incident tickets when anomaly confidence exceeds configured thresholds.9.2 Automated Plant Reporting & Root-Cause SummarizationShift Handover Reports: One-click generation of narrative shift summaries detailing produced volumes, total downtime, recurring line stops, parts swapped, and open escalations.8D Problem-Solving Assistant: When investigating recurring tickets, the AI correlates historical TwinCAT variable states, recipe changes, and operator comments to generate structured 8D incident drafts.Natural Language Machine Querying: Chat interface within the Omnibox (e.g., "Show me all torque spikes on Cell 10 Axis Z that occurred after recipe update v2.4").10. Module 9: UI Component Toolkit & Global/Contextual Search Engine10.1 Unified Industrial UI ToolkitTo ensure consistency across complex views, the frontend utilizes an integrated component library designed for high-density industrial data: [INDUSTRIAL UI TOOLKIT]
  │
  ┌───────────────────────┬──────────────┴──────────────┬────────────────────────┐
  ▼ ▼ ▼ ▼
  [Adaptive Cards] [Virtualized Grid] [Kanban Buckets] [Spatial 2D/3D]
- Metric Micro-charts - 100k+ row virtualization - Drag & drop tickets - Three.js WebGL canvas
- Status Badges - Column reorder/freeze - Multi-swimlane (Tech) - Node picking & labels
- Action Menus - In-line cell editing - SLA countdown timers - Heatmap overlays
  Adaptive Cards: Reusable metric tiles supporting real-time value tickers, sparklines, badge variants (Success, Warning, Critical, Offline), and contextual action dropdowns.Virtualized DataGrids: Powered by @tanstack/react-table and react-virtual, handling datasets with 100,000+ records (e.g., historical telemetry points, spare part records) with column freezing, multi-column sorting, and in-cell editing.Kanban Bucket Boards: Multi-column drag-and-drop boards supporting swimlanes by Technology, Machine, or Urgency, with real-time optimistic updates for ticket progression.Master Layout Shell: Flexible docking engine (using rc-dock or GoldenLayout-style layouts) enabling multi-monitor setups common in plant control rooms.10.2 Contextual vs. Global Search ArchitectureTypeScriptexport interface SearchQuery {
  scope: 'CONTEXTUAL' | 'GLOBAL';
  currentRouteContext?: {
  module: 'TELEMETRY' | 'TICKETING' | 'INVENTORY' | 'CAD_VIEWER';
  activeMachineId?: string;
  activeLineId?: string;
  };
  searchTerm: string;
  filters: {
  domain?: string[];
  technology?: string[];
  dateRange?: [string, string];
  };
  }
  +--------------------------------------------------------------------------------------------------------------------+
  | [Cmd + K] OMNIBOX SEARCH MODAL |
  | > Search: Axis Z Motor_______________________________________________ [Scope: Contextual (Cell 10) | GLOBAL (Plant)] |
  +--------------------------------------------------------------------------------------------------------------------+
  | CONTEXTUAL RESULTS (Station: Cell 10 - Stator Line) |
  | - [Telemetry Tag] MAIN.fbStation1.stAxisZ.fTorqueActual (4.2 Nm) ............................. [Focus Live Scope] |
  | - [CAD Node] Stator_Cell10_Rev3.glb -> /SubAssembly/Motors/AxisZ_Motor ....................... [Isolate 3D Node] |
  | - [Active Ticket] #TCK-492: Axis Z Torque Exceeded (Escalated) ............................... [Open Ticket View] |
  | - [BOM Part] Siemens 1FK7042-2AC71 (Stock: 2 in Crib B) ..................................... [View Inventory] |
  +--------------------------------------------------------------------------------------------------------------------+
  | GLOBAL RESULTS (Entire Plant Enterprise) |
  | - [Machine] Line 02 / Cell 14 / Axis Z Motor ................................................. [Navigate to Cell] |
  | - [Closed Ticket] #TCK-2025-881: Axis Z Motor Overheating on Line 03 ......................... [View Resolution] |
  +--------------------------------------------------------------------------------------------------------------------+
  Contextual Scoped Search: Activated by default when searching within a specific view (e.g., filtering metrics within the active CAD model, machine, or ticket view). Uses in-memory worker indexes for instant UI filtering.Global Omnibox (Cmd+K / Ctrl+K): Cross-indexes all enterprise assets: machines, users, AD groups, TwinCAT symbols, spare parts, active tickets, and historical 8D reports. Powered by a background search service (e.g., Meilisearch/Elasticsearch) with sub-50ms response times.11. Security, Role-Based Access Control & Edge Resilience+-------------------------------------------------------------------------------------------------------------------+
  | Role Name | Telemetry Access | Ticket Permissions | Part Inventory | Machine Controls |
  +-------------------------+---------------------+----------------------+---------------------+----------------------+
  | Operator (Kiosk) | View Assigned Cell | Create / Add Notes | Read-Only Query | Restricted Actions |
  | Maintenance Tech | View All Associated | Assign / Resolve | Check-out / Swap | Calibration Commands |
  | Automation Engineer | Full Edit / Schema | Escalation Target | Map BOM to Symbols | Full Write / Deploy |
  | Plant Shift Manager | Full Read (Plant) | Re-route Escalations | Approve Stock Orders| Acknowledge Alerts |
  +-------------------------------------------------------------------------------------------------------------------+
  Authentication: OpenID Connect (OIDC) / OAuth2 with PKCE, integrating seamlessly with corporate Active Directory / Entra ID identity providers.Edge Communication Security: Mutual TLS (mTLS) for all WebSocket and MQTT edge connections to prevent rogue message injection on the OT network.Safety Interlocks: High-risk write-back operations (e.g., toggling PLC bits or deploying recipe changes) require multi-factor re-authentication and dual-operator authorization (four-eyes principle) before transmitting commands over TwinCAT ADS.
