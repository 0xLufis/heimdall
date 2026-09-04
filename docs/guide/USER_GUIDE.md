# Heimdall User & Operator Guide

This guide describes the user interface, operational workflows, and features available in the Heimdall Web Dashboard.

---

## 1. Access & Navigation

### 1.1 Authentication & Session
1. Navigate to the web application URL (e.g., `http://localhost:3000` or production URL).
2. Enter your credentials or authenticate via configured identity providers (Active Directory, Entra ID, or local account).
3. Upon login, the active organization is loaded based on your assigned permissions (`admin`, `engineer`, `technician`, `operator`).
4. To switch organizations (if you belong to multiple plants or production floors), click your organization name in the top navigation bar and choose from the dropdown menu.

### 1.2 Layout & Theme
* The left sidebar provides direct navigation to all functional areas:
  * **Dashboard**: Executive KPIs and activity feed.
  * **Fleet (Clients)**: Edge controller status and live gauges.
  * **Plant Map**: Interactive CAD floor plan with machine status pins.
  * **Inventory**: Equipment repository, hierarchy trees, and specifications.
  * **Tickets**: Real-time maintenance Kanban board.
  * **Settings / Governance**: Users, roles, and security group mappings.
* To change theme preferences (Light / Dark / System), open the user profile menu at the bottom-left of the sidebar.

---

## 2. Interactive Plant Floor Map (`/dashboard/map`)

The Plant Map enables operators to visually locate production equipment and inspect machine health directly on architectural factory drawings.

### 2.1 Navigation & Controls
* **Pan**: Click and drag on any empty space within the canvas.
* **Zoom**: Use the mouse scroll wheel or on-screen zoom buttons (`+` / `-`).
* **Reset View**: Click the center button to frame the entire production hall.
* **Floor Plan Selector**: Use the dropdown at the top right to switch between different drawing layers (e.g., `Production Hall`, `Line 01 Alpha`, `Line 02 Welding`).

### 2.2 Inspecting a Production Station
1. Stations appear as vector machine blocks with status indicator rings:
   * **Green**: Controller online, no active alerts.
   * **Amber / Yellow**: Warning status (e.g., high memory load, low free disk).
   * **Red**: Critical alarm or open high-priority maintenance ticket.
   * **Gray**: Edge controller offline.
2. Click any station block to open its summary card:
   * Displays the station code (e.g., `LINE-01-OP10`).
   * Lists controlling edge PCs and their current IP addresses.
   * Shows active maintenance tickets and cycle time targets.
   * Provides quick links to open the full asset inspector or file a ticket.

### 2.3 Pinning New Stations to Drawings
1. In editing mode, click an unmapped machine block on the CAD layout.
2. Enter the official **Station Code** (e.g., `LINE-04-OP40`).
3. Select the managing **Edge Controller** from the list of registered PCs.
4. Click **Save Pin Assignment**. The link is persisted and synchronized immediately.

---

## 3. Fleet Controller Monitoring (`/dashboard/clients`)

The Fleet view monitors all industrial PCs (IPCs), Soft-PLCs, and edge compute nodes.

### 3.1 Live Grid & Metrics
* **Heartbeat & Status**: The status indicator pulses green when a node has reported within its expected heartbeat window.
* **Resource Gauges**: Live CPU utilization, RAM usage percentage, and primary OS drive free space.
* **Network & Driver Details**: Displays reported MAC addresses, active IP, and whether the Beckhoff TwinCAT real-time network driver (`TcRTEthernet`) is bound.

### 3.2 Dispatching Remote Commands
1. Click the action menu (`...`) on any controller card and select **Dispatch Command**.
2. Select the command type:
   * `UPDATE_CONFIG`: Update sampling frequencies or adapter enable flags.
   * `FILE_CHECK`: Request a file integrity verification of PLC boot projects.
   * `APPLY_RECIPE`: Push an updated telemetry collection recipe.
3. The command is cryptographically signed and queued for execution on the target daemon.

---

## 4. Maintenance Ticketing & Kanban Board (`/dashboard/tickets`)

The maintenance module coordinates repairs, parts replacements, safety escalations, and calibration sign-offs across the production floor.

