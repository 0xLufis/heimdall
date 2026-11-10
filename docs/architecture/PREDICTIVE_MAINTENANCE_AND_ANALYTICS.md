# Predictive Maintenance, Anomaly Detection & Fleet Analytics Architecture

This document specifies the technical architecture, mathematical foundations, and implementation details for Heimdall's **Predictive Maintenance & Fleet Analytics Engine** (Milestone 12, PM-001 through PM-004).

---

## 1. Overview & Operational Goals

OT/ICS environments require continuous, non-intrusive statistical surveillance of edge controller telemetry (Industrial PCs, Soft-PLCs, Fieldbus Runtimes, and Hardware PLCs) to detect mechanical wear, thermal throttling, and process drift *before* catastrophic line failure occurs.

Heimdall combines:
1. **Statistical Z-Score Anomaly Detection**: Real-time outlier identification over rolling timeseries buffers.
2. **Remaining Useful Life (RUL) & Machine Health Index**: 0–100% degradation scoring per station.
3. **MTBF & MTTR Aggregation**: Reliability metrics derived from historical maintenance incident logs.
4. **Custom KPI Builder**: Engineer-configurable chart widgets (OEE, availability, performance, quality).

---

## 2. Statistical Z-Score Anomaly Detection Engine

For any monitored continuous telemetry metric $X = \{x_1, x_2, \dots, x_N\}$ within a rolling window $T$:

### Mathematical Formulations

$$\mu = \frac{1}{N}\sum_{i=1}^N x_i$$

$$\sigma = \sqrt{\frac{1}{N}\sum_{i=1}^N (x_i - \mu)^2}$$

The standardized score $z_i$ for each telemetry observation $x_i$ is computed as:

$$z_i = \frac{x_i - \mu}{\sigma}$$

### Severity Classification Thresholds

| Standardized Score | Classification | Action Triggered |
| :--- | :--- | :--- |
| **\|z\| ≤ 2.5** | **Normal Operational Range** | Nominal telemetry recording |
| **2.5 < \|z\| ≤ 3.0** | **Degradation Warning** | Visual telemetry chart marker, log entry |
| **\|z\| > 3.0** | **Critical Outlier Anomaly** | Machine Health Index penalty, proactive ticket recommendation |

---

## 3. Remaining Useful Life (RUL) & Machine Health Index

The Machine Health Index $H_m \in [0\%, 100\%]$ is a heuristic score computed only from stored counts:

$$H_m = \operatorname{clamp}_{[0,100]}\left(100\% - (\Delta_{\text{anomalies}} \times 6.5\%) - (N_{\text{tickets}} \times 12.0\%)\right)$$

Where:
- $\Delta_{\text{anomalies}}$ is the count of statistical anomalies detected for the machine (0 where no telemetry series exists).
- $N_{\text{tickets}}$ is the number of open maintenance tickets on the machine.

Remaining-useful-life forecasting is not implemented: no wear model or runtime counters are stored, so no RUL value is reported.

---

## 4. Overall Equipment Effectiveness (OEE) Modeling

Heimdall computes line and machine-level OEE using the canonical manufacturing formula:

$$\text{OEE} = A \times P \times Q$$

- **Availability ($A$)**: Operating Time divided by Planned Production Time.
- **Performance ($P$)**: Net Operating Time divided by Operating Time (ideal cycle time vs. observed cycle time).
- **Quality ($Q$)**: First-Pass Good Output divided by Total Output.

---

## 5. Station-Level Real-Time Telemetry & Plant Map HUD Overlays

Station overlays on the plant map and the 3D digital twin show only measured data. There is no
simulator or fallback generator in the backend, the BFF or the browser: a station without telemetry
renders an explicit "No telemetry" / `—` state.

### 5.1 Data Path

```
PLC (TwinCAT ADS struct)  ──MQTT──▶  TelemetryIngestionService.ProcessPlcMemoryAsync
                                       │ StationTelemetryExtractor (field-name matching)
                                       │ station = machine bound to the sending IPC (StationControllers)
                                       ├─▶ Redis / ICacheService  telemetry:station:{machineId}  (merged snapshot, 24 h)
                                       ├─▶ SignalR /hubs/maintenance → group org:{OrganizationId} (+ platform-admins)
                                       │     StationOscillationReceived / StationTelemetryReceived
                                       └─▶ ILuaAutomationService.EvaluateTelemetryChangeAsync (Lua ring buffer + scripts)

GET /api/v1/analytics/station-kpis  ◀── machines (tenant-filtered) + snapshots + open tickets
        ▲
Nitro BFF /api/telemetry/station-kpis (proxyToBackend: session token + session org only)
        ▲
useStationTelemetry (REST load + hub events) ──▶ StationTelemetryBubble / map.vue / Machine3dDigitalTwin
```

