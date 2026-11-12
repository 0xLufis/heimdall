# Telemetry & Operational Analytics TODO & Specification

## Completed Features

### 1. Station-Level Telemetry & Floor Plan Overlays (real data path)
- [x] **Ingestion → station snapshot**: `TelemetryIngestionService` extracts station fields from ADS struct blocks
  (`StationTelemetryExtractor`), attributes them to the machine bound to the IPC and merges them into the cached
  `StationTelemetrySnapshot` (`telemetry:station:{machineId}`).
- [x] **Org-scoped SignalR**: `StationOscillationReceived` / `StationTelemetryReceived` (typed DTOs) are sent to
  `org:{OrganizationId}` + `platform-admins`; all ticket / inventory / PLC events use the same addressing
  (`MaintenanceHubGroups`). Connections join groups from their claims in `OnConnectedAsync`.
- [x] **Lua**: ingested station metrics feed `ILuaAutomationService.EvaluateTelemetryChangeAsync` (failure-isolated).
- [x] **Station KPIs** (`GET /api/v1/analytics/station-kpis`): machines + snapshots + open tickets; unreported values are `null`.
- [x] **Line KPIs / fleet summary / trends** computed from tickets, snapshots and the ingested history; no demo data.
- [x] **BFF** `server/api/telemetry/station-kpis.get.ts` proxies to the backend (session token + session org only).
- [x] **Composable** `useStationTelemetry.ts`: REST load + hub events, nullable contract, explicit no-data state;
  no client-side generators.
- [x] **HUD**: `StationTelemetryBubble.vue`, `InteractiveMapCanvas.vue` (viewport-clamped placement in
  `utils/telemetryOverlayPlacement.ts`), `map.vue`, `Machine3dDigitalTwin.vue`.

### 2. High-Frequency PLC & IPC Ingestion
- [x] Ring buffer stream visualization (`CircularBuffer.ts`, `useTelemetryStream.ts`).
- [x] Telemetry metric catalog (built-in + user-defined definitions) in `telemetryCacheStore.ts`.

### 3. Live Telemetry Hub & Dev Simulation
- [x] `/dashboard/telemetry` is station-centric: fleet summary tiles (filter the directory), station directory grouped by
  line, per-station live dynamics (6 metrics with trend + min/avg/max), OEE / production KPIs, tickets + SLA flags and the
  controllers bound to the station; deep links `?station=<id|handle>` and `?controllerId=`.
- [x] `useTelemetryStream.ts` follows one (reactive) station over the shared maintenance hub connection
  (`StationOscillationReceived` / `StationTelemetryReceived`); values are `null` until reported, freshness is
  `live | stale | waiting`. The old implementation listened for fields the backend never sent.
- [x] `GET /api/v1/analytics/station-kpis` also returns the last reported dynamics, so the page is populated before the
  next live sample.
- [x] Fleet simulator publishes over MQTT 5 (the backend has no gRPC ingestion) and simulates station processes for the
  enterprise and Nordvik demo datasets, with runtime scenarios (fault, drift, line stop, demo story) through its control
  API and the in-app simulator panel (dev only). Contract fixture: `tests/fixtures/simulator/station_blocks.json`.
- [x] Per-component links: a controller's remote view resolves through `ClientPc/{id}/remote-access`
  (`system_metadata.RemoteAccess.Vnc`), a station's 3D twin through the station tree (`metadata.model3dUrl`, else a
  component CAD reference). No client-side default machine/model.
- [x] `Machine3dDigitalTwin` receives the station id (live HUD) and is re-created per station / model.

## Open Items
- [ ] Persist station telemetry history (time-series store); trends currently use the in-process Lua buffer (200 samples).
- [ ] Ingest machine runtime counters to provide `totalMachineHours` and runtime-based MTBF.
- [ ] Store SLA due dates on tickets in the backend so `slaCompliancePercentage` and SLA warnings can be computed server-side.
- [ ] `TelemetryIngestionService.ProcessTelemetryBatchAsync` acknowledges batches without unpacking them; implement decoding.
- [ ] Per-CAD-node telemetry binding (current live values are station-level).
- [ ] Remote-desktop gateway: the per-controller VNC link is opened directly from the browser; add an authenticated
  websocket relay in the BFF so controllers on OT VLANs are reachable without exposing noVNC ports.
- [ ] UI to edit a station's `model3dUrl` and model upload/storage (today the link is set in station metadata / seeds).

## Future Enhancements
- [ ] Direct OPC UA PubSub binary gateway for hardware PLCs without MQTT broker.
- [ ] User-customizable bubble widget presets saved per user profile.
