# Telemetry, Industrial Analytics & KPI Engineering Tasks

This document specifies enhancements for real-time telemetry processing, trend forecasting, KPI widget construction, and unified dashboard visualization.

---

## 1. Predictive Drift & Metric Trends

- [ ] **Rename "Predictive Drift" to "Metric Trends"**:
  - Standardize terminology across UI tabs, API routes, and documentation.
  - Implement rolling statistical trend lines (linear regression, moving averages, standard deviation corridors) over historical time-series data.
- [ ] **Recipe-Based Forecast System**:
  - Enable engineers to define expected operating baselines and tolerance envelopes per product recipe.
  - Compare live machine telemetry against recipe baselines to forecast impending tool wear or process failure.

---

## 2. KPI Builder & Widget Management

- [ ] **Streamlined KPI Builder Interface**:
  - Relocate the widget builder into an on-demand drawer / modal instead of occupying persistent page space.
  - Enable sorting and filtering across existing KPI widgets (by machine, line, priority, author).
  - Add tag-based organization for KPI widgets (`OEE`, `Energy`, `Thermal`, `Hydraulic`).
  - Support configurable time-window aggregations (1 minute, 5 minutes, 1 hour, shift, 24 hours, custom range).

---

## 3. Dynamic Telemetry Data-Point Discovery

- [ ] **Dynamic Data-Point Catalog**:
  - Replace hardcoded telemetry metric pickers with an indexed, dynamic data-point catalog queried from active controller configurations and edge schemas.
  - Expose variable types (Boolean, Int, Float, String), units of measure (°C, bar, RPM, mm/s), and update frequencies.

---

## 4. Streamlined Unified Dashboard Architecture

- [ ] **Single-Page Tabbed Telemetry Hub**:
  - Consolidate telemetry views into a unified single-page layout with tabbed sub-views:
    - `Live Telemetry`: Real-time streaming gauges, charts, and sparklines.
    - `Templates`: Reusable dashboard visualization layouts.
    - `Configurations`: Data-point mapping and polling intervals.
    - `Rules & Alerts`: Threshold rules, hysteresis, and alarm triggers.
- [ ] **Customizable Live Telemetry & Pin-to-Dashboard**:
  - Allow engineers to rearrange, resize, and customize live telemetry widgets.
  - Provide a "Pin to Overview" action to promote critical machine telemetry cards directly to the main system dashboard (`/dashboard/overview`).
