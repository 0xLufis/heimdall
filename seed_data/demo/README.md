# Nordvik NV1 demo dataset

A small, story-driven dataset for presenting Heimdall. **Everything in it is fictional:**
the plant, the companies (all names end in "(Demo)"), the people (physics-pun names with
"(Demo)" suffix) and every e-mail address (`*.example.com`).

Load it only into a development/demo database. The loader refuses to run unless
`HEIMDALL_ENABLE_DEV=true`, refuses Production/Staging environments and database names
that look like prod/staging.

## Quick start

```bash
just demo-check      # validate JSON, references, enums, timestamps; dry-run against the dev DB (ROLLBACK)
just demo-seed       # load / refresh the dataset into heimdall_dev_db (idempotent upsert) + demo logins
just demo-runtime    # with the backend running: register the demo Lua scripts + replay telemetry
just sim-demo        # with the backend running: live station telemetry for the demo plant (fleet simulator, MQTT)
just sim-reset demo  # one command: load the demo next to the enterprise data and run the simulator with plant activity
```

The dataset is **reference data plus a story backdrop**: plant, people, controllers, parts and the 14 story tickets of
`tickets.json` (the history before the presentation starts). Everything that happens while you present - telemetry,
heartbeats, new machine-raised tickets and their lifecycle, spare-part consumption - is produced by the fleet simulator
through MQTT and the backend REST API (`docs/guide/DEV_GUIDE.md` §3.5).

| Command | What it does |
|---|---|
| `python3 scripts/seed_demo_data.py --dry-run [--print-sql]` | Offline validation and SQL plan; no database access |
| `python3 scripts/seed_demo_data.py --check` | Runs everything against the DB in one transaction, then rolls back |
| `just demo-seed` | Upserts all demo rows in one transaction. Running it again resets the story (statuses, timestamps) |
| `just demo-seed --unpin-conflicts` | Also clears floor-plan handles on *non-demo* stations that use the same handles (see "Coexistence") |
| `just demo-reset` | Deletes only demo rows (`de30de30-…` UUIDs, `demo-…` auth ids, plus tickets on demo machines), then reloads |
| `just demo-runtime` | Registers the 3 demo Lua scripts and replays 697 telemetry samples via the backend API (localhost only) |
| `python3 scripts/seed_demo_data.py --write-telemetry` | Regenerates `telemetry.json` (deterministic) |
| `--now 2026-11-09T20:00:00Z` | Pins "now" so two runs produce byte-identical rows (used to prove idempotency) |

Timestamps are relative to load time (`~now-3h` in the JSON), so SLA timers are always
"live" when you present. Reload right before a demo.

### Demo logins (DEMO ONLY)

`just demo-seed` passes `--with-logins`, which creates Better-Auth credentials for all
demo users with the **demo-only password `Demo-Only-NV1!`** (override with
`HEIMDALL_DEMO_PASSWORD`). This password is public by design; never use the loader against
a shared or production database. The standard dev admin (`admin@heimdall.dev`) is created
separately by the Nuxt dev plugin.

| Persona | E-mail | Role | Use in the demo |
|---|---|---|---|
| Greta Gauss | greta.gauss@nordvik-demo.example.com | plant_engineering_manager | Escalation target, owns the business unit |
| Hugo Hertz | hugo.hertz@nordvik-demo.example.com | shift_leader | Escalated the laser ticket |
| Kurt Kelvin | kurt.kelvin@nordvik-demo.example.com | group_leader | Maintenance lead, spare-parts PO |
| Iris Joule | iris.joule@nordvik-demo.example.com | controls_engineer | Dedicated engineer (parallel work) |
| Lars Lumen | lars.lumen@nordvik-demo.example.com | engineer | Quality/vision owner |
| Ada Ampere | ada.ampere@nordvik-demo.example.com | technician | Laser specialist, works the hero ticket |
| Otto Ohm | otto.ohm@nordvik-demo.example.com | technician | On-call electrician, **absent (training)**, laser cert **expired** |
| Vera Volt | vera.volt@nordvik-demo.example.com | technician | PLC/controls, Line 2 owner, Otto's backup |
| Nils Newton | nils.newton@nordvik-demo.example.com | technician | Dedicated to the housing CNC |
| Paul Pascal | paul.pascal@nordvik-demo.example.com | technician | Fastening/pneumatics, vacation next week |
| Mia Mohs | mia.mohs@nordvik-demo.example.com | operator | Reported the torque issue |

