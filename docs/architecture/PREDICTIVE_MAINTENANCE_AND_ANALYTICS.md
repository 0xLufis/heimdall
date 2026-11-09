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

The Machine Health Index $H_m \in [15\%, 100\%]$ represents the operational integrity of machine $m$:

$$H_m = \max\left(15\%, 100\% - (\Delta_{\text{anomalies}} \times 6.5\%) - (N_{\text{tickets}} \times 12.0\%) - \Delta_{\text{drift}}\right)$$

Where:
- $\Delta_{\text{anomalies}}$ is the count of statistical anomalies detected within the rolling 24-hour window.
- $N_{\text{tickets}}$ is the number of unresolved maintenance tickets associated with the machine or its controlling IPC.
- $\Delta_{\text{drift}}$ is the normalized variance from factory-calibrated nominal parameters.

---

## 4. Overall Equipment Effectiveness (OEE) Modeling

Heimdall computes line and machine-level OEE using the canonical manufacturing formula:

$$\text{OEE} = A \times P \times Q$$

- **Availability ($A$)**: Operating Time divided by Planned Production Time.
- **Performance ($P$)**: Net Operating Time divided by Operating Time (ideal cycle time vs. observed cycle time).
- **Quality ($Q$)**: First-Pass Good Output divided by Total Output.

---

## 5. Station-Level Real-Time Telemetry & Plant Map HUD Overlays

To empower shopfloor supervisors and controls engineers with immediate contextual insight directly on the factory floor layout, Heimdall computes station-level operational telemetry aligned with CAD drawing block handles (e.g. `L01-OP010` to `L08-OP130`):

### 5.1 Station Telemetry Metrics Matrix

* **Station OEE**: Real-time Overall Equipment Effectiveness percentage calculated per machine node, broken down by Availability, Performance, and Quality components.
* **First-Pass Quality Yield**: Ratio of conforming products produced on first pass without requiring rework, scrap, or re-testing:
  $$\text{Yield} = \frac{\text{Conforming Parts}}{\text{Total Parts Produced}} \times 100\%$$
* **Production Throughput (Parts / Hour)**: Instantaneous production rate extrapolated from active operating cycles:
  $$\text{Parts/Hour} = \frac{3600}{\text{Current Tact Time (s)}}$$
* **SLA Incident Tracking**: Real-time correlation with active maintenance tickets. Monitors ticket severity, earliest breach deadlines, and SLA compliance health (`healthy`, `warning`, `breached`, `none`).

### 5.2 Operational Status Classification

Station operational state is dynamically evaluated based on telemetry limits and ticket severity:
* **`RUNNING`**: Machine is cycling within nominal tact time and OEE envelope with zero critical alarms.
* **`IDLE`**: Performance index drops below 82% or parts infeed is starvation-paused.
* **`WARNING`**: OEE drops below 85% or an open High/Critical ticket has an impending SLA breach (< 2 hours).
* **`FAULTED`**: Immediate line stop, emergency stop (E-stop), hardware fault, or breached SLA incident active.
* **`MAINTENANCE`**: Station reserved or undergoing technician on-site diagnostics.

---

## 6. Endpoints & API Reference

| Endpoint | Method | Response Model | Purpose |
| :--- | :--- | :--- | :--- |
| `/api/v1/Analytics/fleet-summary` | `GET` | `FleetSummaryDto` | Fleet uptime, top-faulting machines, backlog age, stock depletion |
| `/api/v1/Analytics/trends` | `GET` | `MachineTrendSeriesDto` | Rolling telemetry points with nominal limits and anomaly flags |
| `/api/v1/Analytics/kpis` | `GET` | `List<LineKpiDto>` | Production line OEE, MTBF, and MTTR aggregations |
| `/api/v1/Analytics/station-kpis` | `GET` | `List<StationKpiDto>` | Station-level OEE, Yield, Parts/Hour, and SLA tickets for CAD overlays |
| `/api/v1/Analytics/powerbi/config` | `GET` | `PowerBiConfigDto` | Power BI Embedded workspace and token configuration |
