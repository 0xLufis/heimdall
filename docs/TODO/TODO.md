# Open items and decisions (2026-11-12)

Consolidated after the plant-simulation / hardening work on branch `feature/simulated-plant-and-hardening`. Each item names the file or endpoint where the work starts.

## Decisions taken (with sanity check)

| Decision | Implemented as | Sanity check |
|---|---|---|
| User directory is global | Every signed-in user lists and searches all users (`/api/users`, `GET /api/v1/auth/users`, search) | Fine inside one company; e-mail and role become visible to operators too (TISAX data minimisation: acceptable, note it in the ISMS). |
| Organisations are managed by Heimdall admins, middle managers and up, and anyone granted the right | `OrganizationManagement` policy: `system_admin`, `heimdall_admin`, `admin` manage all; `plant_director`, `engineering_admin`, `plant_engineering_manager`, `senior_engineering_manager`, `group_leader`, `manager` and holders of `organizations.manage` manage the organisations they belong to | Consistent in backend, BFF and Better-Auth. Open: should `plant_director` manage every organisation like a Heimdall admin? It currently cannot write any machine group (not in `EndpointConfigManagement`). |
| KPI Builder removed, Lua is the KPI tool | Builder deleted; "New KPI" wizard with five templates, API reference, station test runs | OK. A test run still executes write calls for real (warning shown); a true dry-run needs backend support. |
| Stale threshold below 60 s | Per station: stale after 3 missed samples (`clamp(3 x interval, 3 s, 5 min)`), freshness by arrival including unchanged blocks; OFFLINE on the agent's MQTT last will | 4 s sampling gives ~12 s with no extra traffic. A 5 s threshold needs sampling every <= 1.7 s (~2.4x MQTT and SignalR traffic). Caveat: freshness is measured when the backend processes a block, so an ingestion backlog makes old data look fresh (see Telemetry below). |
| Global search lists stations first | Stations, controllers, tickets, people, inventory; page-scoped searches keep their own entity first | OK. |
| Logs bounded by size and age, spooled to disk, verbosity configurable | File logger with rotation and a bounded spool; dev logs off the RAM-backed `/tmp` | See the logging section of `docs/guide/DEV_GUIDE.md`. |
| Inventory page on backend data | Thin BFF mapping onto `/api/v1/inventory/*` | OK; backend gaps listed below. |
| Plugins: user certificate for installing, double signature with machine certificates | User signs the package hash; the server countersigns with its computer certificate; agents verify both chains | Verification does not require a code-signing EKU, so ordinary AD user certificates work (Heimdall-issued user certificates carry codeSigning). Decide whether to require the EKU. Plugins reuse `EndpointConfigManagement`, which includes engineers. |
| Machine groups: scope writes, allow reads | `MachineGroup.OrganizationId`; writes by platform admins, members of the owning organisation, and `EngineeringAdministration` for platform-managed groups | OK. Lua scripts may only change platform-managed groups (they run without a user identity). |
| Ingestion capacity accepted for now ("enterprise can afford better hardware") | Unchanged: one MQTT message at a time | Interacts with freshness (see above); keep it on the list. |
| Kiosk: no auth in dev/demo, computer certificates in production | Dev bypass only with `HEIMDALL_ENABLE_DEV=true`, never under `NODE_ENV=production`; production via proxy-verified client certificate mapped to the controller's stations | `POST /api/v1/pki/workstation-identity` is anonymous and reveals station bindings for any valid certificate: restrict it to the BFF (service key) or internal networks. |
| Certificates from AD or Heimdall; Heimdall root self-signed or AD/CA-derived | One trust store (`trusted_ca_certificates`), subordinate CA via CSR, SAN conventions for users and computers | AD-issued revocation defaults to `NoCheck` (warning); use `Online` in production when CRLs are reachable. |

## Open items

