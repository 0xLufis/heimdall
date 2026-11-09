# Telemetry & Operational Analytics TODO & Specification

## Completed Features

### 1. Station-Level Real-Time Telemetry & Floor Plan Overlays
- [x] **Station KPI Engine (`PredictiveMaintenanceService.GetStationTelemetryKpis`)**:
  - Implemented deterministic station-level metrics calculation across all 100 DXF stations (`L01-OP010` to `L08-OP130`).
  - Core metrics: OEE ($A \times P \times Q$), First-Pass Quality Yield, Throughput Rate (Parts/Hour), Cycle Tact Time, Scrap Parts count.
  - Operational Status evaluation: `RUNNING`, `IDLE`, `WARNING`, `FAULTED`, `MAINTENANCE`.
  - Endpoint: `GET /api/v1/analytics/station-kpis` with line and station query filters.
- [x] **Nitro BFF Telemetry Route (`server/api/telemetry/station-kpis.get.ts`)**:
  - Synchronous high-performance station KPI generation aligning CAD layout blocks with inventory records.
- [x] **Composable (`useStationTelemetry.ts`)**:
  - Synchronous `getStationKpi` with instant deterministic fallback.
  - Real-time `getStationSla` correlating active tickets, identifying SLA breaches, warnings (<2h to breach), and generating countdowns.
  - Lifecycle-safe live simulation loop with automatic teardown in `onUnmounted` (`stopLiveSimulation()`).
- [x] **Expandable Bubble HUD (`StationTelemetryBubble.vue`)**:
  - Compact view: floating pill with OEE badge, Yield % & Parts/Hour, and urgent SLA Ticket alerts.
  - Expanded view: glassmorphic HUD card with A/P/Q gauge bars, scrap count, tact time, active SLA tickets list, and quick actions ("Select Node", "+ Ticket").
- [x] **Plant Map CAD Integration (`InteractiveMapCanvas.vue` & `map.vue`)**:
  - Subpixel-crisp HTML overlay layer synchronized with SVG viewBox coordinate transformations.
  - HUD Toolbar controls: Master toggle, filter modes (`Alerts & SLA`, `All Nodes`, `Hover Focus`), and Fleet Summary Ticker.
  - Spatial Sidebar synchronization with Active Station Telemetry inspection card.

### 2. High-Frequency PLC & IPC Ingestion
- [x] Ring buffer stream visualization (`CircularBuffer.ts`, `useTelemetryStream.ts`).
- [x] Real-time Soft-PLC jitter, cycle time, thermal, and vibration metrics registry (`telemetryCacheStore.ts`).
- [x] Standardized catalog discovery for built-in and user-defined metrics.
- [x] Sub-second dynamic machine oscillation streaming over SignalR (`StationOscillationReceived`, `StationTelemetryReceived`).
- [x] Live SVG sparkline waveform visualization in plant map and 3D digital twin HUD overlays.

### 3. 3D Digital Twin HUD Telemetry Overlays
- [x] Three.js camera projection mapping 3D mesh bounding boxes to 2D viewport coordinates in `useCadModelViewer.ts`.
- [x] Interactive floating 3D HUD telemetry bubbles in `Machine3dDigitalTwin.vue` across mechanical slots (`GANTRY-X-01`, `TOOL-HEAD-01`, `CONVEYOR-01`).
- [x] Sub-second dynamics (Vibration RMS, Spindle RPM, Power Load) tracking 3D camera orbit, pan, and zoom.

## Future Enhancements
- [ ] Direct OPC UA PubSub binary gateway for hardware PLCs without MQTT broker.
- [ ] User-customizable bubble widget presets saved per user profile.