## What is in the dataset

| Entity (table) | Count | Highlights |
|---|---|---|
| Organization (`auth.organization`) | 1 | `demo-org-nordvik` |
| Users / memberships (`auth.user`, `auth.member`) | 11 / 11 | roles from `HeimdallRoles`; optional credentials (`auth.account`) |
| Machine groups (`machine_groups`) | 8 | Business unit → Project → 3 Lines → 1 Sub-line, plus 2 technology groups (Laser fleet, Vision) |
| Stations (`inventory_items` + `stations`, TPT) | 20 | 3 lines, 15 pinned to floor-plan handles `L01-…`, `L02-…`, `L03-…`; **2 cells with sub-machines** (Roundtable `NV-L2-OP040` → S1/S2, laser cell `NV-L2-OP060` → laser source + robot); 1 **decommissioned** station |
| Controllers (`client_pcs`) / links (`StationControllers`) | 6 / 20 | 1-1 (dedicated laser IPCs), 1-n (line IPCs), m-n (`NV-L2-OP060` has primary + secondary IPC); every active station incl. the 4 sub-machines has exactly one *Primary* (reporting) controller; IPCs are pinned to the block of the station their cabinet stands at |
| Installed parts (`inventory_items` + `hardware_assets`) | 21 | 7 in `NV-L2-OP060` (incl. the cell PLC) + one process part per other active station; `metadata.partClass` says which spare fits |
| Machine components (`machine_components`) | 21 | slots = mesh names of `/sample/station_digital_twin.glb` → **3D digital twin binding**; every active top-level station has one |
| Spare parts (`inventory_items`, stock) | 9 | 2 **below minimum** (protective glass 2/10, servo 1/2), 1 at minimum (collimator 1/1), 1 **out of stock** (spindle bearing 0/1), 1 under repair (RMA), 1 serialized camera, 3 healthy; `metadata.consumableClass` / `partClass` link them to the simulator's repairs |
| Group memberships (`machine_group_memberships`) | 23 | |
| Skills / certificates (`skill_definitions`, `personnel_skills`) | 6 / 11 | 1 expired (Otto, laser), 1 expiring in 25 days (Ada, electrical) |
| Maintenance groups / members (`personnel_groups`, `…_members`) | 4 / 9 | Laser specialists, Controls on-call 24/7, Mechanical shift A, Escalation |
| Delegation recipes (`delegation_recipes`) + junctions | 3 (+3 maint. groups, +5 machine groups) | Laser (technology primary), Controls (all lines), CNC (complete machine dedication) |
| Machine delegation bindings | 5 | |
| Technician dedications (`technician_rules`) | 4 | technology / group / machine scopes with backups |
| Shift absences | 2 | Otto now (training, backup Vera), Paul next week |
| Generic templates (`generic_templates`) | 3 | `NV-MCH-LASER-WELD` (extends `MCH-ROBOT-CELL-01`), `NV-TEL-LASER-HEALTH` (extends `TEL-DRIVE-01`), `NV-TCK-LASER-DRIFT` (extends `TCK-POS-01`); **3 instances** stored in station / ticket `metadata.template` |
| Tickets (`maintenance_tickets`) | 14 | Open 3, InProgress 2, Pending 4 (Parts, ExternalOk, Action, SignOff), Resolved 3, Closed 2; 2 escalated (1 active, 1 closed); 2 line stops |
| Ticket comments / service log entries | 10 / 6 | |
| Telemetry history (`telemetry.json`) | 5 stations, 11 series, 697 samples | 6 h at 5-min spacing; persisted into ticket `telemetry_probes_json` (probe buffers) and `telemetry_snapshot` |
| Lua scripts (`lua/`, `backend.lua_scripts` when present) | 3 (+ replay helper) | watchdog (ticket-creating, deduplicated), SLA report (read-only), UI extensions (read-only) |