### Telemetry and ingestion
- [ ] MQTT messages are processed one at a time (`MqttIngestionHostedService.OnMessageReceivedAsync`). At 100 stations / 4 s the debug backend uses ~75-100 % of one core and falls 20-50 s behind under load; keep only the newest pending block per station and process controllers in parallel.
- [ ] Record freshness at receive time once ingestion is decoupled (today a backlog makes old data look fresh).
- [ ] The station arrival map is per backend instance (`StationFreshness.cs`); other instances fall back to the snapshot and the 60 s rule.
- [ ] SignalR sends every station event to every dashboard of the organisation (~23 KiB/s per dashboard for 100 stations); subscribe per page or throttle per client.

### Edge agent and Windows
- [ ] Firewall: allow the Docker range to reach the host backend (`sudo ufw allow proto tcp from 172.16.0.0/12 to any port 1883,5001`), otherwise the Windows VM agent cannot connect.
- [ ] Windows-only paths not exercised on a real Windows service: STOP vs SHUTDOWN, event-log evidence, volatile-registry boot id. (Identity recovery and previous-run classification were seen working in the VM.)
- [ ] `RecordStopIntent` exists but the agent never requests its own restart; no lifecycle UI or SignalR event; the simulator does not emit lifecycle reports.
- [ ] Spool encryption (`SpoolEncryptionMode`) is not implemented; spool files are plaintext.
- [ ] `ControllerRepository.UpsertByMacAddressAsync` overwrites `LastOnline` unconditionally (a spool drain moves it backwards).
- [ ] `DiagnosticSnapshotController` only logs crash telemetry.
- [ ] Deployment: exclude the agent state directory from UWF write filters.
- [ ] `infra/windows/oem/setup.ps1` never copies `infra/windows/tray/` into the VM; the kiosk launcher (`Start-HeimdallTerminal.ps1`) is untested on Windows.
- [ ] Trust bundle is not distributed to agents automatically (`AgentConfig.TrustedCaCertificatesPem`); the agent config UI cannot set it.

### Security and identity
- [ ] Ticket and dashboard caches use global keys (`MaintenanceTicketController.cs`, `DashboardController.cs`); `CacheService.RemoveByPatternAsync` clears only process memory.
- [ ] SSO-simulation sessions are not real Better-Auth sessions: `/api/auth/admin/*` (role change, suspend, sessions) answers 401 under the simulated login; issue signed sessions instead.
- [ ] `useAuthSession.ts` decides admin flags with regexes that differ from the backend policies.
- [ ] Pseudo-IT-admin delegation: UI default on, enforcement default off; `allowEngineeringAdminUserCreation` is enforced nowhere.
- [ ] Identity Studio lets `it_admin` write roles outside the admin guard (`studio.config.ts`).
- [ ] The rank rule allows lateral grants (an engineering admin can grant `it_admin`).
- [ ] Frontend unit tests write to the dev database (`useDb` default) - e.g. `usr-test-entra-001`.
- [ ] Smaller: `technicianRulesStore.ts` trusts a client `callerRole`; `DelegationController` falls back to a client organisation; `activeDirectoryStore.ts` provisions approvers as `it_admin`; `SignIn.vue` logs the sign-in response.
- [ ] Security-group mappings and automation keys live in Nitro memory although the backend has tables.
- [ ] PKI: no admin UI for the trust store or the subordinate CA; no CRL / OCSP published by Heimdall; plugins are stored in backend memory.

### Analytics, inventory, machine groups
- [ ] Alert rules are evaluated nowhere (rules in browser memory); MTBF needs machine runtime counters.
- [ ] Inventory backend gaps: atomic stock decrement, usage ledger / history, using serialized parts, lifecycle condition, `PUT` cannot change minimum stock or manufacturer, no timestamps. Templates, spare-part policies and machine import still use the fixture store.
- [ ] Machine groups UI: only three levels render (sub-lines invisible), machine types list does not match real types, delete without confirmation, no UI for group type / members / owning organisation; `UpdateAsync` ignores `GroupType`; `machineGroupsStore.ts` is dead code.
- [ ] Tickets: comments and service logs have no read endpoints; the BFF ticket mapper drops `alarmCode`, `controllerSource` and probes.
- [ ] Simulator: tickets left open by a restart are not cleaned up; the Nordvik `tickets.json` is static.

