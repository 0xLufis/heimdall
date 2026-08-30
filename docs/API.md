# Heimdall API Specification & Interface Contracts

This document provides a detailed reference for all interfaces exposed by Heimdall:
1. **REST Web API** (`HTTP/JSON`)
2. **Agent Telemetry gRPC Service** (`HTTP/2 Protobuf`)
3. **OPC UA Server & Gateway API** (`opc.tcp://`)

---

## 1. REST Web API (`/api/v1/`)

The REST Web API is hosted by `App.Backend.Api` on port `5000` (HTTP) / `5001` (HTTPS).

### Authentication & Tenant Headers
All requests must include session authentication cookies or Bearer tokens provided by **Better-Auth**, as well as the active organization tenant header:
- `Authorization: Bearer <session_token>`
- `X-Organization-Id: <organization_uuid>`

---

### 1.1 Stations (Production Machines)

#### `GET /api/v1/stations`
Retrieves a paginated summary list of production stations.

- **Query Parameters**:
  - `page` (int, default `1`): Page number.
  - `pageSize` (int, default `25`): Items per page.
  - `search` (string, optional): Filter by name or custom identifier.
- **Response `200 OK`**:
```json
{
  "items": [
    {
      "id": "c39a82e4-180b-48c4-912f-2d6e3bf8e801",
      "name": "Assembly Station 01",
      "customIdentifier": "LINE-A-OP10",
      "pinnedObjectHandle": "HANDLE_ST_01",
      "primaryControllerId": "e14b99f2-2b63-4c91-923a-59b43d2c1102",
      "primaryControllerName": "ASSEMBLY-ST-01",
      "isOnline": true,
      "alertCount": 0
    }
  ],
  "totalCount": 10,
  "page": 1,
  "pageSize": 25
}
```

#### `GET /api/v1/stations/{id}`
Retrieves detailed information for a specific station, including associated controllers, hardware components, and equipment interconnects.

- **Response `200 OK`**:
```json
{
  "id": "c39a82e4-180b-48c4-912f-2d6e3bf8e801",
  "name": "Assembly Station 01",
  "customIdentifier": "LINE-A-OP10",
  "pinnedObjectHandle": "HANDLE_ST_01",
  "controllers": [
    {
      "id": "e14b99f2-2b63-4c91-923a-59b43d2c1102",
      "hostname": "ASSEMBLY-ST-01",
      "ipAddress": "192.168.1.101",
      "controllerType": "IPC",
      "controlRole": "Primary",
      "isPrimary": true,
      "lastOnline": "2026-08-30T00:15:00Z"
    }
  ],
  "hardwareComponents": [
    {
      "id": "a91b2c3d-4e5f-6a7b-8c9d-0e1f2a3b4c5d",
      "name": "Servo Drive Axis X",
      "modelNumber": "AX5106-0000-0214",
      "serialNumber": "BECK-SERVO-9921",
      "category": "Servo"
    }
  ]
}
```

---

### 1.2 Controllers (IPCs / PLCs / Soft-PLCs / Devices)

#### `GET /api/v1/controllers`
Lists industrial controllers with live online telemetry status.

#### `POST /api/v1/controllers/{id}/commands`
Queues a remote command for the agent running on the target IPC/Controller.

- **Request Body**:
```json
{
  "type": "UPDATE_CONFIG",
  "payload": "{\"SamplingIntervalSeconds\": 30}",
  "signature": "MEQCIH...=="
}
```
- **Response `202 Accepted`**:
```json
{
  "commandId": "f83a912b-7c8d-4e9f-0a1b-2c3d4e5f6a7b",
  "status": "Queued",
  "createdAt": "2026-08-30T00:20:00Z"
}
```

---

### 1.3 Copia Automation Integration Webhook

#### `POST /api/v1/integrations/copia/webhook`
Receives Copia Automation Git push notifications when PLC/Soft-PLC project code is committed or deployed.

