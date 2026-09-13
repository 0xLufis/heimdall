# Identity & Computer Management Decoupling Analysis

This document assesses the operational dependency on Microsoft Active Directory (AD) across Heimdall, evaluates alternative endpoint management systems for OT/ICS environments, and specifies decoupled interface contracts for non-AD deployments.

---

## 1. Executive Summary & AD Dependency Footprint

In industrial automation environments, Operational Technology (OT) networks frequently operate with air-gapped network segments, workgroup-based Industrial PCs (IPCs), or alternative Linux/BSD-based soft-PLC controllers where a central Microsoft Active Directory domain controller is absent, unreliable, or politically segregated between corporate IT and factory OT.

### Current AD Touchpoints in Heimdall:
| Component | Functionality | Criticality | Fallback / Standalone Capability |
| :--- | :--- | :--- | :--- |
| **Authentication** | User login and session management | **Low (Non-blocking)** | Better-Auth maintains local accounts (`auth.user`), hashed passwords, and database sessions. AD/Entra ID is used only for external claims. |
| **RBAC Governance** | Organization provisioning and role mapping | **Medium** | Manual role assignment via `/dashboard/users` and `/dashboard/organizations` operates without directory sync. |
| **Host Discovery** | Scanning VLAN OUs for candidate IPC hostnames | **Low** | Edge agents self-register via gRPC (`ReportSystemInfo`). Manual host registration and CSV/JSON batch imports are supported. |
| **mTLS Enrollment** | Mapping OU paths to X.509 client certificate profiles | **Low** | Certificate authority allows direct issuance, manual profile binding, and machine-identifier-based issuance without OU paths. |

> [!NOTE]
> **Zero Downtime Impact on Day-to-Day Operations:** If the Active Directory domain controller goes offline, Heimdall continues uninterrupted. Telemetry ingestion, real-time SignalR notifications, maintenance ticket lifecycles, CAD spatial navigation, and local user sessions require **zero real-time connectivity** to Active Directory.

---

## 2. Decoupled User Management Interface (`IIdentityDirectoryService`)

To enable seamless plug-and-play authentication across environments (pure local database, LDAP/OpenLDAP, FreeIPA, Keycloak, or Entra ID), the platform abstracts directory operations into an interfaced provider contract:

```csharp
namespace App.Backend.Api.Security;

public interface IIdentityDirectoryService
{
    Task<DirectoryUser?> GetUserByUsernameAsync(string username);
    Task<DirectoryUser?> GetUserByIdAsync(string userId);
    Task<IEnumerable<DirectoryGroup>> GetUserGroupsAsync(string userId);
    Task<IEnumerable<DirectoryGroup>> SearchGroupsAsync(string query);
    Task<bool> ValidateCredentialsAsync(string username, string password);
    Task<DirectorySyncResult> SyncDirectoryUsersAsync(CancellationToken cancellationToken = default);
}

public record DirectoryUser(
    string Id,
    string Username,
    string Email,
    string DisplayName,
    string? Department,
    string? Title,
    IEnumerable<string> MemberGroupIds
);

public record DirectoryGroup(
    string Id,
    string DisplayName,
    string? DistinguishedName,
    string? Description
);

public record DirectorySyncResult(
    int UsersScanned,
    int UsersAdded,
    int UsersUpdated,
    int GroupsMatched
);
```

### Supported Provider Implementations:
1. **`DatabaseIdentityService` (Default / Standalone)**:
   - Queries Better-Auth database tables directly (`auth.user`, `auth.account`, `auth.member`).
   - Requires zero external network connectivity.
   - Ideal for standalone test environments, air-gapped manufacturing plants, and OEM deployments.
2. **`LdapOpenDirectoryService` (RFC 4511 / FreeIPA / Samba)**:
   - Uses standard cross-platform `System.DirectoryServices.Protocols` over port 389/636 (LDAPS).
   - Compatible with Linux FreeIPA, OpenLDAP, Samba 4 AD DC, and 389 Directory Server.
3. **`EntraIdGraphIdentityService` (Cloud / Hybrid)**:
   - Connects to Microsoft Graph API `/v1.0/users` and `/v1.0/groups` using OAuth2 client credentials.

---

## 3. Alternative Computer Management Systems Evaluation

When client PCs are not joined to an Active Directory domain (e.g. Windows 10 IoT in WORKGROUP mode, Beckhoff TwinCAT/BSD FreeBSD, Ubuntu Core, or Debian Industrial), the following alternative discovery and fleet management strategies are supported:

### 3.1 Autonomous gRPC Self-Registration (Current Primary Mechanism)
- **Architecture**: The edge agent daemon (`App.Agent.Daemon`) connects outbound over HTTP/2 gRPC to port 5001.
- **Workflow**: On boot, the agent reports its hardware UUID, MAC address, hostname, OS version, and network configuration. If the node is not registered in `client_pcs`, the backend automatically provisions a candidate record.
- **Benefits**: Zero incoming port exposure on the edge PC; functions seamlessly through NAT firewalls and VLAN gateways.

### 3.2 Network Layer Discovery (Passive / Active ARP & mDNS)
- **Passive Subnet Scanners**: Probes local factory subnets (VLAN 10–60) via ICMP echo, ARP tables, and mDNS (`_heimdall-edge._tcp.local`).
- **PROFINET / EtherCAT Topology Parsing**: Queries TwinCAT ADS and industrial network master interfaces to enumerate slave nodes, bus couplers, and connected controllers.

### 3.3 Configuration Management & CMDB Integration
- **Ansible / SaltStack Dynamic Inventory**: Exporting and importing node inventories via REST API (`/api/v1/ClientPc/batch-register`).
- **OEM Provisioning USB / Token**: Inserting a pre-configured `agent.json` with machine token on installation, bypassing all directory enrollment steps.

---

## 4. Migration & Resilience Roadmap

1. **Active Directory Degraded Mode**: When AD LDAP search fails, the UI surfaces a non-blocking warning badge while falling back to cached local users and registered edge hosts.
2. **Directory Provider Selection**: Configure identity provider in `appsettings.json` via `Identity:Provider` (`"Database"`, `"ActiveDirectory"`, `"OpenLDAP"`, `"EntraID"`).
3. **Host Onboarding Without AD**: Allow operators to register client PCs directly via the Web UI (`/dashboard/clients` -> "Register Manual IPC") or via mobile camera QR scanning on machine tags.
