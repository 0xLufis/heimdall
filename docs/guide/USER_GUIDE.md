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
  * **Machines & Lines**: Interactive cards of machines, production lines, and technologies.
  * **Plant Map**: Interactive CAD floor plan with machine status pins.
  * **Inventory**: Differentiated serialized parts and bulk stock items with on-demand tree visualization.
  * **Tickets**: Real-time maintenance Kanban board.
  * **Settings / Governance**: Users, system settings, security group mappings, and account profile.
* To change theme preferences (Light / Dark / System), open the user profile card at the bottom-left of the sidebar.
* Clicking **Account Settings** in the user card navigates to `/dashboard/settings` to manage profile attributes, MFA policies, and presence.

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

### 4.3 Composable Action QR Codes & Camera Scanner
- **QR Label Generation**: Click **Generate Machine QR** to create composable action QR codes (`report-incident`, `inspect-machine`, `claim-ticket`) for specific machines, lines, or tickets. Rendered as crisp, pure SVG graphics ready for printing.
- **Mobile Camera Scanner**: Tap **Scan QR** in the navigation bar to scan physical equipment labels and instantly open the relevant machine's maintenance timeline.

### 4.4 Technician Delegation & Shift Attendance
- **Shift Attendance**: Shift leaders can mark technicians absent (`Sick`, `Emergency`, `Vacation`, `Training`) and designate backup personnel.
- **Technician Dedication**: Group leaders and engineering managers dedicate engineers to specific technologies (`Milling`, `Pressing`, `AOI`), lines, or individual machines.
- **Attendance Inheritance**: Tickets auto-assign to preferred technicians, seamlessly routing to backups when primary personnel are marked absent or out of office.

---

## 5. Inventory & Asset Management (`/dashboard/inventory`)

Heimdall replaces monolithic hierarchy tables with a high-performance, segmented inventory repository optimized for plant engineering workflows:

### 5.1 Repository Tabs: Parts vs. Stock
* **Serialized Parts Tab (`isStockItem = false`)**:
  * Displays high-value discrete capital assets (PLCs, cameras, servo drives, robotic manipulators).
  * Equipment Status tracking:
    * `InMachine`: Actively installed on a production station (with clickable station badge).
    * `InStorage`: Spare units maintained in warehouse storage bins or racks.
    * `UnderRepair`: Assets undergoing refurbishment in the central repair depot.
  * Technology tags (`Assembly`, `Test`, `SMT`, `Welding`, `Fastening`, `Dispensing`, `Robotics`).
* **Bulk Stock Tab (`isStockItem = true`)**:
  * Displays quantity-tracked consumables (bolts, fittings, dispensing nozzles, solder paste, fuses).
  * Current stock level gauges vs. configured minimum reorder thresholds.
  * Low-stock warning badges (`Low Stock`, `Healthy Stock`).
  * Warehouse storage bin addresses (e.g., `Bin 18-A`, `Bin 42-B`).

### 5.2 On-Demand Station Component Tree Visualization
Rather than cluttering the UI with an inflexible static hierarchy tree page, operators can click **Visualise Tree** next to any station or installed component to launch the modal tree inspector:
* Displays the complete hierarchical tree of hardware components, client PCs, and software licenses for that process node.
* Provides interactive node expansion, serial number badges, and component health indicators.

---

## 6. Production Machines & Lines (`/dashboard/machines`)

The dedicated Machines module provides multi-dimensional visualization of the factory floor:

### 6.1 Three Distinct Operational Views
1. **Cards of Machines**: Grid view of individual production stations with real-time controller status, cycle time metrics, assigned technicians, and quick actions.
2. **Cards of Lines**: High-level line groupings (e.g., Line 01 Body Assembly Alpha, Line 06 Battery Module Line) with collapsible station tables showing sequence order, operations (OP10, OP20), and controllers.
3. **Technologies View**: Grouped by industrial discipline:
   * **Assembly**: Mechanical assembly, fitting, and transfer stations.
   * **Test**: End-of-line testing cells, electrical safety, and functional testers.
   * **SMT**: Surface-mount placement, reflow ovens, and solder paste printers.
   * **Welding**: Robotic welding cells, laser joining, and ultrasonic welders.
   * **Fastening**: Screwing stations and torque-angle controlled press cells.
   * **Dispensing**: 2K thermal paste dispensers and gasket gluing stations.
   * **Robotics**: 6-axis articulated robots, AGVs, and high-bay palletizers.

---

## 7. OmniSearch 2.0 (`nvim-coc` Style Auto-Tag Engine)

The top navigation search bar is powered by an intelligent tokenizer modeled after developer IDE autocompletion:
* **Tag Directives**:
  * `@` for Technology (`@Assembly`, `@Test`, `@SMT`, `@Welding`, `@Fastening`, `@Dispensing`, `@Robotics`)
  * `#` for Equipment Status (`#inmachine`, `#instorage`, `#underrepair`, `#online`, `#offline`)
  * `!` for Urgency/Priority (`!critical`, `!high`, `!medium`)
* **Autocomplete Dropdown**: Typing `@`, `#`, or `!` summons a floating suggestion dropdown with description hints.
* **Keyboard Navigation**: Use `Up`/`Down` arrow keys, `Tab`, or `Enter` to auto-insert tags without leaving the keyboard.
* **Fuzzy Ranking**: Sub-millisecond instant matching across stations, serial numbers, models, and parts.

---

## 8. Governance, System Roles & User Settings

### 8.1 System Administration Roles
* **`SystemAdmin`**: Complete platform god user; bypasses all permission constraints and can configure pseudo-IT admin role inheritance.
* **`HeimdallAdmin`**: OT platform administrator managing platform settings, telemetry pipelines, and security mappings.
* **`ITAdmin`**: IT infrastructure administrator authoritative for Active Directory / Entra ID OU approvals and PKI certificate authorities.
* **`EngineeringAdmin`**: Engineering technical administrator managing users, functional engineering configurations, recipes, and machine profiles from the application UI (strictly segregated from production management).

### 8.2 Plant Engineering Roles
* **`PlantDirector`**: Executive plant read access and final sign-off authority for line stops and capital allocation.
* **`PlantEngineeringManager` & `SeniorEngineeringManager`**: Engineering department leadership.
* **`GroupLeader`**: Technology or line group supervisor managing technician dedication and schedules.
* **`Engineer` & `Technician`**: Automation, controls, and maintenance professionals executing work orders and repairs.
* **`OperativePlanner`**: Line planning managers responsible for requesting and authorizing scheduled line stops.

### 8.3 User Profile Card & Account Settings (`/dashboard/settings`)
* View assigned roles, job title, department, and active organization in the persistent sidebar user card.
* Navigate to `/dashboard/settings` to update MFA verification preferences, presence indicators, and personal interface settings.
* Manage Active Directory OU approvals (`/dashboard/security-groups`) to safely onboard edge controllers.