### Search
- [ ] Inventory filter tags still use `/api/v1/inventory/search`; ticket search has no index; `/dashboard/users` has no query parameter; `TagContextSearch` is not mounted.

### Frontend and dev tooling
- [ ] About ten secondary lists have no right-click menu (machine groups, security groups, alert rules, templates, spare-part policies, delegations).
- [ ] The Vite main process still grows with many HMR rebuilds (toolchain); consider making Nuxt DevTools opt-in.
- [ ] e2e (Playwright) suites were not run in this round.

### UI

## Dashboard landing

# [x] Hero cards seem not to render actual data
- Resolved in `src/frontend/web/app/composables/useDashboard.ts` & `src/frontend/web/app/components/dashboard/StatCard.vue`. Real-time metrics fetched from `/api/machines`, `/api/tickets`, and `/api/users` are rendered dynamically without hardcoded falsy fallback numbers.

# [x] Audit logs
- Full audit logs dialog integrated in `src/frontend/web/app/pages/dashboard/overview.vue` via `AuditLogsVisualizer.vue` and `ActivityFeed.vue` with empty-state handling.

## Inventory

# [x] Hide AI import behind global setting
- `canUseAiImport` in `useAppSettings` gates AI import behind configured AI API tokens.
- Displays all audit logs here above info severity via `/api/analytics/audit-logs?minSeverity=above_info`.

# [x] When clicking on item body, open the items page
- Row clicks in `PartsInventoryList.vue` and `InventoryTable.vue` route to `/dashboard/inventory/${id}`.

# [x] Standalone inventory item page with editing, actions and data
- Full standalone page overhauled and fixed at `src/frontend/web/app/pages/dashboard/inventory/[id].vue` with live editing, scannable QR code generator, drop-in alternate parts, supplier data, stock consumption modal, and audit history. Verified with dedicated test suite `InventoryItemEntityView.test.ts`.

## Machines

# [x] Standalone machine view page
- Full standalone page implemented at `src/frontend/web/app/pages/dashboard/machines/[id].vue` with tickets, controllers, live remote view, and decommissioning.

# [x] Add some machine pictogram SVGs
- Component `src/frontend/web/app/components/machines/MachinePictogram.vue` provides vector pictograms for Assembly, CNC, Welding, Quality, Packaging, and Conveyor stations.

# [x] Add color coding for the machines, group by ONE machine grouping
- Deterministic group color mapping engine implemented in `src/frontend/web/app/utils/machineGroupColors.ts`.

# [x] The data display wrapper here would help a lot lmao
- `src/frontend/web/app/components/common/DataDisplayWrapper.vue` integrated into `src/frontend/web/app/pages/dashboard/machines.vue`.

## Plant Map

# [x] Add search and filtering highlight hits and grey out misses when searching
- `src/frontend/web/app/components/map/InteractiveMapCanvas.vue` dims non-matching entities (`opacity: 0.2`) and highlights matching station blocks when search query is active.

# [x] Add an expandable bubble floating above the stations for open tickets
- Floating incident count bubble rendered above each station in `InteractiveMapCanvas.vue` with expandable ticket lists.

## Delegations UI

# [x] Filter by who you can modify
- "Filter by who you can modify" toggle implemented in `DelegationsManager.vue` based on user role and dedication tier.

# [x] Add a tagged grouping to technicians / engineer / managers too
- Personnel tagging (`#electrical`, `#mechanical`, `#controls`, `#lead`) added to `DelegationsManager.vue`.

# [x] Assignments can be: Person (OR group via tag) -> Machine or Machine group(via tag)
- Flexible dedication rules supporting Person/Tag -> Machine/Tag assignments.

### New features

## Standalone search results page view

# [x] Get all the results, render by matching rate, group by search provider
- Standalone search page implemented at `src/frontend/web/app/pages/dashboard/search.vue`.

