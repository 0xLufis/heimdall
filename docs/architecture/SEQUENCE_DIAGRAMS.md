# Master Sequence Diagrams & Protocol Interaction Gallery

This document serves as the canonical visual reference gallery for the Heimdall manufacturing intelligence and telemetry platform.
It documents all core communication protocols, end-to-end data flows, real-time event broadcasting, and edge device lifecycle operations.

---

## 1. Edge Telemetry Ingestion & Real-Time Broadcast Flow

Agents (and the fleet simulator) publish protobuf messages over MQTT 5 to the MQTTnet broker embedded in the backend, authenticated by an agent key. The ingestion hosted service hands them to the telemetry ingestion service: unchanged PLC memory blocks are dropped by the change gate (arrival is still recorded for station freshness), changed blocks become station snapshots in the cache and SignalR events to organisation-scoped groups. Agent lifecycle events, including the MQTT last will, are stored as agent events.

```mermaid
sequenceDiagram
    autonumber
    actor EdgeNode as Edge Agent / Fleet Simulator
    participant Broker as Embedded MQTT Broker (MQTTnet, port 1883)
    participant Ingest as MqttIngestionHostedService
    participant Service as TelemetryIngestionService
    participant Gate as PlcMemoryChangeGate + StationFreshness
    participant Cache as Station snapshot cache (L1 Memory / L2 Redis)
    participant Hub as MaintenanceHub (/hubs/maintenance)
    actor Browser as Nuxt Web Dashboard (org group)

    Note over EdgeNode,Browser: Flow 1: Edge Telemetry Ingestion & Real-Time Broadcast Flow

    EdgeNode->>+Broker: CONNECT (heimdall_agent / agent key, MQTT 5, last will on events topic)
    EdgeNode->>Broker: PUBLISH heimdall/devices/{id}/plc_memory (protobuf AdsPlcMemoryBlock, user property x-agent-key)
    Broker->>+Ingest: Deliver message (wildcard subscription heimdall/devices/+/plc_memory, QoS 1)
    Ingest->>+Service: ProcessPlcMemoryAsync(machineId, block, agentKey)
    Service->>Service: ValidateCallerAuthentication(agentKey)
    Service->>Gate: Record arrival (freshness) and compare block with last one
    alt Block unchanged and keep-alive (10 s) not due
        Gate-->>Service: Drop block
    else Changed or keep-alive due
        Gate-->>Service: Pass block
        Service->>Hub: PlcMemoryReceived (only when the block descriptor changed)
        Service->>Service: StationTelemetryExtractor builds station snapshot
        Service->>Cache: Store station snapshot
        Service->>+Hub: StationTelemetryReceived / StationOscillationReceived to org:{id} and platform-admins
        Hub-->>-Browser: WebSocket event
        Service->>Service: Run Lua TelemetryChange scripts
    end
    Service-->>-Ingest: Done
    deactivate Ingest

    opt system_info message with queued commands
        EdgeNode->>Broker: PUBLISH heimdall/devices/{id}/system_info
        Broker->>Ingest: Deliver message
        Ingest->>Service: ProcessSystemInfoAsync(request, agentKey)
        Service-->>Ingest: SystemInfoResponse(commands)
        Ingest->>Broker: PUBLISH heimdall/devices/{id}/commands (QoS 1)
        Broker->>EdgeNode: Deliver commands
    end

    opt Agent connection lost
        Broker->>Ingest: Last will AgentEventMessage (source AgentLifecycle, connection_lost)
        Ingest->>Service: ProcessAgentEventAsync
        Service->>Service: AgentLifecycleIngestor stores agent_events row and client_pcs.system_metadata.AgentLifecycle
        Service->>Hub: InventoryUpdated (MQTT_AgentLifecycle) to org:{id} and platform-admins
        Note over Service,Browser: Stations whose controllers' agents all sent their last will read as OFFLINE (StationFreshnessPolicy, on the next REST load)
    end
    deactivate Broker
```

---

## 2. Maintenance Ticket Lifecycle & State Transition Flow

Incident reports and automated alarms create maintenance tickets that move through five Kanban statuses (Open, In Progress, Pending, Resolved, Closed); escalation is a separate flag. The BFF applies a change to its ticket store and syncs it to the backend, which updates the database, evicts the multi-tier cache entries and broadcasts SignalR events to the owning organization's group.

