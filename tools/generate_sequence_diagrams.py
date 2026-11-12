#!/usr/bin/env python3
"""
Heimdall Code-to-Sequence-Diagram Generator
Analyzes C# controllers, the MQTT ingestion pipeline, SignalR hubs, edge daemon workers,
and Nuxt Nitro BFF endpoints to generate GitHub-compatible Mermaid sequence diagrams.

Usage:
  python3 tools/generate_sequence_diagrams.py [--output <path>] [--check] [--update-docs]
"""

import sys
import os
import re
import argparse
from pathlib import Path
from typing import Dict, List, Tuple

REPO_ROOT = Path(__file__).resolve().parent.parent

class CodebaseAnalyzer:
    """Extracts routes, MQTT topics, SignalR events, and database actions from source code."""

    def __init__(self, root: Path):
        self.root = root

    def get_csharp_controller_methods(self, filepath: Path) -> List[Dict[str, str]]:
        """Parses HTTP attributes and handler names from C# controller files."""
        if not filepath.exists():
            return []
        content = filepath.read_text(encoding="utf-8")
        methods = []
        pattern = re.compile(
            r'\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]\s*(?:\[[^\]]+\]\s*)*public\s+async\s+Task<[^>]+>\s+(\w+)\s*\(([^)]*)\)',
            re.MULTILINE
        )
        for match in pattern.finditer(content):
            http_verb = match.group(1)
            route = match.group(2) or ""
            method_name = match.group(3)
            params = match.group(4)
            methods.append({
                "verb": http_verb,
                "route": route,
                "name": method_name,
                "params": params
            })
        return methods

    def get_mqtt_topics(self) -> Dict[str, str]:
        """Parses channel constants (e.g. SystemInfoChannel = "system_info") from MqttTopics.cs."""
        topics_file = self.root / "src" / "shared" / "App.Contracts" / "Mqtt" / "MqttTopics.cs"
        if not topics_file.exists():
            return {}
        content = topics_file.read_text(encoding="utf-8")
        pattern = re.compile(r'public const string (\w+Channel)\s*=\s*"([^"]+)";')
        return {m.group(1): m.group(2) for m in pattern.finditer(content)}

    def get_mqtt_ingestion_handlers(self) -> List[str]:
        """Parses which ingestion service method handles which channel in MqttIngestionHostedService.cs."""
        svc = self.root / "src" / "backend" / "App.Backend.Api" / "Services" / "Mqtt" / "MqttIngestionHostedService.cs"
        if not svc.exists():
            return []
        content = svc.read_text(encoding="utf-8")
        pattern = re.compile(r'case MqttTopics\.(\w+):.*?ingestionService\.(\w+)\(', re.DOTALL)
        return [f"{m.group(1)} -> {m.group(2)}" for m in pattern.finditer(content)]

    def get_signalr_client_events(self) -> List[str]:
        """Parses SignalR client broadcast signatures from MaintenanceHub.cs."""
        hub_file = self.root / "src" / "backend" / "App.Backend.Api" / "Hubs" / "MaintenanceHub.cs"
        if not hub_file.exists():
            return []
        content = hub_file.read_text(encoding="utf-8")
        pattern = re.compile(r'Task\s+(\w+)\s*\(([^)]*)\);')
        return [f"{m.group(1)}({m.group(2).strip()})" for m in pattern.finditer(content)]