- **Headers**: `X-Copia-Signature: sha256=...`
- **Request Body**:
```json
{
  "event": "push",
  "repository": {
    "name": "AssemblyLineA_TwinCAT",
    "url": "https://copia.io/org/assembly-line-a-twincat"
  },
  "commit": {
    "sha": "a1b2c3d4e5f67890123456789abcdef012345678",
    "message": "Update motion safety routines for Station OP10",
    "author": "controls.engineer@company.com",
    "timestamp": "2026-08-30T00:10:00Z"
  },
  "affectedAssets": [
    {
      "softwareAssetId": "b10a92f8-3c4d-5e6f-7a8b-9c0d1e2f3a4b",
      "controllerId": "e14b99f2-2b63-4c91-923a-59b43d2c1102"
    }
  ]
}
```

---

## 2. Agent Telemetry gRPC Interface (`Protos/system_info.proto`)

Agents running on IPCs connect over gRPC (`HTTP/2`) to report system metrics, telemetry, driver details, and inventory changes.

```protobuf
syntax = "proto3";

package system_info;

import "google/protobuf/timestamp.proto";

service SystemInfoCollector {
  rpc ReportSystemInfo (SystemInfoRequest) returns (SystemInfoResponse);
  rpc GetPendingCommands (CommandRequest) returns (CommandListResponse);
  rpc AcknowledgeCommand (CommandAckRequest) returns (CommandAckResponse);
}

message SystemInfoRequest {
  string hostname = 1;
  string machine_identifier = 2;
  string mac_address = 3;
  google.protobuf.Timestamp last_online = 4;
  DiskInfo disk_info = 5;
  repeated InventoryComponent components = 6;
  BeckhoffDriverInfo beckhoff_rt_info = 7;
}

message DiskInfo {
  double total_free_gb = 1;
  double os_drive_free_gb = 2;
  map<string, double> drives = 3;
}

message InventoryComponent {
  string name = 1;
  string technology = 2;
  string type = 3; // "hardware", "software", "telemetry", "driver"
  string data_json = 4;
}

message BeckhoffDriverInfo {
  string adapter_name = 1;
  string driver_version = 2;
  string service_name = 3; // e.g., "TcRTEthernet"
  string pci_device_id = 4; // e.g., "PCI\VEN_8086&DEV_1539"
  bool is_realtime_driver_bound = 5;
}

message SystemInfoResponse {
  bool success = 1;
  string message = 2;
  google.protobuf.Timestamp server_time = 3;
}
```

---

## 3. OPC UA Server & Gateway Specification

Heimdall exposes an integrated **OPC UA Server** to allow existing Industrial SCADA, MES, and Data Collection frameworks (Ignition, Kepware, Wonderware, Node-RED) to consume asset telemetry without custom API adapters.

- **Endpoint URL**: `opc.tcp://0.0.0.0:4840/Heimdall`
- **Security Policies**: `Basic256Sha256` (Sign & Encrypt), `Aes128_Sha256_RsaOaep`
- **Authentication**: Username/Password or X.509 Client Certificates

### OPC UA Address Space Hierarchy (OPC 10000-100 Devices Compliant)

```
Root
└── Objects
    └── Heimdall
        ├── Stations
        │   └── Station_LINE_A_OP10 (FolderType)
        │       ├── CustomIdentifier (BaseDataVariableType, String)
        │       ├── IsOnline (BaseDataVariableType, Boolean)
        │       └── Controllers (FolderType)
        │           └── ASSEMBLY-ST-01 (DeviceType)
        │               ├── Hostname (String)
        │               ├── IPAddress (String)
        │               ├── Telemetry
        │               │   ├── CpuLoad (Double, EURange: 0..100)
        │               │   ├── RamUsage (Double, EURange: 0..100)
        │               │   └── OsDriveFreeGB (Double)
        │               └── BeckhoffRT
        │                   ├── DriverBound (Boolean)
        │                   └── DriverVersion (String)
```

