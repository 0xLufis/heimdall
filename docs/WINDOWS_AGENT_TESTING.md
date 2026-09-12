# Windows Agent Docker Test Environment & Windows Endpoint Testing

This document details the architecture, configuration, and testing procedures for running the Heimdall Edge Agent daemon (`App.Agent.Daemon`) inside an automated Windows container on Linux.

---

## 1. Architectural Overview

Running Windows in a Linux Docker environment requires hardware-assisted full virtualization (QEMU/KVM) wrapped inside a container. The setup uses `dockurr/windows` as the base engine with customized OEM unattended provisioning.

```
+----------------------------------------------------------------------------------------------------------------+
| Linux Host (KVM Accelerated)                                                                                  |
|                                                                                                                |
|  +-----------------------------+      dotnet publish         +-----------------------------------------------+ |
|  | agent/App.Agent.Daemon      | --------------------------> | ./infra/windows/shared/agent                  | |
|  | (.NET 10 Daemon Source)     |      (win-x64 build)        | (Bi-directional Host Mount)                   | |
|  +-----------------------------+                             +-----------------------------------------------+ |
|                 |                                                                    |                         |
|                 |                                                                    v                         |
|  +-----------------------------+      WinRM (:5985)          +-----------------------------------------------+ |
|  | tools/test_windows_agent.py | --------------------------> | heimdall_windows_agent (Docker QEMU Container)| |
|  | (Automated Test Suite)      |                             |  - Windows 10 Enterprise LTSC (10l)           | |
|  +-----------------------------+      HTTP (:5998)           |  - WinRM Service & OpenSSH                    | |
|                 |                --------------------------> |  - Configurator HTTP API (:5998)              | |
|                 |                                            |  - Heimdall Agent Daemon Process              | |
|                 v                                            |  - WMI, Registry, EventLog, DPAPI Store       | |
|  +-----------------------------+                             +-----------------------------------------------+ |
|  | heimdall_backend (:5001)    | <===========================================+                                 |
|  | (REST :5099 & gRPC :5001)   |        gRPC Telemetry & Remote Command Dispatch                               |
|  +-----------------------------+        over 'heimdall_dev_net'                                                |
+----------------------------------------------------------------------------------------------------------------+
```

### Why Windows 10 LTSC (`10l`)?
- **Low RAM Footprint**: Idle memory is only ~1.2 GB - 1.8 GB (compared to 3.5+ GB on Windows 11).
- **Intact Subsystems**: Unlike stripped third-party ISOs (e.g. Tiny10), Windows 10 LTSC retains 100% of official Microsoft WMI providers (`System.Management`), Windows Event Log channels, WinRM services, and Cryptography / DPAPI providers.
- **Enterprise & EULA Compliance**: Adheres to Heimdall's engineering guidelines (`AGENTS.MD`), complying with Microsoft Evaluation terms, NIS2, and TISAX auditability.

---

## 2. Directory Structure

```
infra/windows/
├── Dockerfile           # Extends dockurr/windows:latest with Windows 10 LTSC defaults
├── oem/
│   ├── install.bat      # Windows SetupComplete hook executing setup.ps1
│   └── setup.ps1        # WinRM, Firewall, Beckhoff mock registry, and daemon watcher setup
├── shared/              # Bi-directional host-guest shared mount (drive Z:\ in Windows)
│   └── agent/           # Compiled self-contained win-x64 agent binaries
└── storage/             # Persistent QEMU virtual disk (ignored by Git)
```

---

## 3. Network Ports & Services

| Port | Service | Description | Credentials |
|------|---------|-------------|-------------|
| `8006` | Web VNC Viewer | Interactive browser-based display of the Windows desktop | N/A |
| `3389` | RDP | Windows Remote Desktop Protocol | `Docker` / `Password123!` |
| `5985` | WinRM HTTP | Windows Remote Management for headless CLI automation | `Docker` / `Password123!` |
| `5998` | Configurator API | Heimdall Agent HTTP REST API (`/api/config`) | N/A (Dev network) |
| `2222` | OpenSSH | In-guest Secure Shell access | `Docker` / `Password123!` |

---

## 4. Quick Start Guide

### Prerequisites
- Linux host with Intel VT-x or AMD-V enabled in BIOS/UEFI.
- `/dev/kvm` accessible to user/docker.
- .NET 10 SDK and Docker Compose.

### Step 1: Check Prerequisites & Compile Agent for Windows
```bash
python3 tools/test_windows_agent.py --check-prereqs
python3 tools/test_windows_agent.py --build-agent
```
This builds a self-contained `win-x64` executable `App.Agent.Daemon.exe` directly into `infra/windows/shared/agent/`.

### Step 2: Start the Windows Container
The Windows container is gated under the `windows` profile in `docker-compose.yml`:
```bash
docker compose --profile windows up -d windows-agent
```
*Note: First boot will download the official Windows 10 LTSC image and execute the automated unattended setup. You can observe progress in your web browser at `http://localhost:8006`.*

### Step 3: Check Container Status
```bash
python3 tools/test_windows_agent.py --status
```

---

## 5. Running the Tests

### A. Backend Windows Invariant Integration Tests (xUnit)
Validates that the backend gRPC telemetry pipeline accurately ingests and persists Windows-specific WMI UUIDs, `C:\` disk metrics, EventLog items, Beckhoff RT drivers, and command queues:
```bash
dotnet test tests/backend/App.Backend.Tests/App.Backend.Tests.csproj --filter "FullyQualifiedName~WindowsEndpointTelemetryTests"
```

### B. Live Windows Endpoint Integration Suite
Validates reachability and hits the agent's HTTP endpoint and backend registration:
```bash
python3 tests/integration/test_windows_endpoint.py
```

### C. Direct Agent HTTP Configurator Query
Once the agent daemon is active in the Windows container, hit its embedded HTTP API:
```bash
curl -s http://localhost:5998/api/config | jq .
```

---

## 6. Customization

You can override default settings via environment variables in `.env` or CLI:

```bash
# Use Tiny10 instead of Windows 10 LTSC
WIN_VERSION="tiny10" docker compose --profile windows up -d windows-agent

# Use Windows 11 LTSC with 4GB RAM
WIN_VERSION="11l" WIN_RAM="4G" docker compose --profile windows up -d windows-agent
```