### 4.1 8-Stage Kanban Status Columns
* **`Open`**: Newly reported incidents awaiting technician assignment.
* **`In Progress`**: Active repair or diagnostics by a designated technician.
* **`Pending Parts`**: Work paused awaiting replacement components from warehouse stock.
* **`Escalated`**: Safety-critical lockouts (e.g., dual-channel safety relay desynchronization, light curtain muting timeout).
* **`Escalated External`**: Escalated to external tier-3 vendors or enterprise teams (e.g., SAP MES RFC dropout).
* **`Closure Pending`**: Maintenance complete, awaiting formal calibration verification or outside AOK sign-off.
* **`Resolved`**: Verified operational, fully documented, and cleared for production.
* **`Closed Unresolved`**: Ticket retired without corrective action (e.g., duplicate, obsolete equipment).

### 4.2 Error Template Catalog
Operators can select from predefined error templates covering:
- **Motion & Drive**: Axis position divergence, servo torque limit exceeded.
- **Safety Systems**: Light curtain muting desync, E-stop dual channel violation.
- **Fieldbus & Networks**: SAP MES RFC dropout, PROFINET bus fault.
- **Vision & Optics**: AOI blob rejection spike, telecentric lens strobe lag.
- **Dispensing & Joining**: Gap filler nozzle pressure sag, screwdriver torque-angle window violation, NC servo press envelope error.

Selecting a template automatically populates the title, technical error code, default tags, sample function block state, and telemetry keys.

### 4.3 Composable Action QR Codes & Camera Scanner
- **QR Label Generation**: Click **Generate Machine QR** to create composable action QR codes (`report-incident`, `inspect-machine`, `claim-ticket`) for specific machines, lines, or tickets. Rendered as crisp, pure SVG graphics ready for printing.
- **Mobile Camera Scanner**: Tap **Scan QR** in the navigation bar to scan physical equipment labels and instantly open the relevant machine's maintenance timeline.

### 4.4 Technician Delegation & Shift Attendance
- **Shift Attendance**: Shift leaders can mark technicians absent (`Sick`, `Emergency`, `Vacation`, `Training`) and designate backup personnel.
- **Technician Dedication**: Group leaders and engineering managers dedicate engineers to specific technologies (`Milling`, `Pressing`, `AOI`), lines, or individual machines.
- **Attendance Inheritance**: Tickets auto-assign to preferred technicians, seamlessly routing to backups when primary personnel are marked absent or out of office.

---

## 5. Inventory & Asset Management (`/dashboard/inventory`)

### 5.1 Repository Tabs
* **Hardware Tab**: Physical assets (drives, motors, sensors, IPCs, valves).
* **Software Tab**: Operating systems, TwinCAT runtime licenses, PLC project versions.
* **Hierarchy Tree View**: Explores recursive assemblies (e.g., Line $\to$ Station $\to$ Controller $\to$ Drive $\to$ Encoder).

### 5.2 Dynamic Asset Editor (5 Tabs)
When adding or editing an asset, use the 5-tab editor:
* **Identity**: Set asset name, model, serial number, and select the manufacturer.
* **Topology**: Assign the station, managing PC, and parent assembly.
* **Commercial**: Record the procurement cost in Hungarian Forint (HUF) and vendor information.
* **Specs**: Add dynamic technical attributes (voltage ratings, payload limits, cycle times).
* **Templates**: Select from pre-configured equipment templates to auto-fill common specifications.

---

## 6. System Governance & Active Directory (`/dashboard/admin/system-settings`)

### 6.1 Multi-Factor Authentication (MFA) Policies
Configure re-authentication timeout thresholds per security role:
- `SystemAdministrator`: Re-authenticate always (every session).
- `Engineer`: Re-authenticate once a week (7 days).
- `Technician`: Re-authenticate once a month (30 days).
- Custom rules: Define enforcement thresholds for specialized security groups.

### 6.2 Active Directory Host Discovery & VLAN Separation
- Discover unmanaged factory edge hosts partitioned across industrial VLANs (VLAN 10 Robotics, VLAN 20 Vision, VLAN 30 Milling, VLAN 40 Dispensing, VLAN 50 Fastening, VLAN 60 Pressing).
- Use **Mass Import Templating** to translate AD OU attributes (`Location`, `Subnet`, `MachineType`, `Purpose`) into structured Heimdall machine metadata.

### 6.3 PKI Root CA & OU Certificate Rules
- Import existing enterprise Root CA certificates or generate internal self-signed certificates.
- Define automatic X.509 certificate enrollment rules based on host Active Directory OU membership.

### 6.4 Security Group Organization Mapping (`/dashboard/security-groups`)
- Map incoming directory claims (Entra ID GUIDs or on-prem AD Distinguished Names) to Heimdall tenant organizations.
- Test claims mapping in the interactive evaluation sandbox with presets for engineering personas (Sally Vance, George Orwell, Alex Novak, Root Admin).