```mermaid
sequenceDiagram
    autonumber
    actor Tech as Technician / Shift Leader (Web UI)
    participant NitroBFF as Nuxt Nitro BFF (/api/tickets/{id})
    participant Controller as MaintenanceTicketController (ASP.NET Core REST)
    participant Repo as MaintenanceTicketRepository (EF Core)
    participant Cache as CacheService (Multi-Tier Invalidation)
    participant Hub as MaintenanceHub (SignalR)
    actor Operators as Dashboards in the org group (+ platform-admins)

    Note over Tech,Operators: Flow 2: Maintenance Ticket Lifecycle & State Transition Flow

    Tech->>+NitroBFF: PATCH /api/tickets/{id} { status: "InProgress" }
    NitroBFF->>NitroBFF: ticketsStore: apply the change and record edit history
    NitroBFF-->>-Tech: 200 OK { success, ticket }
    NitroBFF-)+Controller: Background sync: PATCH /api/v1/tickets/{id}/status ("InProgress")

    Controller->>Repo: UpdateStatusAsync(id, "InProgress")
    Repo->>Repo: Set status, stamp StartedAt and reaction time
    Repo-->>Controller: MaintenanceTicket (Updated entity)

    Controller->>Cache: RemoveAsync("tickets:all:{all, open, inprogress, pending, resolved, closed}")
    Controller->>Cache: RemoveAsync("tickets:item:{id}") and RemoveAsync("dashboard:metrics")
    Controller->>Cache: SetAsync("tickets:item:{id}", updated, 5 min)

    Controller->>+Hub: org group + platform-admins: StatusChanged(id, "InProgress")
    Controller->>Hub: org group + platform-admins: TicketUpdated(updated)
    Hub-->>-Operators: WebSocket Broadcast: StatusChanged & TicketUpdated
    Operators->>Operators: Kanban card moves Open -> In Progress

    Controller-->>-NitroBFF: 204 NoContent
```

---

## 3. Active Directory OU Discovery & Batch Import Flow

The IT administration module lists the plant's Active Directory organizational units and production VLANs with their candidate industrial PCs. In this build the directory is simulated from the enterprise plant dataset and only available with dev features. Hosts of OUs approved for read/write are imported into the central inventory in one batch.

```mermaid
sequenceDiagram
    autonumber
    actor Admin as IT Administrator
    participant WebUI as Nuxt 4 Admin Console (System Settings)
    participant NitroBFF as Nuxt Nitro BFF (/api/proxy/v1/activedirectory/*)
    participant ADController as ActiveDirectoryController (REST, ItAdministration policy)
    participant Directory as Simulated plant directory (fixtures/enterprise_plant_dataset.json)
    participant DB as AppDbContext (PostgreSQL)

    Note over Admin,DB: Flow 3: Active Directory OU Discovery & Batch Import Flow (simulated directory, dev features only)

    Admin->>+WebUI: Open the Active Directory section
    WebUI->>+NitroBFF: GET /api/proxy/v1/activedirectory/ous
    NitroBFF->>+ADController: GET /api/v1/ActiveDirectory/ous (session bearer + X-Organization-Id)
    ADController->>DB: Query existing ClientPc hostnames & AdOuGovernances
    DB-->>ADController: Imported hostnames & OU approvals (read_write / read_only / unapproved)

    ADController->>Directory: Load plant OUs and candidate hosts
    Directory-->>ADController: 14 OUs (8 production lines, VLAN10 to VLAN60) with candidate IPC/PC hosts
    ADController->>ADController: Mark already-imported hosts & attach OU governance
    ADController-->>-NitroBFF: List<AdOrganizationalUnit> with hostCount & candidateHosts
    NitroBFF-->>-WebUI: OU list
    WebUI-->>-Admin: OU table with approval badges
    Note over WebUI,NitroBFF: Without the backend the BFF answers /api/activedirectory/* from its own dataset store

    Admin->>+WebUI: Select OUs, naming pattern & tag rules, click "Preview"
    WebUI->>+NitroBFF: POST /api/proxy/v1/activedirectory/preview-import
    NitroBFF->>+ADController: POST /api/v1/ActiveDirectory/preview-import
    ADController-->>-NitroBFF: Preview { totalFound, preview: hosts with resolved names and OU tags }
    NitroBFF-->>-WebUI: Preview rows

    Admin->>WebUI: Click "Import"
    WebUI->>+NitroBFF: POST /api/proxy/v1/activedirectory/import-hosts { hosts: [...] }
    NitroBFF->>+ADController: POST /api/v1/ActiveDirectory/import-hosts
    ADController->>DB: Check AdOuGovernance of every target OU (403 unless approved read_write)
    ADController->>DB: Insert or update ClientPc rows (VLAN, OU path, OU tags, certificate from an auto-enroll OU rule)
    DB-->>ADController: Saved (Entities committed)
    ADController-->>-NitroBFF: { message, importedCount, updatedCount, totalProcessed }
    NitroBFF-->>-WebUI: Import result
    WebUI-->>-Admin: Toast & refreshed fleet table
```

---

## 4. PKI Trust Store Import & Client Certificate Auto-Enrollment Flow