class SequenceDiagramBuilder:
    """Builds validated GitHub-compatible Mermaid sequence diagrams."""

    @staticmethod
    def build_telemetry_flow() -> str:
        return """```mermaid
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
        Service->>Hub: Station freshness turns OFFLINE for its controllers
    end
    deactivate Broker
```"""

    @staticmethod
    def build_maintenance_lifecycle_flow() -> str:
        return """```mermaid
sequenceDiagram
    autonumber
    actor Tech as Technician / Shift Leader (Web UI)
    participant NitroBFF as Nuxt Nitro BFF (/api/maintenance/tickets)
    participant Controller as MaintenanceTicketController (ASP.NET Core REST)
    participant Repo as MaintenanceTicketRepository (EF Core)
    participant Cache as CacheService (Multi-Tier Invalidation)
    participant Hub as MaintenanceHub (SignalR)
    actor Operators as All Connected Fleet Dashboards

    Note over Tech,Operators: Flow 2: Maintenance Ticket Lifecycle & State Transition Flow

    Tech->>+NitroBFF: PATCH /api/maintenance/tickets/{id} { status: "In_Progress", actor: "Gábor Varga" }
    NitroBFF->>+Controller: HTTP PATCH /api/v1/MaintenanceTicket/{id}/status ("In_Progress")
    
    Controller->>Repo: UpdateStatusAsync(id, "In_Progress")
    Repo->>Repo: Append state transition comment & touch updatedAt timestamp
    Repo-->>Controller: MaintenanceTicket (Updated entity)
    
    Controller->>Cache: RemoveAsync("tickets:all:all")
    Controller->>Cache: RemoveAsync("dashboard:metrics")
    Controller->>Cache: SetAsync("tickets:item:{id}", updated, 5 min)
    
    Controller->>+Hub: org group + platform-admins: StatusChanged(id, "In_Progress")
    Controller->>Hub: org group + platform-admins: TicketUpdated(updated)
    Hub-->>-Operators: WebSocket Broadcast: StatusChanged & TicketUpdated
    Operators->>Operators: Kanban column reactive transition (Open -> In_Progress)
    
    Controller-->>-NitroBFF: 204 NoContent
    NitroBFF-->>-Tech: 200 OK (Ticket successfully updated)
```"""

    @staticmethod
    def build_ad_discovery_flow() -> str:
        return """```mermaid
sequenceDiagram
    autonumber
    actor Admin as IT Administrator
    participant WebUI as Nuxt 3 Admin Console
    participant NitroBFF as Nuxt Nitro BFF (/api/activedirectory/ous)
    participant ADController as ActiveDirectoryController (REST API)
    participant MockAD as Mock Active Directory / LDAP Service
    participant DB as AppDbContext (PostgreSQL)

    Note over Admin,DB: Flow 3: Active Directory OU Discovery & Batch Import Flow

    Admin->>+WebUI: Open "Active Directory Synchronization" View
    WebUI->>+NitroBFF: GET /api/activedirectory/ous
    NitroBFF->>+ADController: GET /api/v1/ActiveDirectory/ous
    ADController->>DB: Query existing ClientPcs & AdOuGovernances
    DB-->>ADController: Existing imported hostnames & approved OU list
    
    ADController->>MockAD: Discover plant OUs (VLAN10, VLAN20, VLAN30, VLAN40, VLAN50)
    MockAD-->>ADController: Return 14 plant OUs with candidate IPC/PC hostnames
    ADController->>ADController: Cross-reference already-imported hostnames & set status
    ADController-->>-NitroBFF: List<ActiveDirectoryOU> with hostCount & candidateHosts
    NitroBFF-->>-WebUI: Render OU tree table with approval badges
    
    Admin->>+WebUI: Click "Approve OU & Batch Import Hosts"
    WebUI->>+NitroBFF: POST /api/activedirectory/ous/approve { ouPath, accessLevel: "approved" }
    NitroBFF->>+ADController: POST /api/v1/ActiveDirectory/ous/approve
    ADController->>DB: Save AdOuGovernance entity
    ADController-->>-NitroBFF: 200 OK
    
    WebUI->>+NitroBFF: POST /api/activedirectory/import-hosts { ouPath, hostnames: [...] }
    NitroBFF->>+ADController: POST /api/v1/ActiveDirectory/import-hosts
    ADController->>DB: Insert new ClientPc entities (with VLAN, OU path, CMI hardware template)
    DB-->>ADController: Saved (Entities committed)
    ADController-->>-NitroBFF: ImportSummary { importedCount: 12, skippedCount: 0 }
    NitroBFF-->>-WebUI: Display Toast & Refresh Fleet Table
```"""

    @staticmethod
    def build_pki_cert_flow() -> str:
        return """```mermaid
sequenceDiagram
    autonumber
    actor SecAdmin as Security Administrator
    participant Modal as PKI Certificate Manager (Vue 3)
    participant CertController as CertificateManagementController
    participant CryptoEngine as System.Security.Cryptography (X.509 / RSA-2048)
    participant DB as PostgreSQL (ClientCertificates & OuCertRules)
    actor Kiosk as Kiosk Terminal (computer certificate)
    participant Proxy as TLS-terminating Reverse Proxy

    Note over SecAdmin,Kiosk: Flow 4: PKI Root CA Import & Client Certificate Auto-Enrollment Flow

    SecAdmin->>+Modal: Open PKI Manager & Click "Generate / Import Root CA"
    Modal->>+CertController: POST /api/v1/CertificateManagement/root-ca/import (Pem Certificate & Key)
    CertController->>CryptoEngine: Parse and validate X509BasicConstraints & KeyCertSign flags
    CertController->>DB: Store Root CA Record (Status: "Active", IsRootCa: true)
    DB-->>CertController: Committed
    CertController-->>-Modal: Root CA Active (Thumbprint, Expiry: 10 Years)

    SecAdmin->>+Modal: Create OU Auto-Enrollment Policy (e.g. OU=Robotics,OU=VLAN10)
    Modal->>+CertController: POST /api/v1/CertificateManagement/ou-rules
    CertController->>DB: Insert OuCertificateEnrollmentRule (ValidityYears: 2, KeyAlgorithm: "RSA-2048")
    DB-->>CertController: Rule Saved
    CertController-->>-Modal: Rule Enacted

    SecAdmin->>+Modal: Trigger "Sync OU Certificates"
    Modal->>+CertController: POST /api/v1/CertificateManagement/sync-ou-certificates
    CertController->>DB: Query Client PCs matching active OU rules lacking valid certs
    DB-->>CertController: Unenrolled PCs (e.g. IPC-L01-OP030-DEDICATED)
    
    loop For Each Unenrolled Controller
        CertController->>CryptoEngine: RSA.Create(2048) & CertificateRequest(CN=hostname, OU=ouPath)
        CertController->>CryptoEngine: SignWithPem(RootCA, HashAlgorithmName.SHA256)
        CertController->>DB: Store ClientCertificateRecord with Thumbprint, Expiry, SerialNumber
    end
    DB-->>CertController: Batch committed
    CertController-->>-Modal: Sync Complete (Generated: N, Active: Total)

    Kiosk->>+Proxy: TLS handshake presenting the issued computer certificate
    Proxy->>Proxy: Verify certificate against the Heimdall trust bundle
    Proxy->>CertController: Forward request with X-Client-Cert header
    CertController->>CryptoEngine: Validate forwarded certificate against trust store
    CryptoEngine-->>CertController: Certificate valid
    CertController-->>Proxy: Kiosk terminal authenticated
    Proxy-->>-Kiosk: Terminal page served
```"""

    @staticmethod
    def build_station_graph_flow() -> str:
        return """```mermaid
sequenceDiagram
    autonumber
    actor Engineer as Maintenance Engineer / Operator
    participant CadMap as CAD Floor View / Station Pin
    participant TreeModal as StationComponentTreeModal (Vue 3)
    participant NitroBFF as Nuxt Nitro BFF (/api/proxy/v1/Machine)
    participant StationCtrl as MachineController (REST API)
    participant TptRepo as StationRepository (EF Core TPT)
    participant DB as PostgreSQL (inventory_items, station_controllers)

    Note over Engineer,DB: Flow 5: Station Component Graph Traversal Flow

    Engineer->>+CadMap: Click Station Pin on CAD Drawing (e.g. L01-OP030)
    CadMap->>+TreeModal: Open modal with stationIdentifier="L01-OP030"
    TreeModal->>+NitroBFF: GET /api/proxy/v1/Machine/{id}
    NitroBFF->>+StationCtrl: GET /api/v1/Machine/{id}
    StationCtrl->>+TptRepo: GetStationWithControllersAndComponentsAsync(id)
    
    TptRepo->>DB: SELECT FROM stations JOIN inventory_items ON stations.id = inventory_items.id
    TptRepo->>DB: SELECT FROM station_controllers JOIN client_pcs ON ...
    TptRepo->>DB: SELECT FROM inventory_items WHERE machine_id = @id (Installed Components)
    DB-->>TptRepo: Station row, 1..N IPC/PLC controllers, N discrete hardware components
    TptRepo-->>-StationCtrl: Station aggregate entity with M:N controller junctions
    
    StationCtrl-->>-NitroBFF: StationDetailDto (Controllers, Sensors, Drives, Sub-assemblies)
    NitroBFF-->>-TreeModal: JSON payload with graph hierarchy
    
    TreeModal->>TreeModal: Construct recursive DOM tree (Station -> Controllers -> Discrete Parts)
    TreeModal-->>-Engineer: Interactive tree view with lifecycle badges (InMachine, InStorage, UnderRepair)
```"""

    @staticmethod
    def build_agent_command_flow() -> str:
        return """```mermaid
sequenceDiagram
    autonumber
    actor Admin as Site Automation Engineer
    participant WebUI as Nuxt Web Admin Panel
    participant CmdCtrl as AgentCommandController (REST) / TelemetryIngestionService
    participant DB as PostgreSQL (QueuedAgentCommands Table)
    participant Broker as Embedded MQTT Broker
    actor Agent as Edge Agent Daemon (Background Worker)
    participant LocalExecutor as Edge Command Dispatcher & Recipe Engine

    Note over Admin,LocalExecutor: Flow 6: Edge Agent Command Dispatch & Execution Loop

    Admin->>+WebUI: Submit Configuration Update or Diagnostics Request
    WebUI->>+CmdCtrl: POST /api/v1/AgentCommand/{clientPcId}/update-config (Config, Signature)
    CmdCtrl->>CmdCtrl: Validate user policy [Authorize(Policy = "EndpointConfigManagement")]
    CmdCtrl->>DB: INSERT INTO queued_agent_commands (ClientPcId, Type: "UPDATE_CONFIG", Payload, Signature, CreatedAt)
    DB-->>CmdCtrl: Queued
    CmdCtrl-->>-WebUI: 200 OK { Message: "Command queued successfully" }
    WebUI-->>-Admin: Display success notification

    loop Every system_info report (60s with +/-10% jitter)
        Agent->>+Broker: PUBLISH heimdall/devices/{id}/system_info (SystemInfoRequest)
        Broker->>CmdCtrl: MqttIngestionHostedService -> ProcessSystemInfoAsync
        CmdCtrl->>DB: SELECT FROM queued_agent_commands WHERE ClientPcId = @id AND ExecutedAt IS NULL
        DB-->>CmdCtrl: Pending ServerCommand ("UPDATE_CONFIG")
        CmdCtrl->>Broker: PUBLISH heimdall/devices/{id}/commands (SystemInfoResponse, QoS 1)
        Broker-->>-Agent: Deliver commands
    end

    Agent->>LocalExecutor: Dispatch command to Recipe Engine
    LocalExecutor->>LocalExecutor: Cryptographically verify RSA-SHA256 / Ed25519 signature
    
    alt Signature Valid
        LocalExecutor->>LocalExecutor: Atomically apply new polling parameters & restart driver handles
        Agent->>Broker: PUBLISH heimdall/devices/{id}/events AgentEventMessage(Level="Info", Message="Config applied successfully")
    else Signature Invalid
        LocalExecutor->>LocalExecutor: Reject payload & quarantine command
        Agent->>Broker: PUBLISH heimdall/devices/{id}/events AgentEventMessage(Level="Critical", Message="Signature verification failed")
    end
```"""

    def generate_master_gallery(self) -> str:
        """Assembles the complete master visual reference document."""
        return f"""# Master Sequence Diagrams & Protocol Interaction Gallery

This document serves as the canonical visual reference gallery for the Heimdall manufacturing intelligence and telemetry platform.
It documents all core communication protocols, end-to-end data flows, real-time event broadcasting, and edge device lifecycle operations.

---

## 1. Edge Telemetry Ingestion & Real-Time Broadcast Flow

Agents (and the fleet simulator) publish protobuf messages over MQTT 5 to the MQTTnet broker embedded in the backend, authenticated by an agent key. The ingestion hosted service hands them to the telemetry ingestion service: unchanged PLC memory blocks are dropped by the change gate (arrival is still recorded for station freshness), changed blocks become station snapshots in the cache and SignalR events to organisation-scoped groups. Agent lifecycle events, including the MQTT last will, are stored as agent events.

{self.build_telemetry_flow()}

---

## 2. Maintenance Ticket Lifecycle & State Transition Flow

Incident reports and automated alarms trigger maintenance tickets across the 8-stage Kanban lifecycle. Status transitions update database entities, evict relevant multi-tier cache entries, and broadcast real-time updates via SignalR to maintain synchronized views across all operator terminals.

{self.build_maintenance_lifecycle_flow()}

---

## 3. Active Directory OU Discovery & Batch Import Flow

The IT administration module queries factory Active Directory and LDAP domains to discover industrial controllers grouped by organizational units and production VLANs. Discovered hosts undergo governance approval before batch insertion into the central inventory.

{self.build_ad_discovery_flow()}

---

## 4. PKI Root CA Import & Client Certificate Auto-Enrollment Flow

The Heimdall PKI issues X.509 computer certificates. This flow illustrates root CA import, declarative OU enrollment rules, certificate generation, and a kiosk terminal authenticating with its computer certificate, which a TLS-terminating reverse proxy verifies and forwards in the X-Client-Cert header. Agent telemetry itself is authenticated by an agent key over MQTT, not by client certificates.

{self.build_pki_cert_flow()}

---

## 5. Station Component Graph Traversal Flow

Manufacturing stations model complex multi-controller assemblies using Table-per-Type (TPT) inheritance and many-to-many junction entities. Selecting a station on factory CAD layouts traverses the component graph to render connected IPCs, PLCs, sensors, and spare parts.

{self.build_station_graph_flow()}

---

## 6. Edge Agent Command Dispatch & Execution Loop

Engineers issue remote configuration updates and diagnostic recipes through authenticated REST APIs. Commands are queued in PostgreSQL, delivered to edge agents over MQTT on the commands topic after their next system_info report, verified cryptographically, and executed locally.

{self.build_agent_command_flow()}

---

*Generated automatically by `tools/generate_sequence_diagrams.py` based on source code analysis.*
"""


