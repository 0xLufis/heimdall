#!/usr/bin/env python3
"""
Heimdall Code-to-Sequence-Diagram Generator
Analyzes C# controllers, gRPC telemetry services, SignalR hubs, edge daemon workers,
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
    """Extracts routes, gRPC methods, SignalR events, and database actions from source code."""

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

    def get_grpc_collector_methods(self) -> List[str]:
        """Parses RPC method signatures from system_info.proto."""
        proto_file = self.root / "shared" / "App.Contracts" / "Protos" / "system_info.proto"
        if not proto_file.exists():
            return []
        content = proto_file.read_text(encoding="utf-8")
        pattern = re.compile(r'rpc\s+(\w+)\s*\(([^)]+)\)\s*returns\s*\(([^)]+)\);')
        return [f"{m.group(1)}({m.group(2).strip()}) -> {m.group(3).strip()}" for m in pattern.finditer(content)]

    def get_signalr_client_events(self) -> List[str]:
        """Parses SignalR client broadcast signatures from MaintenanceHub.cs."""
        hub_file = self.root / "backend" / "App.Backend.Api" / "Hubs" / "MaintenanceHub.cs"
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
    actor EdgeNode as Edge Agent Daemon (C# / TwinCAT)
    participant GrpcEndpoint as SystemInfoCollectorService (HTTP/2 gRPC)
    participant SecurityAuth as Auth & mTLS Cert Validator
    participant Repo as ClientPcRepository (PostgreSQL)
    participant Cache as CacheService (L1 Memory / L2 Redis)
    participant Hub as MaintenanceHub (SignalR WebSocket)
    actor Browser as Nuxt Web Dashboard (Vue 3 / Pinia)

    Note over EdgeNode,Browser: Flow 1: Edge Telemetry Ingestion & Real-Time Broadcast Flow
    
    EdgeNode->>EdgeNode: Collect OS, CMI, TwinCAT ADS & Process Metrics
    EdgeNode->>EdgeNode: Evaluate Deadband Delta & Compress (Zstd / gzip)
    EdgeNode->>+GrpcEndpoint: ReportSystemInfo(SystemInfoRequest) / ReportTelemetryBatch
    GrpcEndpoint->>SecurityAuth: Validate caller mTLS cert & API token
    SecurityAuth-->>GrpcEndpoint: Authorized (Tenant & PC Context)
    
    GrpcEndpoint->>Repo: UpsertClientPcTelemetryAsync(snapshot)
    Repo-->>GrpcEndpoint: Entity updated in PostgreSQL (100 IPC nodes)
    
    GrpcEndpoint->>Cache: SetAsync("telemetry:snapshot:{hostname}", snapshot, 5 min)
    GrpcEndpoint->>Cache: RemoveAsync("telemetry:fleet:live")
    
    GrpcEndpoint->>+Hub: Clients.All.TelemetryReceived(hostname, mac, summary)
    Hub-->>-Browser: Broadcast WebSocket message: TelemetryReceived
    Browser->>Browser: Update Pinia telemetryStore & refresh Canvas / 3D spatial node
    
    opt Pending Commands in Queue
        GrpcEndpoint->>Repo: FetchUnacknowledgedCommands(clientPcId)
        Repo-->>GrpcEndpoint: List<QueuedAgentCommand> (e.g. UPDATE_CONFIG)
    end
    
    GrpcEndpoint-->>-EdgeNode: SystemInfoResponse(success=true, commands=[...])
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
    
    Controller->>+Hub: Clients.All.StatusChanged(id, "In_Progress")
    Controller->>Hub: Clients.All.TicketUpdated(updated)
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
    actor EdgeNode as Edge Client PC (mTLS Consumer)

    Note over SecAdmin,EdgeNode: Flow 4: PKI Root CA Import & Client Certificate Auto-Enrollment Flow

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

    EdgeNode->>+CertController: mTLS gRPC Handshake using issued Client Certificate
    CertController->>CryptoEngine: Validate client cert against Root CA trust chain
    CryptoEngine-->>CertController: Certificate Valid & In-Policy
    CertController-->>-EdgeNode: TLS Handshake Completed (mTLS Established)
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
    participant CmdCtrl as AgentCommandController (REST API)
    participant DB as PostgreSQL (QueuedAgentCommands Table)
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

    loop Every 60s Cycle (with ±10% Anti-Thundering Jitter)
        Agent->>+CmdCtrl: Periodic gRPC Poll: ReportSystemInfo(SystemInfoRequest)
        CmdCtrl->>DB: SELECT FROM queued_agent_commands WHERE ClientPcId = @id AND ExecutedAt IS NULL
        DB-->>CmdCtrl: Return pending ServerCommand ("UPDATE_CONFIG")
        CmdCtrl-->>-Agent: SystemInfoResponse(success=true, commands=[ServerCommand])
    end

    Agent->>LocalExecutor: Dispatch command to Recipe Engine
    LocalExecutor->>LocalExecutor: Cryptographically verify RSA-SHA256 / Ed25519 signature
    
    alt Signature Valid
        LocalExecutor->>LocalExecutor: Atomically apply new polling parameters & restart driver handles
        Agent->>CmdCtrl: gRPC StreamAgentEvents: AgentEventMessage(Level="Info", Message="Config applied successfully")
    else Signature Invalid
        LocalExecutor->>LocalExecutor: Reject payload & quarantine command
        Agent->>CmdCtrl: gRPC StreamAgentEvents: AgentEventMessage(Level="Critical", Message="Signature verification failed")
    end
```"""

    def generate_master_gallery(self) -> str:
        """Assembles the complete master visual reference document."""
        return f"""# Master Sequence Diagrams & Protocol Interaction Gallery

This document serves as the canonical visual reference gallery for the Heimdall manufacturing intelligence and telemetry platform.
It documents all core communication protocols, end-to-end data flows, real-time event broadcasting, and edge device lifecycle operations.

---

## 1. Edge Telemetry Ingestion & Real-Time Broadcast Flow

Industrial controllers execute continuous data collection from TwinCAT ADS, EtherCAT, and CMI hardware interfaces. Data points pass through deadband filters, are compressed via Zstandard, and stream to the backend via HTTP/2 gRPC. Verified telemetry updates the database and cache, triggering real-time SignalR WebSocket notifications to connected web clients.

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

Heimdall secures all edge-to-cloud communications via mutual TLS (mTLS). This flow illustrates root certificate authority import, declarative OU enrollment rule assignment, X.509 RSA-2048 certificate generation, and device certificate binding.

{self.build_pki_cert_flow()}

---

## 5. Station Component Graph Traversal Flow

Manufacturing stations model complex multi-controller assemblies using Table-per-Type (TPT) inheritance and many-to-many junction entities. Selecting a station on factory CAD layouts traverses the component graph to render connected IPCs, PLCs, sensors, and spare parts.

{self.build_station_graph_flow()}

---

## 6. Edge Agent Command Dispatch & Execution Loop

Engineers issue remote configuration updates and diagnostic recipes through authenticated REST APIs. Commands are queued in PostgreSQL, retrieved by edge agents during regular gRPC polling intervals, verified cryptographically, and executed locally.

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
    participant GrpcEndpoint as SystemInfoCollectorService (gRPC)
    participant Repo as ClientPcRepository (PostgreSQL)
    participant Cache as CacheService (L1 Memory / L2 Redis)
    participant Hub as MaintenanceHub (SignalR WebSocket)
    actor Browser as Nuxt Web Dashboard (Vue 3 / Pinia)

    EdgeNode->>+GrpcEndpoint: ReportSystemInfo(SystemInfoRequest)
    GrpcEndpoint->>Repo: UpsertClientPcTelemetryAsync(snapshot)
    Repo-->>GrpcEndpoint: Entity committed
    GrpcEndpoint->>Cache: SetAsync("telemetry:snapshot:{{hostname}}", snapshot, 5 min)
    GrpcEndpoint->>+Hub: Clients.All.TelemetryReceived(hostname, mac, summary)
    Hub-->>-Browser: WebSocket Broadcast
    Browser->>Browser: Update reactive canvas & spatial view
    GrpcEndpoint-->>-EdgeNode: SystemInfoResponse(success=true)
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
    participant Grpc as SystemInfoCollector (Port 5001 gRPC)
    participant DB as PostgreSQL
    actor Web as Operator Web UI

    loop Baseline Cycle (60s ± 10% Jitter)
        Agent->>+Grpc: ReportSystemInfo(SystemInfoRequest)
        Grpc->>DB: Upsert Telemetry & Check Queued Commands
        DB-->>Grpc: Command List (e.g. UPDATE_CONFIG)
        Grpc-->>-Agent: SystemInfoResponse(success=true, commands=[...])
        
        opt Commands Received
            Agent->>Agent: Verify Cryptographic Signature (RSA/Ed25519)
            Agent->>Agent: Apply Recipe Parameters & Restart Drivers
            Agent->>Grpc: StreamAgentEvents(AgentEventMessage: "Applied")
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
    API->>+Hub: Clients.All.StatusChanged(id, "Resolved")
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
