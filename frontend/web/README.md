# Heimdall Web Frontend & Auth Administration

Technical documentation for the web frontend, authentication, and administrative management layer of Heimdall.

## Overview
The Heimdall frontend provides operational dashboards, spatial CAD floor plans, live maintenance ticketing, and multi-tenant user administration. It uses **Better-Auth** for authentication and session forensics, integrating directly with the backend API via Nitro BFF server routes.

## Core Capabilities
- **Operational Dashboards**: Live telemetry gauges, client PC status tables, and KPI summaries.
- **Interactive Plant Map**: Vector-based AutoCAD DXF floor plan viewer with spatial machine pinning.
- **Maintenance Ticketing**: Real-time 8-stage Kanban board (`Open`, `In_Progress`, `Pending_Parts`, `Escalated`, `Escalated_External`, `Closure_Pending`, `Resolved`, `Closed_Unresolved`) with drag-and-drop, SFC tracking, and error template interpolation.
- **Composable Action QR Generator**: Pure TypeScript zero-dependency SVG QR code generator (`qrSvgRenderer.ts`) for machine incident reporting and direct camera scanning.
- **Dynamic Asset Editor**: 5-tab editor with customizable specs and template interpolation.
- **Identity & Multi-Tenancy**: Organization switching, session forensics, and role-based access control.

## Administrative Capabilities
The system provides standalone governance dashboards for system administrators and engineers:

### 1. User Management
- **Lifecycle Control**: Search, filter, and list all system users.
- **Account Security**: Ban/Unban users with mandatory reason logging.
- **Role Elevation**: Granular role assignment (Engineer, Manager, Technician, etc.).
- **Impersonation**: Securely assume a user's identity for troubleshooting.

### 2. Session Forensics & MFA Policy Governance
- **Real-time Tracking**: View all active device sessions for any user.
- **Remote Revocation**: Kill specific sessions (remote sign-out) to secure compromised accounts.
- **MFA Policy Thresholds**: Configurable re-authentication intervals per security group / role (`SystemAdministrator` always, `Engineer` weekly, `Technician` monthly).
- **Interactive Session Evaluation**: Sandbox to verify whether an active session requires MFA challenge based on elapsed time.

### 3. Organization & Security Group Mapping (`/dashboard/security-groups`)
- **Directory Claims Transform**: Auto-provisions organizations and assigns roles based on Entra ID GUIDs and Active Directory Distinguished Names.
- **Interactive Claims Sandbox**: Test presets for key engineering personas (Sally Vance, George Orwell, Alex Novak, Root Admin).

### 4. Active Directory Discovery & PKI Root CA Governance (`/dashboard/admin/system-settings`)
- **VLAN Host Discovery**: Inspects factory OUs partitioned across VLANs 10–60.
- **Key-Value Import Templating**: Maps AD OU attributes (Location, Subnet, Machine Type, Purpose) directly to Heimdall host metadata.
- **Root CA & OU Certificates**: Assigns X.509 certificates to hosts by OU with self-signed or custom CA import.

## Database Schema (Auth Layer)
| Table | Description |
|-------|-------------|
| `user` | Core user profiles and administrative status. |
| `session` | Active authentication tokens and device metadata. |
| `account` | Links users to auth methods (Email/PW or OAuth). |
| `organization` | High-level groupings for multi-tenant support. |
| `member` | Links users to organizations. |
| `invitation` | Pending organizational join requests. |

## Role Hierarchy & Permissions
Heimdall uses a resource-based Access Control (AC) system:
- **system_admin**: Full CRUD on Users, Orgs, Sessions, and Invitations. Can impersonate anyone.
- **admin**: Regional admin with full org-level control.
- **manager**: Read-access to org data and basic user listing.
- **user**: Standard read-only access to assigned resources.

## UI Architecture
Components are modularized for maintainability:
- `DashboardUserTable`: High-fidelity management table with search/filters.
- `DashboardOrgCard`: Interactive visualization of organizational units.
- `authClient`: Centralized Better-Auth client with all plugins (Admin, Org, Multi-Session).

## Development & Seeding
To populate the system with mock data for testing:
1. Ensure `DATABASE_URL` is set in `.env`.
2. Visit `/api/dev/seed-admin` in your browser.
3. Login at `/auth/login` using `admin` / `admin`.

## Testing
The system includes automated tests for auth logic:
- `npm test`: Runs Vitest suite for auth utilities and API routes.
- `seed-admin`: Integration test for data consistency and role mapping.
