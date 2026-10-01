# Heimdall Edge Agent & Client Daemon Engineering Tasks

This document specifies enhancements for the Heimdall Edge Daemon (`App.Agent.Daemon`), supervisor watchdog resilience, client terminal views, and real-time machine event tracking.

---

## 1. Resilience, Crash Reporting & Supervisor Watchdog

- [ ] **Daemon Supervisor & Watchdog Architecture**:
  - Implement a lightweight watchdog process or layered supervisor to monitor the edge daemon service.
  - Automatically restart the daemon on unexpected exits and generate local post-mortem diagnostic dumps.
  - Deliver graceful crash telemetry back to the Heimdall backend upon link recovery.
- [ ] **System Settings Agent Policy Completion**:
  - Complete backend binding and persistence for all agent runtime policy controls in `/dashboard/admin/system-settings`.
  - Validate remote configuration push, heartbeat intervals, memory limits, and local spool threshold enforcement.

---

## 2. Dedicated Machine Issue-Reporting Web View

- [ ] **Restricted Machine Terminal View**:
  - Implement a lightweight, dedicated web view tailored for production station touchscreens (`/terminal/machine/:id`).
  - Authenticate terminals using client X.509 certificates (or workstation tokens) without requiring human operator credentials.
  - Strictly enforce read-only visibility into machine status and telemetry, with write privileges strictly scoped to ticket submission.
  - Simplify ticket creation to a 2-click process (Preset Category / Fault Code -> Submit) with immediate visual acknowledgment.

---

## 3. File System Monitoring & Recipe Change Events

- [ ] **Production Recipe & Config Change Hook**:
  - Implement a configurable file system watcher in `App.Agent.Daemon` targeting critical production directories (e.g. TwinCAT recipe files, robot point tables, CNC G-code files).
  - Compute cryptographic checksums (SHA-256) on modified files to detect drift or unauthorized changes.
  - Dispatch structured change events (`RecipeChangedEvent`) to the backend via gRPC / SignalR with actor and timestamp context.