### The story

*Line 2's rotor laser welding cell `NV-L2-OP060` is losing laser power.*

1. ~4 h ago the laser power started drifting (6.0 → 5.7 kW). At **‑3 h** the
   Telemetry Watchdog raised a **Critical** ticket (`ALM-LSR-217`) automatically.
2. Ada picked it up via the station QR code 20 min later, stopped the line (45 min line stop),
   cleaned the protective glass – power recovered to 5.92 kW – and restarted.
3. The power is drifting again and the **protective glass temperature is 53–54 °C, ~20 min
   from the 55 °C alarm**. The Critical SLA (2 h) is breached; Hugo escalated at ‑1 h to Iris
   (`ParallelWork`).
4. The replacement glass is a **Pending (Parts)** ticket: only 2 glasses left (minimum 10),
   both reserved for the twin cell on Line 1; PO with 2-day ETA.
5. Meanwhile: resin flow on `NV-L1-OP030` just dropped below spec (Open, unassigned), the housing CNC spindle on Line 3 is slowly wearing toward its vibration alarm (Pending, OEM visit),
   and the on-call electrician Otto is in training today – his laser certificate expired anyway.

The plant-map KPI overlay shows the live station telemetry of the simulator, keyed by floor-plan
handle; with `just sim-scenario demo_story` `L02-OP060` drifts into *FAULTED* and `L01-OP030` slows down,
which matches the story. Demo tickets carry the handle (e.g. `L02-OP060`) in `tags` so the HUD correlates them.

## Suggested 5–10 minute walkthrough

Before presenting: `just demo-seed` (refreshes all timers) and, with the backend running,
`just demo-runtime`. Log in as `admin@heimdall.dev` (or as a persona above).

1. **Plant map – `/dashboard/map`** (1 min). Switch the Telemetry HUD to *Alerts & SLA*.
   `L02-OP060` is red (SLA breached), `L01-OP030` shows active tickets. Expand the bubble on
   `L02-OP060`: OEE, yield, oscillation sparkline. *"Something is wrong on Line 2."*
2. **Tickets – `/dashboard/tickets`** (2 min). Filter by search `laser` / tag `#Drift`. Open
   *"Laser power drift – weld penetration below spec"*: Critical, InProgress, escalated
   (ParallelWork → Iris), line stop 45 min, alarm `ALM-LSR-217`, change history, 4 comments,
   telemetry snapshot and the probe buffer (36 samples of laser power / glass temperature /
   penetration). Then show the board across all 5 states and the Pending reasons
   (Parts, ExternalOk, Action, SignOff).
3. **Machine & digital twin – `/dashboard/machines`** (1.5 min). Open `NV-L2-OP060 Rotor
   Laser Welding Cell`: line/sub-line/project/business-unit hierarchy, the two sub-machines
   (laser source, robot), two controllers (primary + secondary IPC). Open the 3D twin and
   click the optic head – it is bound to the `TOOL-HEAD-01` component (serial
   `NV-DEMO-OH-2060`). Show the service log (glass replaced 9 days ago, cleaned today).
   Optionally show `NV-L2-OP040` (Roundtable) and the decommissioned `NV-L3-OP100`.
