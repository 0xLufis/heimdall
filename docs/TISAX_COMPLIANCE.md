# Heimdall TISAX Compliance & Security Assessment Mapping

This document details how the **Heimdall Industrial Management System** aligns with the requirements of **TISAX (Trusted Information Security Assessment Exchange)** based on the **VDA Information Security Assessment (VDA ISA 6.0)** catalog for **High Protection Needs (Assessment Level 2 & 3)**.

---

## 1. Executive Security Overview

Manufacturing and automotive OT environments require strict information security controls to protect intellectual property, prevent unauthorized access to production lines, and ensure auditability. Heimdall embeds security controls at every architectural layer:

```mermaid
graph TD
    subgraph Identity_Auth ["Identity & Access Control (VDA ISA 1.1 - 1.4)"]
        BetterAuth[Better-Auth Framework]
        RBAC[Role-Based & Tenant Isolation]
    end

    subgraph Data_Protection ["Data Security & Cryptography (VDA ISA 2.1 - 2.3)"]
        GlobalFilter[EF Core Global Query Filters]
        TLS[TLS 1.3 / mTLS Transport Encryption]
        CmdSign[ECDSA Command Payload Signing]
    end

    subgraph Audit_Traceability ["Auditability & Event Logging (VDA ISA 5.1 - 5.3)"]
        AgentEvents[Immutable Agent Event Log]
        AuditLogs[System Audit Trail]
    end

    BetterAuth --> RBAC
    RBAC --> GlobalFilter
    TLS --> CmdSign
    CmdSign --> AgentEvents
    AgentEvents --> AuditLogs
```

---

## 2. VDA ISA 6.0 Control Mapping Matrix

| VDA ISA 6.0 ID | Security Control Area | Heimdall Technical Implementation | Compliance Verification |
|---|---|---|---|
| **ISA 1.1** | **Identity & Access Management** | Authenticated session management powered by Better-Auth (`auth` schema). Enforces multi-factor authentication (MFA) and unique user identity. | `AuthSession` token validation on REST & gRPC API endpoints. |
| **ISA 1.2** | **Tenant & Data Isolation** | Multi-tenancy enforced at DbContext level using EF Core Global Query Filters (`OrganizationId`). | Multi-tenant query isolation verified via unit tests (`App.Backend.Tests`). |
| **ISA 1.3** | **Role-Based Access Control (RBAC)** | Fine-grained JSON privilege grants defined per user role (`UserRole.Privileges`). | Checked by API controller authorization attributes. |
| **ISA 2.1** | **Cryptography & Key Management** | Cryptographic verification of queued remote agent commands via `Signature` validation (ECDSA/RSA). | `QueuedAgentCommand` payload verification in agent daemon. |
| **ISA 2.2** | **Transport Security** | Mandatory HTTPS (TLS 1.3) for REST API; encrypted SSL connections to PostgreSQL 17; mTLS for gRPC telemetry streams. | Server SSL certificates in `infra/database/certs/server.crt`. |
| **ISA 2.3** | **Secret Management** | Zero plain-text credentials stored in codebase. Database credentials and keys loaded from secret files (`infra/database/secrets/`). | Permitted 600 file permissions on keys; environment variable injection. |
| **ISA 5.1** | **System Auditability & Logging** | Immutable logging of security events, configuration updates, and heartbeat failures in `agent_events`. | Centralized `AgentEvent` EF Core entity with UTC timestamps. |
| **ISA 5.2** | **OT & IT Network Segmentation** | Clear separation between IT management network (HTTP/JSON API) and OT fieldbus/telemetry network (mTLS gRPC / OPC UA). | Purdue model alignment in architecture documentation. |

---

## 3. Cryptographic Command Validation Specification

To prevent unauthorized command execution on Industrial PCs (such as unauthorized configuration updates or remote service restarts), all agent commands are cryptographically signed before being queued.

```
+------------------+         Signed Payload         +-------------------+
|  Heimdall Admin  |------------------------------->|  Agent Daemon     |
|  Console / API   |  (Payload + ECDSA Signature)   |  (Industrial PC)  |
+------------------+                                +-------------------+
                                                              |
                                                              v
                                                    +-------------------+
                                                    | Verify Signature  |
                                                    | against Public    |
                                                    | Key Certificate   |
                                                    +-------------------+
                                                              |
                                                     [Valid]  |  [Invalid]
                                                        +-----+-----+
                                                        |           |
                                                        v           v
                                                     Execute     Reject &
                                                     Command      Audit Log
```

1. **Signing**: When an administrator queues an agent command via `POST /api/v1/controllers/{id}/commands`, the backend signs the JSON payload string using the system private key.
2. **Transmission**: The signature is stored in `QueuedAgentCommand.Signature` and delivered to the Agent via gRPC (`GetPendingCommands`).
3. **Verification**: The Agent daemon validates the signature against the trusted Heimdall Server Public Key before executing the payload. If signature verification fails, the command is rejected, and a `Critical` security event is logged in `AgentEvent`.

---

## 4. Audit Trail & Log Retention Policy

- **Event Storage**: Security events are recorded in `backend.agent_events` with fields `Source`, `Message`, `Level`, `ClientPcId`, and `Timestamp`.
- **Severity Levels**:
  - `Information`: Routine telemetry, normal heartbeat status.
  - `Warning`: Resource usage limit exceeded, disk space low.
  - `Error`: Command processing failure, driver binding mismatch.
  - `Critical`: Cryptographic signature failure, unauthorized access attempt, unexpected agent disconnect.
- **Retention**: Log records are retained for a minimum of 12 months to satisfy TISAX audit requirements.

