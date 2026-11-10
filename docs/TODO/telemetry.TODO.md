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

## Open Items
- [ ] Persist station telemetry history (time-series store); trends currently use the in-process Lua buffer (200 samples).
- [ ] Ingest machine runtime counters to provide `totalMachineHours` and runtime-based MTBF.
- [ ] Store SLA due dates on tickets in the backend so `slaCompliancePercentage` and SLA warnings can be computed server-side.
- [ ] `TelemetryIngestionService.ProcessTelemetryBatchAsync` acknowledges batches without unpacking them; implement decoding.
- [ ] Per-CAD-node telemetry binding (current live values are station-level).
- [ ] Pass `station-id` from `StationDetailWorkbench.vue` to `Machine3dDigitalTwin` so the 3D HUD shows live values.

## Future Enhancements
- [ ] Direct OPC UA PubSub binary gateway for hardware PLCs without MQTT broker.
- [ ] User-customizable bubble widget presets saved per user profile.
