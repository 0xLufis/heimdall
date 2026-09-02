# Heimdall Auth & Admin System

Technical documentation for the authentication and administrative management layer of Project Heimdall.

## Overview
Project Heimdall uses **Better-Auth** for secure, multi-tenant authentication and a high-fidelity administrative dashboard inspired by Clerk. It integrates seamlessly with the .NET 9 backend via a shared PostgreSQL database.

## Tech Stack
- **Framework**: Nuxt 3 (TypeScript)
- **Auth Engine**: Better-Auth V1
- **Database**: PostgreSQL 17.9
- **Database Driver**: `node-postgres` (pg)
- **UI Components**: Tailwind CSS & Shadcn-inspired custom components
- **SSO Providers**: Google, GitHub, Microsoft (Personal & Entra ID)

## Administrative Capabilities
The system provides a standalone dashboard for system administrators (`system_admin` role) with the following features:

### 1. User Management
- **Lifecycle Control**: Search, filter, and list all system users.
- **Account Security**: Ban/Unban users with mandatory reason logging.
- **Role Elevation**: Granular role assignment (Engineer, Manager, Technician, etc.).
- **Impersonation**: Securely assume a user's identity for troubleshooting.

### 2. Session Forensics
- **Real-time Tracking**: View all active device sessions for any user.
- **Remote Revocation**: Kill specific sessions (remote sign-out) to secure compromised accounts.
- **Device Metadata**: Detection of OS and IP address for session identification.

### 3. Organization Engine
- **Multi-Tenancy**: Create and manage organizations (teams/departments).
- **Member Grouping**: Group users within organizations with specific local roles.
- **Slug Management**: Clean URL identifiers for organizational contexts.

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
