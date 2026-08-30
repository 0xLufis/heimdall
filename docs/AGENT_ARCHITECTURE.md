# Heimdall Industrial Edge Agent Architecture Manual

This document provides a comprehensive architectural specification of the **Heimdall Industrial Edge Agent Daemon** (`App.Agent.Daemon`), detailing its operational topology, threading lifecycle, Multi-Recipe Merger DAG, dynamic bandwidth throttling, and store-and-forward resilience.

---

## 1. High-Level Edge Topology

The Heimdall Agent runs as a background service on industrial operating systems (Windows 10/11 IoT Enterprise, Beckhoff TwinCAT/BSD, Debian Industrial, Ubuntu Core, Yocto Linux). It bridges the operational technology (OT) domain with the enterprise/cloud management plane.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              INDUSTRIAL EDGE CONTROLLER                                │
│                                                                                        │
│  ┌──────────────────────────────────────────────────────────────────────────────────┐  │
│  │                            Heimdall Edge Agent Core                              │  │
│  │                                                                                  │  │
│  │   ┌─────────────────────┐  ┌──────────────────────┐  ┌────────────────────────┐  │  │
│  │   │  Recipe Engine &    │  │ Multi-Recipe Runtime │  │ Deadband & Delta       │  │  │
│  │   │  Crypto Verifier    │──│ Merger (DAG Engine)  │──│ Evaluator (xxHash64)   │  │  │
│  │   └─────────────────────┘  └──────────┬───────────┘  └───────────┬────────────┘  │  │
│  │                                       │                          │               │  │
│  │                                       ▼                          ▼               │  │
│  │   ┌───────────────────────────────────────────────────────────────────────────┐  │  │
│  │   │                Consolidated Unified Scheduler & Poller                    │  │  │
│  │   └───────────────┬─────────────────────────┬──────────────────────┬──────────┘  │  │
│  │                   │                         │                      │             │  │
│  │                   ▼                         ▼                      ▼             │  │
│  │           [Beckhoff ADS / ECAT]         [OPC UA Client]       [Modbus TCP & CIM] │  │
│  │                                                                                  │  │
│  │   ┌───────────────────────────────────────────────────────────────────────────┐  │  │
│  │   │ 4-Tier Priority Channel Router (P0 Critical -> P3 Bulk)                   │  │  │
│  │   └───────────────────────────────────┬───────────────────────────────────────┘  │  │
│  │                                       ▼                                          │  │
│  │   ┌───────────────────────────────────────────────────────────────────────────┐  │  │
│  │   │ Token Bucket Rate Limiter & Adaptive Zstandard (zstd) Compression         │  │  │
│  │   └───────────────────────────────────┬───────────────────────────────────────┘  │  │
│  │                                       ▼                                          │  │
│  │   ┌───────────────────────────────────────────────────────────────────────────┐  │  │
│  │   │ Local Store-and-Forward Resilient Spooler (SQLite WAL)                    │  │  │
│  │   └───────────────────────────────────┬───────────────────────────────────────┘  │  │
│  └───────────────────────────────────────┼──────────────────────────────────────────┘  │
└──────────────────────────────────────────┼─────────────────────────────────────────────┘
                                           │ TLS 1.3 / mTLS gRPC over HTTP/2
                                           ▼
                                 Heimdall Central Cloud / API
```

---

## 2. Multi-Recipe Merger DAG Algorithm

When multiple operational teams deploy independent declarative recipes to an edge node (e.g., *IT Health*, *Line 1 TwinCAT Quality*, *OP10 Spindle Telemetry*), the runtime merger consolidates them into an optimized Directed Acyclic Graph (DAG) before execution:

1. **Canonical Key Deduplication**:
   - Each data point is hashed to its **Canonical Source Target Key (CSTK)**:
     $$\text{CSTK} = \text{SourceType} : \text{TargetAddress} : \text{Resource}$$
   - Overlapping queries against the same PLC tag, Modbus register, or CIM table map to **1 single physical driver probe**.

2. **Strictest Min-Interval Scheduling**:
   - For $k$ subscriptions on a CSTK with requested intervals $\{T_1, T_2, \dots, T_k\}$, the physical probe executes at:
     $$T_{\text{exec}} = \min(T_1, T_2, \dots, T_k)$$
   - Readings are published to the probe node and decimated/downsampled for subscribers requesting slower rates.

3. **Deepest Scope & Property Union**:
   - Process inspection depth elevates to $\max(\text{Depth}_1, \dots, \text{Depth}_k)$.
   - WQL projected properties merge via set union ($\bigcup \text{Properties}$), executing a single query.

---

## 3. Dynamic Bandwidth Throttling & Priority Egress

To prevent industrial fieldbus or factory LAN congestion:

| Priority Tier | Target Latency | Batching Strategy | Egress Behavior |
| :--- | :--- | :--- | :--- |
| **`P0_CriticalAlarm`** | $< 10\text{ ms}$ | **0 delay** (Immediate) | Emergency stops, EtherCAT link drop, thermal alarms. Bypasses all rate delays. |
| **`P1_HighOperational`** | $< 200\text{ ms}$ | $10\text{ items}$ or $200\text{ ms}$ | Soft-PLC state change (`Run` $\to$ `Stop`), drive error codes. |
| **`P2_MediumMetrics`** | $1\text{ s} - 5\text{ s}$ | $100\text{ items}$ or $2\text{ s}$ | Spindle speed RPM, motor current, pressure telemetry. |
| **`P3_LowInventory`** | $5\text{ m} - 15\text{ m}$ | $1000\text{ items}$ or $5\text{ m}$ | Installed software inventory, SMART disk health, OS patches. |

### Dynamic Alarm Elevation:
If a metric data point configured as `P2_MediumMetrics` (e.g. Bearing Temp) breaches a configured alarm expression (`Value > 85.0`), it is promoted dynamically to **`P0_CriticalAlarm`**, flushing immediately without batch delay.

---

## 4. Local Store-and-Forward Resilience

During factory network outages or maintenance partitions:
- High-priority packets (`P0`/`P1`) are persisted to a zero-allocation local SQLite WAL buffer database.
- When network connectivity is restored, an asynchronous spooling worker drains the persistent buffer using exponential backoff with full jitter:
  $$t_{\text{backoff}} = \min\left(t_{\max}, t_{\text{base}} \cdot 2^{\text{attempt}}\right) + \text{Uniform}(0, \text{Jitter})$$