4. **Who fixes it – `/dashboard/delegations` and `/dashboard/users`** (1.5 min). The *Laser
   Welding – Process & Optics* recipe: requires `NV-LASER-CL4`, primary Ada, on-call fallback
   Otto, escalation after 20 min to *Plant Engineering Escalation*. Point out that Otto is absent
   today (backup Vera) and his laser certificate is expired – so the fallback would not be
   qualified. Show Nils' *complete machine dedication* for the housing CNC.
5. **Spare parts – `/dashboard/inventory`** (1 min). Filter stock items: protective glass
   2 / min 10, servo 1 / min 2, spindle bearing 0 / min 1, a servo drive under repair.
6. **Templates – `/dashboard/templates`** (0.5 min). `NV-MCH-LASER-WELD` extends the builtin
   robot-cell template; both laser cells are instances of it (`metadata.template`).
7. **Automation – `/dashboard/scripts`** (1.5 min). Run *NV Demo – Telemetry Threshold & Drift
   Watchdog* with context `{"context_machine_id": "<id of NV-L2-OP060>"}`: it reports the
   alarm, finds the existing ticket and **does not create a duplicate**, and projects
   *"glass temperature alarm in ~20 min"*. Run it for `NV-L3-OP020`: vibration OK, alarm in
   ~26 h. Run *SLA Risk Report* (read-only). The *Laser Fleet UI Extensions* script has already
   registered a dashboard banner, a machine badge and a read-only "Laser health snapshot" action.

Station ids: `docker exec postgres_heimdall psql -U postgres -d heimdall_dev_db -Atc "select custom_identifier, id from backend.stations where id::text like 'de30de30%' order by 1"`.

## Live simulation (fleet simulator)

`just demo-runtime` replays *history* into the Lua buffer. For *live* values on the plant map, the station
KPI tiles and the telemetry pages, run the fleet simulator against this dataset (backend must be running):

```bash
just sim-demo                                   # 6 demo IPCs (heartbeats) + 19 stations (process data), foreground
just sim-demo --scenario demo_story --speed 10  # start with the story drifts, 10x faster
just sim-scenario demo_story                    # or trigger the story on the running simulator
just sim-scenario clear                         # recover every station
```

It publishes over MQTT like the real agent (`heimdall/devices/HW-NV-IPC-…/system_info` and `…/plc_memory`), using
`controllers[].machine_identifier`, `station_controllers` (the *Primary* controller reports a station; the
decommissioned `NV-L3-OP100` and the sub-machines without a controller link are not simulated) and, per station,
`custom_identifier`, `machine_type`, `metadata.line`, `metadata.cycleTimeTargetS` and the laser template values
(`laser_power_nominal_kw`, `laser_power_min_kw`, `glass_temp_alarm_c`). With plant activity on (default), faults become
machine-raised tickets that the crews of `people.json` (laser specialists, controls on-call, mechanical shift A) reserve,
pick up, repair with parts from the demo store and close.

`demo_story` starts three drifts: `NV-L2-OP060` loses laser power while the protective glass heats up
(WARNING, then FAULTED once power falls below 5.7 kW or the glass reaches 55 °C; latched until recovered),
`NV-L3-OP020` develops spindle bearing wear (vibration), `NV-L1-OP030` slows down (cycle time). Single faults,
drifts, maintenance, line stops and the speed are available through the control API on `127.0.0.1:5055`
(see `docs/guide/DEV_GUIDE.md` §3.5). Stop `just start simulator` first if it runs with another dataset
(`sim-demo` does that for you).

## Coexistence with the enterprise dataset

`just db-seed` loads the 100-station enterprise dataset, which also uses the `L01-…L08-…`
floor-plan handles. With both loaded the map shows two stations per handle (the loader warns).
Options: load the demo into a database without the enterprise data, or run
`just demo-seed --unpin-conflicts`, which clears `pinned_object_handle` on the conflicting
*non-demo* stations (re-running `just db-seed` restores them – note that `db-seed` truncates
all tables, demo data included).

## Files