The Heimdall PKI issues X.509 computer certificates. This flow illustrates importing an enterprise CA into the trust store, declarative OU enrollment rules, certificate issuance by the Heimdall CA, and a kiosk terminal authenticating with its computer certificate, which a TLS-terminating reverse proxy verifies and forwards to the BFF in the X-Client-Cert header. Agent telemetry itself is authenticated by an agent key over MQTT, not by client certificates.

```mermaid
sequenceDiagram
    autonumber
    actor SecAdmin as Security Administrator
    participant Settings as System Settings PKI panel (Nuxt, via /api/proxy/v1/certificatemanagement)
    participant CertController as CertificateManagementController (ItAdministration policy)
    participant Pki as PkiService (X.509, BCL X509Chain)
    participant DB as PostgreSQL (trusted_ca_certificates, ou_certificate_rules, client_certificates)
    actor Kiosk as Kiosk Terminal (computer certificate)
    participant Proxy as TLS-terminating Reverse Proxy
    participant BFF as Nuxt BFF (/api/terminal/*)
    participant Workstation as PkiWorkstationController

    Note over SecAdmin,Workstation: Flow 4: PKI Trust Store Import & Client Certificate Auto-Enrollment Flow

    SecAdmin->>+Settings: Import an AD CS / enterprise root CA (RootCertImportModal)
    Settings->>+CertController: POST /api/v1/CertificateManagement/root-ca/import { rawPem }
    CertController->>Pki: ImportTrustedCasAsync(pem, source "imported" or "ad")
    Pki->>Pki: Parse PEM bundle, require basicConstraints cA=TRUE and a valid period
    Pki->>DB: Insert trusted_ca_certificates rows (Status "Active")
    CertController-->>-Settings: Trusted CA record (thumbprint, subject, expiry)

    SecAdmin->>Settings: Create OU auto-enrollment rule (e.g. OU=Robotics,OU=VLAN10-Production)
    Settings->>+CertController: POST /api/v1/CertificateManagement/ou-rules
    CertController->>DB: Insert OuCertificateRule (ValidityYears: 2, KeyAlgorithm: "RSA-2048", AutoEnroll)
    CertController-->>-Settings: Rule saved

    SecAdmin->>Settings: Trigger "Sync OU Certificates"
    Settings->>+CertController: POST /api/v1/CertificateManagement/sync-ou-certificates
    CertController->>DB: Client PCs whose AD OU matches an auto-enroll rule
    DB-->>CertController: PCs without an active certificate for the rule's profile

    loop For each unenrolled client PC
        CertController->>Pki: IssueAsync(kind "computer", hostname, machine identifier)
        Pki->>Pki: Sign with the active Heimdall CA (self-signed RSA-4096 root made on first use, or subordinate), new RSA-2048 key
        Pki->>Pki: SAN dNSName + URI urn:heimdall:machine:<id>, EKU clientAuth + serverAuth
        Pki->>DB: Store client_certificates row (thumbprint, serial, expiry)
        CertController->>DB: ClientPc.CertificateThumbprint / CertificateProfileName
    end
    CertController-->>-Settings: { message, syncedCount, activeRulesCount }
    Settings-->>-SecAdmin: Sync complete

    Kiosk->>+Proxy: TLS handshake presenting the issued computer certificate
    Proxy->>Proxy: Verify certificate against the Heimdall trust bundle (GET trust-bundle)
    Proxy->>+BFF: GET /api/terminal/machine/{id} + X-Client-Cert
    BFF->>BFF: HEIMDALL_TRUST_CLIENT_CERT_HEADER=true and peer in HEIMDALL_TRUSTED_PROXIES?
    BFF->>+Workstation: POST /api/v1/pki/workstation-identity (certificate PEM)
    Workstation->>Pki: ValidateAsync + map to ClientPc (machine URI, DNS name, thumbprint)
    Workstation-->>-BFF: Valid, ClientPc, stations bound in StationControllers
    BFF-->>-Proxy: 200 for bound stations, 403 otherwise (401 if invalid)
    Proxy-->>-Kiosk: Terminal page / API response
```

---

## 5. Station Component Graph Traversal Flow

Manufacturing stations model complex multi-controller assemblies using Table-per-Type (TPT) inheritance and many-to-many junction entities. Opening a station's component tree (from the plant map's context menu or the machines page) traverses the component graph to render connected IPCs, PLCs, component slots, and installed parts.