def update_documentation_files(builder: SequenceDiagramBuilder, root: Path):
    """Embeds generated Mermaid sequence diagrams into system documentation."""
    # 1. Update SYSTEM_ARCHITECTURE.md with central telemetry flow
    sys_arch_file = root / "docs" / "architecture" / "SYSTEM_ARCHITECTURE.md"
    if sys_arch_file.exists():
        content = sys_arch_file.read_text(encoding="utf-8")
        diagram_block = f"""### 4.3 Central Telemetry & Maintenance Interaction Sequence

```mermaid
sequenceDiagram
    autonumber
    actor EdgeNode as Edge Agent Daemon (C# / TwinCAT)
    participant Broker as Embedded MQTT Broker
    participant Ingest as MqttIngestionHostedService
    participant Service as TelemetryIngestionService
    participant Cache as Station snapshot cache (L1 / L2 Redis)
    participant Hub as MaintenanceHub (SignalR)
    actor Browser as Nuxt Web Dashboard

    EdgeNode->>Broker: PUBLISH heimdall/devices/{{id}}/plc_memory
    Broker->>+Ingest: Deliver message
    Ingest->>+Service: ProcessPlcMemoryAsync
    Service->>Cache: Store station snapshot (unchanged blocks dropped by change gate)
    Service->>+Hub: StationTelemetryReceived to org group
    Hub-->>-Browser: WebSocket event
    Service-->>-Ingest: Done
    deactivate Ingest
```"""
        if "### 4.3 Central Telemetry & Maintenance Interaction Sequence" not in content:
            content += f"\n\n{diagram_block}\n"
            sys_arch_file.write_text(content, encoding="utf-8")
            print(f"[OK] Updated {sys_arch_file.relative_to(root)} with Mermaid sequence diagram.")

    # 2. Update EDGE_AGENT_AND_PROTOCOLS.md with agent polling and command loop
    edge_proto_file = root / "docs" / "architecture" / "EDGE_AGENT_AND_PROTOCOLS.md"
    if edge_proto_file.exists():
        content = edge_proto_file.read_text(encoding="utf-8")
        agent_diagram = f"""### 1.4 Agent-Backend Communication Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Agent as Edge Agent Daemon
    participant Broker as Embedded MQTT Broker (port 1883)
    participant Ingest as TelemetryIngestionService
    participant DB as PostgreSQL
    actor Web as Operator Web UI

    loop Baseline Cycle (60s +/- 10% Jitter)
        Agent->>Broker: PUBLISH heimdall/devices/{{id}}/system_info
        Broker->>+Ingest: Deliver message
        Ingest->>DB: Upsert telemetry and check queued commands
        Ingest-->>-Broker: PUBLISH heimdall/devices/{{id}}/commands (if any)
        Broker->>Agent: Deliver commands

        opt Commands Received
            Agent->>Agent: Verify Cryptographic Signature (RSA/Ed25519)
            Agent->>Agent: Apply Recipe Parameters & Restart Drivers
            Agent->>Broker: PUBLISH heimdall/devices/{{id}}/events (AgentEventMessage: "Applied")
        end
    end
```"""
        if "### 1.4 Agent-Backend Communication Sequence" not in content:
            content += f"\n\n{agent_diagram}\n"
            edge_proto_file.write_text(content, encoding="utf-8")
            print(f"[OK] Updated {edge_proto_file.relative_to(root)} with Mermaid sequence diagram.")

    # 3. Update API_REFERENCE.md with maintenance status flow
    api_ref_file = root / "docs" / "api" / "API_REFERENCE.md"
    if api_ref_file.exists():
        content = api_ref_file.read_text(encoding="utf-8")
        api_diagram = f"""### 2.5 Real-Time Maintenance Event Sequence

```mermaid
sequenceDiagram
    autonumber
    actor Client as Web Client
    participant API as MaintenanceTicketController
    participant DB as Database
    participant Hub as MaintenanceHub (SignalR)
    actor Subscribers as All Connected Operators

    Client->>+API: PATCH /api/v1/MaintenanceTicket/{id}/status ("Resolved")
    API->>DB: UpdateStatusAsync(id, "Resolved")
    DB-->>API: Updated
    API->>+Hub: org group + platform-admins: StatusChanged(id, "Resolved")
    Hub-->>-Subscribers: Real-Time Event: StatusChanged
    API-->>-Client: 204 NoContent
```"""
        if "### 2.5 Real-Time Maintenance Event Sequence" not in content:
            content += f"\n\n{api_diagram}\n"
            api_ref_file.write_text(content, encoding="utf-8")
            print(f"[OK] Updated {api_ref_file.relative_to(root)} with Mermaid sequence diagram.")