# [x] Here make sure redirects work as intended
- Redirects directly navigate to corresponding entity pages (`/dashboard/machines/${id}`, `/dashboard/inventory/${id}`, `/dashboard/tickets`).

# [x] Simplify the scoped searches elsewhere
- Standardized search configuration via `useConfigurableSearch.ts`.

# [x] Get rid of the ^F filters as they overlap in function with tags
- Scoped search bar unified to structured tags.

# [x] Reactive UI, query on input, complete tag on enter, search on enter when no tag is being completed or shift + enter
- Search bar reactive execution and tag autocomplete implemented in `HeimdallSearchBar.vue`.
- Tag syntax `<! or empty><key>:<value>` with default AND logic gate.

## Standalone filtering UI

# [x] Make search sleeker, and allow to later filter the search
- Split OmniSearch into decoupled free-text search engine (relevance ranking & keyword matching) and structured filtering engine (tag syntax `key:val`, negative gates `!key:val`, discrete facets) in `useSearchAndFilter.ts`.
- Created standardized UI component `SearchAndFilterBar.vue` reused across `DataDisplayWrapper.vue`, `inventory/index.vue`, and `search.vue`.

# [x] Only filters to stay in the search is tags
- Free-form text and tag filter engine (`key:val`, `!key:val`, `#tag`) separated cleanly from free text query. Verified with `SplitSearchAndFilterEngine.test.ts`.

# [x] Add a sorting engine
- Sorting engine supporting Best Match, Name, and Provider in `search.vue`, plus 3-state sorting in `DataDisplayWrapper.vue`.

## Create standard data display wrappers

# [x] Card view
# [x] List view
# [x] Table view
- 3-way toggle between Card, List, and Table views in `DataDisplayWrapper.vue`.
- On-click 3-state column sorting (none -> asc -> desc -> none).
- Decoupled closing X from card content (`decoupleClose: true`).
- 1 default action plus actions dropdown with custom context menu.

### Broken features

## All of the machine grouping frontend

# [x] Get rid of all of it, build it from the ground up
# [x] Create a tag store UI where you can click a tag which then has a selection pop-up to add to machines or existing groups, or drag and drop to machine or group.
# [x] Add groups / machines toggle for the above UI
# [x] Not tree based
# [x] Go for tagging here too
# [x] Manual overrides take precedence over security group mappings
- Rebuilt from the ground up in `src/frontend/web/app/components/tickets/MachineGroupManager.vue` and `machine-groups.vue`.

## [x] The VNC integration should dynamically get the target PCs VNC status, installed or not
- BFF endpoint `src/frontend/web/server/api/clients/[id]/vnc-status.get.ts` and dynamic status badge in `RemoteQuickViewModal.vue`.

## Telemetry templates UI is broken in live Telemetry

# [x] Get rid of it
- Removed broken "Templates" tab button at line 318 in `src/frontend/web/app/pages/dashboard/telemetry/index.vue`.

## Telemetry templates

# [x] Rename to telemetry recipes
- Rebranded `src/frontend/web/app/pages/dashboard/telemetry/templates.vue` to "Industrial Telemetry Recipes Studio" with explanatory callout clarifying data point register mapping and agent compilation.

## Ticket escalations need better UI/UX and better handling of escalation targets

# [x] Look into the definition of an escalation matrix
- Built `TicketEscalationMatrixModal.vue` with 4-tier industrial escalation matrix (L1 Shift Response, L2 Dedicated Engineer, L3 Management, L4 OEM), auto-resolving station dedicated engineer targets via `resolvePreferredTechnician`, supporting manual override targets, and handover protocol selection ('Notification', 'HandOff', 'ParallelWork').
- Wired into `TicketKanbanBoard.vue` and verified with unit tests `TicketEscalationMatrix.test.ts`.

### Get rid of it

## [x] No writing of Active directory or any other IT infra
- Read-only enforcement verified in `activeDirectoryStore.ts` and `ItInfraGovernance.test.ts`.

## [x] SAP Plant Maintenance (PM) Connector
- Confirmed out of scope and omitted from codebase.