```mermaid
sequenceDiagram
    autonumber
    actor Engineer as Maintenance Engineer / Operator
    participant CadMap as Plant Map (station pin, right-click menu)
    participant Workbench as Station workbench (/dashboard/machines?tree={id})
    participant NitroBFF as Nuxt Nitro BFF (/api/proxy/v1/*)
    participant InvCtrl as InventoryController (REST API)
    participant TptRepo as AssetRepository (EF Core TPT)
    participant DB as PostgreSQL (stations, StationControllers, client_pcs, machine_components, inventory_items)

    Note over Engineer,DB: Flow 5: Station Component Graph Traversal Flow

    Engineer->>+CadMap: Right-click a station pin (e.g. L01-OP030), "Inspect Component Tree"
    CadMap->>+Workbench: Navigate to /dashboard/machines?search=L01-OP030&tree={id}
    deactivate CadMap
    Workbench->>+NitroBFF: GET /api/proxy/v1/inventory/station-tree/{id}
    NitroBFF->>+InvCtrl: GET /api/v1/inventory/station-tree/{id} (session bearer + X-Organization-Id)
    InvCtrl->>+TptRepo: GetStationComponentTreeAsync(id)

    TptRepo->>DB: stations (TPT join inventory_items) by id, custom identifier or name, with responsible teams
    TptRepo->>DB: StationControllers JOIN client_pcs (with the controllers' own inventory items)
    TptRepo->>DB: machine_components WHERE machine_id = @id (slots and their current part)
    TptRepo->>DB: inventory_items WHERE machine_id = @id (installed parts, manufacturer)
    DB-->>TptRepo: Station row, 1..N IPC/PLC controllers, component slots, installed parts
    TptRepo-->>-InvCtrl: StationComponentTreeDto (404 for an unknown station)

    InvCtrl-->>-NitroBFF: 200 StationComponentTreeDto
    NitroBFF-->>-Workbench: JSON payload with the station graph

    Workbench->>Workbench: Render Station -> Controllers -> component slots / installed parts
    Workbench-->>-Engineer: Tree with lifecycle badges (InMachine, InStorage, UnderRepair)
```

---

## 6. Edge Agent Command Dispatch & Execution Loop

Engineers issue remote configuration updates and diagnostic recipes through authenticated REST APIs. Commands are queued in PostgreSQL, delivered to edge agents over MQTT on the commands topic after their next system_info report, verified cryptographically, and executed locally.

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Site Automation Engineer
    participant WebUI as Nuxt Web Admin Panel (via /api/proxy/AgentCommand)
    participant CmdCtrl as AgentCommandController (REST)
    participant DB as PostgreSQL (queued_agent_commands)
    participant Ingest as MqttIngestionHostedService / TelemetryIngestionService
    participant Broker as Embedded MQTT Broker
    actor Agent as Edge Agent Daemon (Worker + MqttAgentClient)
    participant Handler as CommandHandler

    Note over Admin,Handler: Flow 6: Edge Agent Command Dispatch & Execution Loop

    Admin->>+WebUI: Submit a signed configuration update (or a file check)
    WebUI->>+CmdCtrl: POST /api/v1/AgentCommand/{clientPcId}/update-config { config, signature }
    CmdCtrl->>CmdCtrl: [Authorize(Policy = "EndpointConfigManagement")]
    CmdCtrl->>DB: INSERT INTO queued_agent_commands (ClientPcId, Type: "UPDATE_CONFIG", Payload, Signature, IsProcessed = false)
    DB-->>CmdCtrl: Queued
    CmdCtrl-->>-WebUI: 200 OK { Message: "Command queued successfully" }
    WebUI-->>-Admin: Display success notification
    Note over CmdCtrl,DB: The signature arrives with the request. Plugin installs are signed by the backend (PluginService, HEIMDALL_MASTER_PRIVATE_KEY)

    loop Every system_info report (triggers evaluated every 10 s +/- 1 s, heartbeat 60 s)
        Agent->>+Broker: PUBLISH heimdall/devices/{id}/system_info (SystemInfoRequest, QoS 1)
        Broker->>+Ingest: Deliver message -> ProcessSystemInfoAsync
        Ingest->>DB: SELECT queued_agent_commands WHERE ClientPcId = @id AND NOT IsProcessed
        DB-->>Ingest: Pending ServerCommand ("UPDATE_CONFIG"), then marked IsProcessed
        Ingest-->>-Broker: PUBLISH heimdall/devices/{id}/commands (SystemInfoResponse, QoS 1)
        Broker-->>-Agent: Deliver commands (commands topic subscription)
    end

    Agent->>+Handler: HandleCommandAsync(command)
    Handler->>Handler: Verify RSA-SHA256 signature (PKCS1 padding) against ServerPublicKey

    alt Signature valid
        Handler->>Handler: Apply the signed configuration (or run the whitelisted command / plugin action)
        Handler-->>Agent: CommandExecutionResult(Success)
    else Signature invalid or missing
        Handler->>Handler: Reject the command (ErrorCode.SignatureVerificationFailed)
        Handler-->>-Agent: CommandExecutionResult(Failure), logged as a warning
    end
```

---

*Generated automatically by `tools/generate_sequence_diagrams.py` based on source code analysis.*
