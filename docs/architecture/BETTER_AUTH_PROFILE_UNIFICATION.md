# Better-Auth Profile Unification & Universal User Enforcement

This document specifies the architectural model and runtime guarantees ensuring that **no user can exist or be referenced anywhere in Heimdall without a registered Better-Auth user profile** (`auth.user` / `App.Shared.Entities.AuthUser`).

---

## 1. Context & Architectural Challenge

Prior to unification, several subsystems across the Heimdall platform referenced user identifiers that originated outside the primary Better-Auth database:
- **Active Directory & Entra ID Security Group Mapping / Org Sync**: External directory user IDs (e.g. `usr-test-entra-001`) synced into Heimdall organizations caused database-level foreign key constraint failures (`member_user_id_user_id_fk`) in PostgreSQL when enrolled into `auth.member` before an `auth.user` row was provisioned.
- **Legacy OU Approval Mappings**: Default approver strings (`it_admin`) or hardcoded mock references (`usr-sally-01`) lacked formal user profiles.
- **Maintenance Ticketing**: Tickets and comments referenced author, reporter, and assigned technician IDs (`usr-op-01`..`06`, `usr-tech-01`..`04`, `usr-admin`) that were held only in client-side stores or mock arrays.
- **Technician Routing & Candidate Management**: Routing rules and out-of-office delegations referenced candidate technician IDs (`tech-01`..`05`, `tech-alice`, `usr-sally`, `usr-kovacs`, etc.) that were not guaranteed to exist in the database.
- **Dev & Outside IT Automation Bots**: API-key-authenticated service bots (`outside-it-automation-bot`, `usr-automation-bot`) and development bypass principals (`dev-admin-id`) operated without guaranteed database records.

---

## 2. Core Defense-in-Depth Model

To eliminate orphaned references and foreign key violations while preserving system resilience in air-gapped and hybrid environments, Heimdall applies a multi-layered defense:

```
[ Incoming Action / Event ]
  (Entra SSO / Ticketing / OU Approvals / Technician Rules / API Bot)
           │
           ▼
[ Profile Resolution: resolveUserProfile(id, hints) ]
  Checks in-memory catalog -> checks plant dataset -> synthesizes standard profile
           │
           ▼
[ Auto-Provisioning: ensureBetterAuthProfile(id) ]
  Queries `auth.user` (Drizzle / EF Core)
  ├── If exists: proceeds
  └── If missing: atomic INSERT into `auth.user` with verified profile
           │
           ▼
[ Target Operation Executes Cleanly ]
  (auth.member enrollment, ticket creation, rule assignment, audit logging)
```

---

## 3. Implementation Details

### 3.1 Central Master Registry & Provisioning Engine
- **Module**: `frontend/web/server/utils/authProfiles.ts`
- **Master Profile Catalog (`MASTER_SYSTEM_USER_PROFILES`)**:
  - **14 Demo Personas**: `usr-sysadmin-1`, `usr-director-1`, `usr-plant-eng-1`, `usr-senior-eng-1`, `usr-heimdall-admin-1`, `usr-itadmin-1`, `usr-itsiteadmin-1`, `usr-engadmin-1`, `usr-op-planner-1`, `usr-gl-assy-1`, `usr-ferenc`, `usr-eng-robotics-1`, `usr-tech-welding-1`, `usr-manager-andras`.
  - **11 Ticketing Actors**: `usr-op-01` through `usr-op-06`, `usr-tech-01` through `usr-tech-04`, `usr-admin`.
  - **14 Technician Candidates & Dedication Users**: `tech-01` through `tech-05`, `tech-alice`, `tech-sally`, `tech-orwell`, `tech-kovacs`, `usr-sally`, `usr-orwell`, `usr-katalin`, `usr-varga`, `usr-nemeth`, `usr-horvath`, `usr-pap`.
  - **4 Automation & Dev Identities**: `usr-automation-bot`, `outside-it-automation-bot`, `usr-autonomous-agent`, `dev-admin-id`.
- **Functions**:
  - `resolveUserProfile(userId, hints)`: Resolves full name, email, role, username, and department from master profiles, synthetic plant dataset, or fallback synthesis.
  - `ensureBetterAuthProfile(userId, hints)`: Asynchronous DB assertion that checks `auth.user` and inserts the user record if not present.
  - `ensureAllBetterAuthProfiles()`: Bulk-syncs all catalogued identities into `auth.user` on Nitro boot (`ensureAdminUser.ts`) and dev seed endpoints.
  - `getAllKnownUserProfiles()`: Exposes a consolidated directory of active identities for UI pickers, user cards, and routing delegation selectors.

### 3.2 Directory Sync & Foreign Key Protection
- **Module**: `frontend/web/server/utils/securityGroupOrgSync.ts`
- Before inserting any mapping entry into `auth.member`, `syncUserSecurityGroupsToOrganizations` awaits `ensureBetterAuthProfile(userId, { role: targetOrg.role })`.
- Active Directory OU governance (`activeDirectoryStore.ts` and `api/activedirectory/ous/approve.post.ts`) ensures that `approvedBy` and `requestedBy` IDs resolve to real Better-Auth profiles.

### 3.3 Maintenance Ticketing Hardening
- **Frontend BFF**: `frontend/web/server/utils/ticketsStore.ts`, `api/tickets/index.post.ts`, `api/tickets/[id]/comments.post.ts`
- **Backend API**: `backend/App.Backend.Api/Controllers/V1/MaintenanceTicketController.cs`
- In both BFF and .NET backend, creating a ticket or comment checks the database `AuthUsers` table for `CreatedBy`/`reportedByUserId`, `AssignedTo`/`assignedTechnicianId`, and comment `authorUserId`, auto-provisioning compliant profiles if absent.

### 3.4 Technician Candidates & Assignment Rules
- **Frontend BFF**: `frontend/web/server/utils/technicianRulesStore.ts`
- **Backend API**: `backend/App.Backend.Api/Controllers/V1/TechnicianController.cs`
- Creating rules or absence delegations ensures `technicianId` and `backupTechnicianId` profiles in `auth.user`.
- If candidates are queried on an unseeded environment, the backend automatically seeds the candidate pool into `db.AuthUsers`.

### 3.5 Pipeline & Database Initialization
- **Seed Pipeline**: `seed_data/seed_pipeline.py`
- Incorporates `ADDITIONAL_SYSTEM_USERS` to generate transactional `INSERT ... ON CONFLICT DO UPDATE` statements in `seed_data/incremental_seed.sql`.
- Applied seed yields **107 users** in `auth.user` (60 synthetic factory users + 47 system/persona/technician identities) and **196 organization memberships** in `auth.member`.

---

## 4. Verification & Quality Gates

All assertions and database integrity rules are validated across both frontend and backend suites:
1. **.NET Backend Suite (`dotnet test`)**: 208/208 tests passed (0 failures).
2. **Frontend & BFF Suite (`npx vitest run`)**: 41/41 test files passed, 332/332 tests passed (0 failures).
3. **Database Constraints**: Zero `member_user_id_user_id_fk` errors during full Entra SSO integration runs (`AzureSsoAndEntraIntegration.test.ts`).
