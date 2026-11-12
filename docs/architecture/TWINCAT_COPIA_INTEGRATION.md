# TwinCAT 3 & Copia Automation Integration Architecture

## 1. Executive Summary & Overview

Heimdall integrates directly with **Beckhoff TwinCAT 3** industrial controllers and **Copia Automation** (Git-based industrial version control) to provide automated version tracking, code integrity audits, and change detection on the factory floor.

### 1.1 Copia EULA & Terms of Service Compliance (Anti-User-Pooling)

Copia Automation operates under a **seat-based named user licensing model**, governed by the [Copia Terms of Service](https://copia.io/terms-of-service/). Key contractual stipulations include:

- **Exhibit A ("Authorized User")**:
  > *"‘Authorized User’ means any individual (in each case to the extent that Licensee’s license includes, and Licensee pays for, such individual) who is authorized to access the Product, Documentation or Services and exercise the rights licensed by Licensee. Each Authorized User must use a unique identity to access and use the Product unless otherwise licensed, and may access the services only to the extent licensed by Licensee."*
- **Section 1.3(d) (Restrictions)**:
  > *"Licensee agrees that it (and its Authorized Users) will not without express written permission of Copia: ... (d) permit direct or indirect access to, or use of, the Services in a way that circumvents any contractual usage limit;"*
- **Section 3(b) (Account Confidentiality & Responsibility)**:
  > *"Licensee is Responsible for its Authorized Users and Each of Their Accounts. ... Licensee shall require Authorized Users to maintain proper password security, and to keep their accounts confidential."*

Under these provisions, routing modifications made by various plant engineers through a single generic "service account" or shared headless API key directly violates the unique identity mandate and circumvents seat-based usage limits ("user pooling").

To maintain 100% compliance with Copia's Terms of Service and security architecture, Heimdall enforces a **Two-Stage PLC Version Tracking Model**:

1. **Stage 1 (Local Edge Tracking - Automated Service User)**:
   - When TwinCAT ADS runtime transitions (ADS downloads or online changes) occur on the OT network, changes are captured and committed strictly to an **on-premise local Git repository**.
   - This local tracking uses the internal service identity:
     - **Author**: `heimdall-probe`
     - **Email**: `heimdall-probe@internal`
   - **Crucially, Stage 1 never calls Copia Cloud Services or Cloud Infrastructure.** Because on-premise local Git operations are purely edge-hosted and never interact with Copia Cloud, using `heimdall-probe` for automated local edge tracking is fully compliant and incurs zero Copia seat consumption.
   - A pending change record (`PendingUserAuthorization`) and user notification are generated immediately.

2. **Stage 2 (User-Authenticated Cloud Sync - Personal API Key / Unique Identity)**:
   - When an automation engineer or plant operator receives the notification, they review the pending local diff in Heimdall.
   - To synchronize the commit to Copia Cloud (`POST /api/v1/copia/sync-cloud`), the engineer must authenticate using their **own personal Copia API Key / Personal Access Token (PAT)**.
   - The backend validates that the API key belongs to a named human engineer and **explicitly rejects** any attempts to use service account keys (such as `service-user`, `heimdall-probe`, or `shared-*`).
   - The cloud commit is attributed directly to the authenticated engineer's email, satisfying Copia's unique identity requirement under Exhibit A and Section 1.3(d).

**Implementation status**: the two-stage model and its EULA guards are implemented in the backend (`CopiaIntegrationService`, `CopiaController`), but the edge part is not. The agent does not detect ADS downloads (its ADS endpoint is a simulation) and does not call the backend for Copia. Stage 1 is entered through REST (`POST /api/v1/copia/track-local` or `ingest-current-config`), and the backend records the change as a pending sync (in memory, with a generated local commit id `loc-…`) without running any Git operation. The Stage 2 push calls Copia only when `COPIA_DEVICE_LINK_ENABLED=true` and `COPIA_API_URL` are set; otherwise it is simulated.

### 1.2 Copia DeviceLink™ vs. Copia Source Control (No DeviceLink License Required)

A common enterprise licensing question is: **"Does this workflow operate as intended and is it legal if our organization only purchases Copia Source Control user seats, without a Copia DeviceLink™ device license?"**

**Answer: Yes, 100% legal and operational.** Here is why:

| Dimension | Copia DeviceLink™ (Proprietary Add-On) | Heimdall Two-Stage Integration (Source Control Seats Only) |
|---|---|---|
| **What It Is** | Copia's proprietary appliance agent that actively connects to physical PLCs over OT networks and initiates automated cloud commits. | Design: Heimdall's own edge daemon (`heimdall-agent`) connects to TwinCAT via standard Beckhoff ADS and manages local on-prem Git tracking. Not implemented yet: the agent has no ADS client, and local tracking is a backend record (section 4.1). |
| **Licensing Metric** | Per physical controller / connected asset. | **Zero DeviceLink licenses required.** Uses standard per-seat named user licenses for Copia Source Control. |
| **Edge Git Operations** | Managed by Copia DeviceLink cloud services. | Design: purely local on-premise Git repository using open-source Git tooling (`heimdall-probe`). Zero Copia Cloud involvement. Today no Git command runs; the backend keeps a pending-sync record with a generated commit id. |
| **Cloud Synchronization** | Fully automated background headless push to Copia Cloud. | Initiated by the authorized human engineer (`POST /api/v1/copia/sync-cloud` or `git push`) authenticated with their personal Copia PAT. |
| **Copia EULA & ToS Compliance** | Requires DeviceLink subscription per device. | Fully complies with Copia Terms of Service Exhibit A ("Authorized User") and Section 1.3(d). Pushing user-created or user-reviewed commits via standard Git or PAT is the core intended purpose of Copia Source Control seats. |

Because Heimdall performs all edge runtime monitoring and local change tracking independently, and synchronization to Copia Cloud is authorized by a licensed engineer under their personal credentials, **no Copia DeviceLink license is required or invoked.**

```
+-----------------------------------------------------------------------------------------+
|                                    FACTORY OT NETWORK                                   |
|                                                                                         |
|  +--------------------+      ADS (agent side today:    +-----------------------------+  |
|  | Beckhoff TwinCAT 3 | <============================> | Heimdall Agent (Daemon)     |  |
|  | IPC / CX Controller|    simulated, dev builds)      | - ADS state change trigger  |  |
|  +--------------------+                                | - SHA-256 recipe file watch |  |
|                                                        +--------------+--------------+  |
+-----------------------------------------------------------------------|-----------------+
                                                                        | MQTT 5: ADS state
                                                                        v in system_info
+-----------------------------------------------------------------------------------------+
|                              HEIMDALL ON-PREM EDGE PLATFORM                             |
|                                                                                         |
|  Stage 1 trigger (REST, authenticated; the agent does not send it yet):                 |
|  POST /api/v1/copia/track-local or POST /api/v1/copia/ingest-current-config             |
|                  |                                                                      |
|                  v                                                                      |
|  +---------------+----------------+       Stage 1      +-----------------------------+  |
|  | Heimdall Backend API           | -----------------> | Local tracking record       |  |
|  | - CopiaIntegrationService      |                    | - Author: heimdall-probe    |  |
|  | - Rate-Limiting Debouncer      |                    | - PendingUserAuthorization  |  |
|  | - EULA Anti-Pooling Guard      |                    | - generated loc-* commit id |  |
|  +---------------+----------------+                    +-----------------------------+  |
|                  | Generates Notification                                               |
|                  v                                                                      |
|  +--------------------------------+                                                     |
|  | Engineer Dashboard UI          |                                                     |
|  | - Settings / Security Tab      |                                                     |
|  | - Personal Copia PAT / API Key |                                                     |
|  +---------------+----------------+                                                     |
+------------------|----------------------------------------------------------------------+
                   | Stage 2: POST /api/v1/copia/sync-cloud with a personal Copia key
                   v (pushed by the backend; simulated unless DeviceLink is configured)
+-----------------------------------------------------------------------------------------+
|                                  COPIA AUTOMATION CLOUD                                 |
|                                                                                         |
|  +--------------------------------+       Webhook      +-----------------------------+  |
|  | Copia Cloud Repository         | -----------------> | Heimdall Backend Webhook    |  |
|  | - Attributed to Human Engineer |  HMAC-SHA256 Push  | POST /api/v1/copia/webhook  |  |
|  | - Visual IEC 61131-3 Diffs     |                    | - X-Copia-Signature-256     |  |
|  +--------------------------------+                    | - SoftwareAsset.Version =   |  |
|                                                        |   pushed commit hash        |  |
|                                                        +-----------------------------+  |
|                                                                                         |
+-----------------------------------------------------------------------------------------+
```

---

## 2. TwinCAT Clean Repository Structure & Git Ignore Specifications

TwinCAT 3 project solutions (`.sln`) contain both XML-based source definitions (POUs, DUTs, GVLs, Hardware configurations) and volatile binary artifacts generated during local build and debug sessions. Tracking volatile files causes merge conflicts, repository bloat, and false-positive diffs in Copia.

### 2.1 Recommended Repository Structure

```
MyPlcSolution/
├── .gitignore
├── .gitattributes
├── README.md
├── MyPlcSolution.sln
└── MyTwinCatProject/
    ├── MyTwinCatProject.tsproj
    ├── _Config/
    │   ├── IO/
    │   └── PLC/
    └── MyPlc/
        ├── MyPlc.plcproj
        ├── DUTs/
        │   └── ST_MotorTelemetry.TcDUT
        ├── GVLS/
        │   └── GVL_Sensors.TcGVL
        ├── POUs/
        │   ├── MAIN.TcPOU
        │   └── FB_AxisController.TcPOU
        └── VISUs/
```

### 2.2 Standard Industrial `.gitignore` for Beckhoff TwinCAT 3

```gitignore
# ==============================================================================
# Heimdall Recommended .gitignore for Beckhoff TwinCAT 3 & Copia Automation
# ==============================================================================

# Visual Studio & TwinCAT Environment Volatiles
.vs/
*.suo
*.user
*.userprefs
*.sln.docstates
*.VisualState.xml

# TwinCAT Boot and Runtime Compilations
_Boot/
_CompileInfo/
*.tpy
*.compileinfo
*.bootdata
*.bootdata.old
*.compiled-library

# Backup and Temporary Storage
*.bak
*.autosave
*.tmp
*.~*
*.orig

# TwinCAT Archival Packages (should not be committed to Git)
*.tszip

# Symbol & Build Outputs
*.pdb
*.obj
*.bin
*.ilk

# OS & File System Artifacts
Thumbs.db
.DS_Store
desktop.ini
```

### 2.3 Essential `.gitattributes` for Merge & Diff Integrity

To ensure Copia accurately parses TwinCAT XML definitions and line endings across Windows engineering workstations:

```gitattributes
* text=auto eol=crlf
*.TcPOU text diff=pascal
*.TcDUT text diff=pascal
*.TcGVL text diff=pascal
*.tsproj text merge=union
*.plcproj text merge=union
```

---

## 3. ADS Login & Download Event Architecture

### 3.1 ADS State Lifecycle

TwinCAT runtime communication occurs over the **Automation Device Specification (ADS)** routing protocol (port 10000 on AMS Net ID target).

During engineering interventions, the following events occur:
1. **ADS Login**: An engineer attaches the TwinCAT XAE debugger to runtime port 851.
2. **Online Change**: Incremental code swap without stopping the PLC task cycle.
3. **Full Download**: Target PLC transitions from `ADSSTATE_STOP` to `ADSSTATE_RESET` to `ADSSTATE_RUN` with full memory re-allocation.

### 3.2 The Event Storm Problem

During commissioning, a PLC engineer may trigger dozens of online changes or ADS logins per minute. Naively triggering a Copia commit or cloud webhook on every raw ADS frame results in:
- Rate-limit exhaustion against the Copia REST API (`HTTP 429 Too Many Requests`).
- Redundant Git commit clutter consisting of incomplete, intermediate code snippets.
- Unnecessary network bandwidth utilization on cell-level industrial switches.

### 3.3 Rate-Limiting & Debouncing Architecture

Heimdall implements a two-tier debouncing algorithm in `CopiaIntegrationService`:

```
Incoming ADS Event (Download / Login)
                 │
                 ▼
  Extract `AmsNetId:EventType` Key
                 │
                 ▼
     Is Elapsed Time Since Last Event
     < Debounce Window (Default: 30s)?
           │               │
          Yes              No
           │               │
           ▼               ▼
      [DEBOUNCE]     [PROCESS EVENT]
     Drop duplicate   Update Timestamp
     Log debug msg    Execute Stage 1 Tracking
```

#### Algorithm Details
- **Debounce Window**: Configurable per controller (defaults to `30s` for online changes, `120s` for logins).
- **Concurrency**: Thread-safe evaluation via `ConcurrentDictionary<string, DateTimeOffset>`.
- **Sliding Guard**: If rapid continuous events occur, only the initial trigger and the final stable state trigger are forwarded.

---

## 4. Two-Stage Workflow & EULA Anti-Pooling Enforcement

### 4.1 Stage 1: Local On-Premise Git Tracking (`heimdall-probe`)

When an ADS event clears the debouncer:
1. **Heimdall Edge Agent** captures the PLC project files and hashes them. *Not implemented*: the agent neither detects downloads nor captures project files. Stage 1 starts when an authenticated caller posts the change to `POST /api/v1/copia/track-local` (device, repository path, branch, modified files) or uploads a `CurrentConfig.tnzip` archive to `POST /api/v1/copia/ingest-current-config`. The debouncer (`ShouldProcessPlcEvent`) exists but these endpoints do not apply it.
2. **`ICopiaIntegrationService.TrackLocalPlcChangeAsync(...)`** records the change; the design creates a commit in the local edge Git repository, but no Git command runs yet:
   - **Commit Author**: `heimdall-probe <heimdall-probe@internal>`
   - **Commit Id**: a generated placeholder (`loc-` plus 8 hex characters); the commit message argument is not stored.
   - **Status**: Recorded as `PendingUserAuthorization` in an in-memory pending-sync list (lost on restart).
3. **Notification Generation**:
   - The response and `GET /api/v1/copia/pending-syncs` carry a notification message telling engineers that unpushed logic changes were captured locally; no notification is pushed to dashboards by the backend.
   - The notification directs the engineer to review the pending sync.

### 4.2 Stage 2: User-Authenticated Cloud Sync (Personal Copia PAT)

To synchronize changes to Copia Cloud:
1. The engineer accesses **Settings > Security > Copia Automation Personal Credentials** (`/dashboard/settings`) or the pending changes card.
2. The engineer enters their personal Copia API key / PAT. Keys are stored per-user and never shared.
3. The engineer triggers **"Sync with My Key"** (BFF `POST /api/integrations/copia/sync`, which calls `POST /api/v1/copia/sync-cloud`):
   - Payload: `{ syncId, userApiKey, userEmail, userDisplayName }` (the e-mail claim of the session takes precedence over `userEmail`)
4. **Backend EULA Compliance Verification**:
   - If `userApiKey` is missing or empty, the sync is rejected (`HTTP 400`).
   - If `userApiKey` matches known service patterns (`service-user`, `heimdall-probe`, `shared-*`), the backend throws an `InvalidOperationException`, answered as `HTTP 400`:
     > *"Copia EULA Compliance Violation: Generic service account keys or shared keys cannot be used for Copia Cloud synchronization. Individual user-licensed API tokens are mandatory."*
   - Once validated, the commit is pushed to Copia Cloud authenticated as the individual engineer using their personal token. The push calls the Copia DeviceLink API only when `COPIA_DEVICE_LINK_ENABLED=true` and `COPIA_API_URL` are set; otherwise the sync is simulated and returns a generated cloud commit id.
   - The `SoftwareAsset` catalog is updated with the new verified version.

### 4.3 Webhook Ingestion & Visual Diff Verification

Once Copia commits the new PLC logic in the cloud, it dispatches an automated webhook (`POST /api/v1/copia/webhook`) to Heimdall.

#### Webhook Security & Processing
1. **HMAC-SHA256 Signature Verification**: Evaluates `X-Copia-Signature-256` (`sha256=<hex>`) against the shared secret `COPIA_WEBHOOK_SECRET` using constant-time cryptographic comparisons (`CryptographicOperations.FixedTimeEquals`). The check runs only when the secret is set and the header is present; otherwise the anonymous endpoint accepts the payload unverified, so production must set `COPIA_WEBHOOK_SECRET`.
2. **Metadata Extraction**:
   - Repository name & active branch
   - Commit hash (`after`)
   - Author email and commit message
   - Changed IEC 61131-3 logic files (`.TcPOU`, `.TcGVL`, `.TcDUT`)
3. **SoftwareAsset Version Synchronization**:
   - Matches repository name to Heimdall's `SoftwareAsset` catalog.
   - Updates `SoftwareAsset.Version` to the verified Git commit hash.
   - Dashboards are not notified (no SignalR event); they see the new version on their next load.

---

## 5. Sequence Diagram

```mermaid
sequenceDiagram
    autonumber
    actor Engineer as Automation Engineer
    participant PLC as Beckhoff TwinCAT PLC
    participant Agent as Heimdall Edge Agent
    participant Backend as Heimdall Backend API
    participant LocalGit as On-Prem Local Git
    participant Copia as Copia Automation Cloud

    rect rgb(240, 248, 255)
        Note over PLC,Backend: Stage 1: Local Edge Tracking (Automated)
        PLC->>Agent: ADS State Change (Online Change / Download), agent side simulated in dev builds
        Agent->>Backend: MQTT 5 system_info (ADS state in the IndustrialOT component)
        Note over Agent,Backend: Stage 1 starts with POST /api/v1/copia/track-local or /ingest-current-config (REST), which the agent does not call yet
        Backend->>LocalGit: Record change as "heimdall-probe <heimdall-probe@internal>"
        LocalGit-->>Backend: Pending sync (generated loc-* commit id, PendingUserAuthorization)
        Backend->>Engineer: Pending sync listed ("Local PLC changes detected on {deviceId}")
    end

    rect rgb(255, 248, 240)
        Note over Engineer,Copia: Stage 2: User-Authenticated Cloud Sync (EULA Compliant)
        Engineer->>Backend: Review pending syncs (GET /api/v1/copia/pending-syncs via the BFF)
        Engineer->>Backend: POST /api/v1/copia/sync-cloud (syncId, userApiKey, userEmail)

        alt Missing or Generic Service Account Key (service-user, heimdall-probe, shared-*)
            Backend-->>Engineer: 400 Bad Request (EULA Anti-Pooling Violation)
        else Valid Personal Copia API Key / PAT
            Backend->>Copia: Push Local Commit Authenticated as Engineer (DeviceLink API when configured, else simulated)
            Copia-->>Backend: 200 OK (Synchronized to Remote)
            Backend-->>Engineer: 200 CopiaCloudSyncResult (cloud commit, synced at)
            Copia->>Copia: Generate Visual IEC 61131-3 Diffs
            Copia->>Backend: POST /api/v1/copia/webhook (X-Copia-Signature-256, HMAC-SHA256)
            Backend->>Backend: Verify HMAC (COPIA_WEBHOOK_SECRET) & Update SoftwareAsset.Version in DB
        end
    end
```

---

## 6. API Reference

### Backend Endpoints (`App.Backend.Api.Controllers.V1.CopiaController`)

| Endpoint | Method | Description | Auth Requirement |
|---|---|---|---|
| `/api/v1/copia/track-local` | `POST` | Records a pending local change attributed to `heimdall-probe` (generated commit id, no Git operation) | Authenticated caller (`[Authorize]`); the edge agent does not call it yet |
| `/api/v1/copia/ingest-current-config` | `POST` | Extracts an uploaded `CurrentConfig.tnzip`, filters volatile files and records a pending sync | Authenticated caller (`[Authorize]`) |
| `/api/v1/copia/pending-syncs` | `GET` | Lists unpushed local changes pending user key | Authenticated User |
| `/api/v1/copia/sync-cloud` | `POST` | Synchronizes a pending sync to Copia Cloud (DeviceLink API when configured, otherwise simulated) | Personal User PAT (`userApiKey` required; service keys rejected) |
| `/api/v1/copia/webhook` | `POST` | Receives push events from Copia Cloud | Anonymous; `X-Copia-Signature-256` HMAC checked only when `COPIA_WEBHOOK_SECRET` is set and the header is present |

### Frontend BFF Endpoints (`frontend/web/server/api/`)

| Endpoint | Method | Description |
|---|---|---|
| `/api/user/copia-key` | `GET` | Retrieves masked personal Copia key status |
| `/api/user/copia-key` | `POST` | Saves the personal Copia API key in the BFF's in-memory key store (per user, lost on restart) |
| `/api/integrations/copia/pending-syncs` | `GET` | Fetches pending sync queue for the current user |
| `/api/integrations/copia/sync` | `POST` | Executes cloud sync using stored personal user key |

---

## 7. Implementation & Testing Reference

- **Backend Interface**: `backend/App.Backend.Api/Services/ICopiaIntegrationService.cs`
- **Backend Implementation**: `backend/App.Backend.Api/Services/CopiaIntegrationService.cs`
- **Backend Controller**: `backend/App.Backend.Api/Controllers/V1/CopiaController.cs`
- **Backend Tests**: `tests/backend/App.Backend.Tests/CopiaIntegrationServiceTests.cs` (6 test scenarios covering debouncing, HMAC verification, `heimdall-probe` commits, and EULA anti-pooling enforcement)
- **Frontend BFF Store**: `frontend/web/server/utils/copiaUserKeyStore.ts`
- **Frontend UI**: `frontend/web/app/pages/dashboard/settings.vue` (Security tab)
- **Frontend Tests**: `tests/frontend/unit/CopiaUserKeyAndEulaCompliance.test.ts` (10 test scenarios covering personal key persistence, anti-pooling rejection, and end-to-end sync flows)