Recognised PLC fields (case-insensitive, separators and a one-letter IEC prefix ignored, any struct depth):
vibration (`VibrationRms`, `VibrationMmS`), spindle speed (`SpindleRpm`), power (`PowerKw`), temperature
(`MotorTemp`, `TemperatureC`), cycle time (`CycleTimeMs`, `CycleTimeSeconds`), frequency, `Availability`,
`Performance`, `Quality`, `Oee`, `PartsPerHour`, `TargetPartsPerHour`, `TactTime`, `PartsProduced`, `ScrapParts`
and the station state (`State`, `MachineState`; string or device-state label). See `StationTelemetrySnapshot.cs`.

### 5.2 Station Metrics

* **Yield** = (parts produced − scrap) / parts produced, from PLC counters.
* **Quality** = reported quality, else yield. **OEE** = reported OEE, else A × P × Q when all three are reported.
* **Throughput, cycle/target times** are taken as reported; nothing is extrapolated.
* **Tickets / SLA**: open tickets bound to the machine; `hasSlaBreach` uses the backend rule (open Critical ticket
  older than 12 h). The UI additionally evaluates each ticket's `slaDueAt` where present.

### 5.3 Operational Status

Reported by the PLC state field and mapped to `RUNNING`, `IDLE`, `WARNING`, `FAULTED`, `MAINTENANCE`; machines whose
status is `maintenance` show `MAINTENANCE`; otherwise `UNKNOWN`. Status is not inferred from OEE thresholds.

### 5.4 Live Dynamics

`StationOscillationReceived` carries the dynamics present in each ingested block (vibration RMS, spindle RPM, power,
temperature, cycle time, frequency; absent quantities are `null`). The client keeps a 24-sample vibration history per
station for the sparkline. Vibration above 2.8 mm/s (ISO 10816 class II "unsatisfactory" boundary) is highlighted.
The update rate is the PLC publish rate; the hub adds no interpolation.

### 5.5 3D Digital Twin HUD

Component node bounding boxes are projected from Three.js world space into viewport coordinates:
$$\mathbf{v}_{\text{ndc}} = \mathbf{P} \cdot \mathbf{V} \cdot \begin{pmatrix} x \\ y_{\text{max}} + \Delta y \\ z \\ 1 \end{pmatrix}$$
$$\text{screenX} = \left(\frac{v_{\text{ndc}}.x + 1}{2}\right) \times W_{\text{canvas}}, \quad \text{screenY} = \left(\frac{1 - v_{\text{ndc}}.y}{2}\right) \times H_{\text{canvas}}$$
Node tint reflects the bound component's stored `EquipmentStatus`; live values are station-level (telemetry is not
reported per CAD node).

### 5.6 Line & Fleet KPIs

* Line KPIs group machines by line group: incidents (30 days), MTTR from resolved tickets, observed MTBF (mean gap
  between consecutive incidents per machine), and A/P/Q/OEE averaged over stations that report them.
* Fleet summary: controller online share, backlog, stock depletion, top-faulting machines from tickets.
  `totalMachineHours` and `slaCompliancePercentage` are `null` until runtime counters / ticket due dates are stored.
* Trend series come from ingested station metrics (Lua telemetry buffer, in-process, last 200 samples per metric).
  Recipe-based wear forecasting has no data source and returns no forecast.

---

## 6. Endpoints & API Reference

| Endpoint | Method | Response Model | Purpose |
| :--- | :--- | :--- | :--- |
| `/api/v1/Analytics/fleet-summary` | `GET` | `FleetSummaryDto` | Fleet uptime, top-faulting machines, backlog age, stock depletion |
| `/api/v1/Analytics/trends` | `GET` | `MachineTrendSeriesDto` | Rolling telemetry points with nominal limits and anomaly flags |
| `/api/v1/Analytics/kpis` | `GET` | `List<LineKpiDto>` | Production line OEE, MTBF, and MTTR aggregations |
| `/api/v1/Analytics/station-kpis` | `GET` | `List<StationKpiDto>` | Station-level OEE, Yield, Parts/Hour, and SLA tickets for CAD overlays |
| `/api/v1/Analytics/powerbi/config` | `GET` | `PowerBiConfigDto` | Power BI Embedded workspace and token configuration |
