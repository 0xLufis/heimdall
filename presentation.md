---
marp: true
theme: gaia
_class: lead
backgroundColor: #0f172a
color: #f8fafc
transition: slide
---

# **HEIMDALL**
### The Industrial Edge Management Ecosystem
**Comprehensive Project Overview**
*Scalable Monitoring, Spatial Intelligence, & Asset Lifecycle*

---

# **The Industrial Challenge**
### "The Blind Factory Floor"
- **Operational Fragmentation**: Disconnect between physical assets (PLC/CNC) and digital inventory.
- **Scale Complexity**: Managing thousands of edge nodes without a unified control plane.
- **Data Gravity**: High-volume telemetry often leads to system-wide latency and crashes.
- **Spatial Blindness**: Tabular data fails to convey *where* a failure is physically occurring.

---

# **The Heimdall Vision**
### A Unified "Omni-Lens"
Heimdall is a multi-tier ecosystem designed to bridge the gap between physical industrial operations and high-level management.

1.  **The Agent**: Lightweight C# probe for edge telemetry and fieldbus diagnostics.
2.  **The Core**: Web API with Table-per-Type (TPT) inheritance and hybrid caching.
3.  **The Lens**: Web Dashboard with integrated DXF spatial engine and live Kanban ticketing.

---

# **Architecture: The Three Pillars**

### 1. The Edge Agent
- Cross-platform C# service for system health (CPU, RAM, Disk).
- Secure command queuing for remote configuration.
- Real-time heartbeat reporting.

### 2. High-Performance Core
- Optimized LINQ projections for GB-to-KB payload reduction.
- Flexible JSONB metadata for heterogeneous asset types.

---

# **Architecture: The Three Pillars**

### 3. Spatial Intelligence (The Lens)
- **CAD Integration**: Direct DXF-to-SVG rendering engine.
- **Live Anchors**: Dynamic pinning of logical assets to physical CAD coordinates.
- **Interactive Topography**: Click-to-Inspect functionality for floor-level hardware.

---

# **Data Model: TPT Inheritance**
### Structural Integrity meets Flexibility
We utilize **Table-per-Type (TPT)** to model a complex industrial hierarchy:

- **BaseInventoryItem**: Common attributes (Serial, Cost, Org).
- **Machine**: Process stations and station-level assets.
- **ClientPc**: The physical computing nodes (controllers).
- **Hardware/Software**: Nested components (Valves, PLC code, Licenses).

---

# **The Power of Omni-Search**
### Natural Language Infrastructure Querying
Heimdall's search engine supports unified tagging across the entire stack:

- `manufacturer:dell technology:vision`
- `status:offline organization:alpha`
- `type:machine costcenter:eng-4`

*Results are delivered in <200ms across thousands of indexed nodes.*

---

# **Technical Achievements**
- **Payload Optimization**: Reduced search responses from **1.2GB to 134KB**.
- **Concurrency**: Built for high-traffic environments with non-blocking UI patterns.
- **Security**: Multi-tenant isolation using Better-Auth.
- **UX**: Minimalist, industrial-grade dashboard with zero focus-hijacking.

---

# **The Roadmap**
### Phase 2 & Beyond
- **Predictive Maintenance**: Integrating ML models to forecast component failure.
- **Remote Orchestration**: Full-scale remote file deployment and shell execution via the Agent.
- **Digital Twin Sync**: Bi-directional communication with PLC registers (OPC-UA).

---

# **HEIMDALL**
### *Total Visibility. Absolute Control.*

**University MVP Presentation**
[github.com/heimdall-infrastructure]

---
