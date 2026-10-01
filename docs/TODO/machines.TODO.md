# Production Machine Lifecycle & Status Tracking

This document outlines enhancements to machine lifecycle management, decommissioning workflows, and operational state tracking in Heimdall.

---

## 1. Machine Operational Status Hierarchy

- [x] **Extended Machine Lifecycle States**:
  - Implement explicit operational statuses across machine entities:
    - `active`: Machine is commissioned, online, and participating in active production.
    - `inactive`: Machine is temporarily shut down, idling, or paused for scheduled retooling.
    - `maintenance`: Machine is currently under active maintenance or repair.
    - `decommissioned`: Machine has been permanently retired from the production line.
    - `dismantled` / `taken_apart`: Machine components and sub-assemblies have been salvaged into spare parts inventory.
  - Expose status badges with consistent semantic color coding across machine cards, tables, and tree views.

---

## 2. Machine Decommissioning & Salvage Workflow

- [x] **Decommissioning Action & Dialog**:
  - Add a guided "Decommission Machine" action to the machine detail view and context menu.
  - Require user justification, decommission date, and target archive disposition.
  - Prompt for handling of installed parts:
    - Return to warehouse inventory as `used` or `donor` parts.
    - Mark as `scrap`.
    - Retain with decommissioned machine assembly record.
- [x] **Audit Trail & Historical Reporting**:
  - Archive machine operational history, telemetry logs, and maintenance ticket records upon decommissioning.
  - Ensure decommissioned machines can still be referenced in historical maintenance audits and reports without cluttering active floor dashboards.
