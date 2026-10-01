# Maintenance Ticketing System Tasks & Roadmap

This document outlines UI cleanups, operational workflow refinements, and advanced ticketing features for floor technicians and engineering teams.

---

## 1. UI & Visual Cleanups

- [ ] **Remove Redundant Fleet Sim Button**:
  - Remove the development "Fleet Sim" button from the production ticketing toolbar (retain simulation gating in development if needed via dev settings).
- [ ] **Dedicated Ticket Templates Page View**:
  - Provide a clean dedicated page route or full view for ticket template management instead of relying solely on nested overlay dialogs.
- [ ] **Card Fill-Mode Style Toggle**:
  - Add visual presentation toggle for tickets: `outline`, `semi-filled`, and `filled` card styles to suit ambient lighting conditions on the shop floor.
- [ ] **Mobile-Friendly Workstation Layout**:
  - Optimize ticket lists, status badges, and action menus for touch interactions and narrow mobile viewports.
- [ ] **Border & Rounding Geometry Polish**:
  - Unify outline borders and corner radiuses (`rounded-xl` / `rounded-2xl`) across ticket cards, filters, and modals to eliminate visual clipping.

---

## 2. Functionality & Workflow Enhancements

- [ ] **Ticket Template Editing Interface**:
  - Add full edit capabilities for existing ticket templates (title, default priority, responsible department, description, estimated downtime).
  - Enforce that ticket templates can only instantiate tickets in the `open` state.
- [ ] **Shopfloor TV Monitor Display Mode**:
  - Implement a high-visibility fullscreen monitor dashboard designed for wall-mounted TV displays.
  - Display large typography, active line stoppages, top downtime machines, and currently assigned technicians.
  - Implement automatic view alternation / carousel cycling (e.g. cycle between Active Alarms, Open Tickets, and Weekly Stoppage Metrics every 30 seconds).
- [ ] **Dual Sorting Modes**:
  - Provide a toggle for ticket ordering:
    - `Time First`: Chronological order (oldest open first), then by priority.
    - `Priority First`: Severity order (critical / high / medium / low), then by open duration.
- [ ] **Per-Machine & Line Group Filtering**:
  - Add filtering by machine groupings, production lines, and technician dedication assignments.
- [ ] **Known Ticket Templates Assignment**:
  - Allow administrators to map known ticket templates to specific machine types, technologies (e.g. SMT, Robotics, Fastening), or custom groups.
- [ ] **PLC TwinCAT DUT Error Code Export**:
  - Export ticket template definitions and enumerated error codes into a Beckhoff TwinCAT `.TcDUT` file for automated error reporting directly from PLC logic.