| File | Content |
|---|---|
| `plant.json` | organization, users, manufacturers, suppliers, machine groups, machines, controllers, controller links, installed parts, spare parts, components, group memberships |
| `people.json` | skills, certificates, maintenance groups, delegation recipes + junctions, machine bindings, technician dedications, absences |
| `tickets.json` | tickets, comments, machine service logs |
| `templates.json` | plant-specific generic templates (`extends_code` → builtin template) |
| `telemetry.json` | generated telemetry history (`--write-telemetry`) |
| `lua/*.lua`, `lua/manifest.json` | demo automation scripts and the telemetry replay helper |

Per-component links: every demo controller carries `system_metadata.RemoteAccess.Vnc` (port 8006, path
`websockify`, no host = the machine serving the web UI, i.e. the Windows dev container from `just windows start`)
and every demo station carries `metadata.model3dUrl` (`/sample/station_digital_twin.glb`). The remote viewer and
the 3D twin resolve through these per-row links, so they all show the same desktop / model only because the rows
point at the same target; change a row to point one controller or station somewhere else.

Format conventions: column names are the live PostgreSQL column names; `_key` becomes a
deterministic UUID (`de30de30-…`, uuid5 of the key); `"@key"` references another row;
`"~now-2d4h"` / `"~now+90m"` are timestamps relative to load time. The loader validates
references, duplicate ids/handles, every active station has exactly one Primary controller, every pin exists in
`production_hall.dxf`, every component slot is a mesh of the digital twin and holds a part installed in that station,
spares fit an installed part class, roles are `HeimdallRoles` constants, enum values (ticket status/priority/pending reason/
handover state, group types, disciplines, skill categories …), negative stock, timestamp
ordering (no future `created_at`/`resolved_at`, comments after their ticket), state
consistency (Pending ⇒ pending reason, Resolved/Closed ⇒ resolved_at) and that all e-mails
use reserved example domains. Against a live DB it also diffs its column allow-list with
`information_schema` and reports schema drift.

## Known limitations

* **Lua scripts:** once the `AddLuaScripts` migration has created `backend.lua_scripts`, `just demo-seed`
  upserts the 3 demo scripts there (they survive restarts). On an older DB the table is skipped and
  `just demo-runtime` registers them through the API instead.
* **The Lua telemetry buffer is in-memory** (`DbPlantDataProvider`), so it is empty after every backend
  restart → run `just demo-runtime` again. Replaying twice appends the samples again (the buffer keeps
  the last 200 per metric). UI extensions are registered when the UiExtension script runs (on create/update).
* **Tenant scoping:** demo rows carry `organization_id = demo-org-nordvik`. Platform admins
  (`admin@heimdall.dev`, `system_admin`) see every organization; other users only see their active
  organization's rows (`TenantScopeMiddleware`). Present as the admin, or make sure a persona's session
  has `demo-org-nordvik` as active organization.
* There is no persistent telemetry-history table; history is persisted only inside tickets
  (`telemetry_probes_json` probe buffers, `telemetry_snapshot`). Station KPI tiles show the last
  telemetry the simulator reported (`GET /api/v1/analytics/station-kpis`).
* Tickets have no SLA column; the loader stores `metadata.slaHours` and `metadata.slaDueAt`. The
  BFF ticket mapper reads only a top-level `slaDueAt`, so DB tickets show no SLA due date in the UI
  until the mapper falls back to `metadata.slaDueAt`.
* The skills catalog / personnel-skills pages are served from an in-memory BFF store
  (`server/utils/componentsAndDelegationsStore.ts`), not from `skill_definitions`.
* On a dev DB whose delegation junction tables were created by the old `02-schema-patches.sh`
  (`created_at` column), `GET /api/v1/delegations/recipes` fails with `column d0.assigned_at does not exist`
  because EF maps `AssignedAt`. The `MachineHierarchyDelegationAndTemplates` migration creates them with
  `assigned_at`; the loader adapts to whichever column exists.
