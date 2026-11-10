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