def main():
    parser = argparse.ArgumentParser(description="Generate and validate Mermaid sequence diagrams from Heimdall codebase.")
    parser.add_argument("--output", "-o", default=str(REPO_ROOT / "docs" / "architecture" / "SEQUENCE_DIAGRAMS.md"),
                        help="Path to output markdown file (default: docs/architecture/SEQUENCE_DIAGRAMS.md)")
    parser.add_argument("--check", action="store_true",
                        help="Validate that the output file exists and matches current generated content (CI mode)")
    parser.add_argument("--update-docs", action="store_true",
                        help="Embed diagrams directly into SYSTEM_ARCHITECTURE.md, EDGE_AGENT_AND_PROTOCOLS.md, and API_REFERENCE.md")
    args = parser.parse_args()

    builder = SequenceDiagramBuilder()
    generated_md = builder.generate_master_gallery()
    output_path = Path(args.output).resolve()

    if args.check:
        if not output_path.exists():
            print(f"[FAIL] Target file does not exist: {output_path}", file=sys.stderr)
            sys.exit(1)
        existing_content = output_path.read_text(encoding="utf-8")
        if existing_content.strip() != generated_md.strip():
            print(f"[FAIL] {output_path} is out of date. Run without --check to regenerate.", file=sys.stderr)
            sys.exit(1)
        print(f"[PASS] Sequence diagrams in {output_path} are up to date.")
        sys.exit(0)

    # Write master gallery
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(generated_md, encoding="utf-8")
    print(f"[OK] Generated sequence diagram gallery at {output_path}")

    if args.update_docs:
        update_documentation_files(builder, REPO_ROOT)

    print("[SUCCESS] All sequence diagrams processed successfully.")


if __name__ == "__main__":
    main()
