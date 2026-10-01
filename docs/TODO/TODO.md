# Heimdall Core System Roadmap & Engineering Tasks

This document tracks general cross-cutting architectural enhancements, developer tooling improvements, and system-level features for the Heimdall platform.

---

## 1. Developer Tooling & Environment Automation

- [x] **One-Command Dev Setup (`just dev-setup`)**:
  - Automate prerequisite checks (.NET 10 SDK, Bun, Docker, Python 3.12, PostgreSQL client tools).
  - Automate database initialization, migrations, and enterprise plant seeding via a single command.
  - Provide interactive diagnostic checks for local ports (`3000`, `5000`, `5001`, `5432`, `6379`).
- [x] **Optional Dev pgAdmin Profile**:
  - Add optional `pgadmin` service profile to `docker-compose.yml` (`docker compose --profile pgadmin up`).
  - Configure default connection bindings and credentials targeting the local Heimdall development PostgreSQL instance.

---

## 2. Localization & Multi-Language Support (i18n)

- [ ] **Frontend Internationalization Framework**:
  - Integrate `@nuxtjs/i18n` with lightweight translation loaders.
  - Establish standardized locale bundle structure under `locales/` (`en.json`, `hu.json`).
  - Add language switcher toggle in the navigation header and user preferences.
- [ ] **Hungarian Language Support (`hu`)**:
  - Complete Hungarian translations for industrial terminology (machines, production lines, maintenance tickets, telemetry alarms, spare parts inventory).
  - Ensure datetime, currency (EUR / HUF), and numeric formatting adhere to Hungarian and European industrial standards.

---

## 3. Modular Feature Architecture & Dependency Decoupling

- [ ] **Feature Addon Decoupling**:
  - Establish clean dependency boundaries between `Core Platform`, `Maintenance Ticketing`, `Inventory & Asset Provisioning`, and `Industrial Telemetry`.
  - Introduce feature manifest interfaces allowing deployments to run in minimal mode (e.g. Telemetry + Ticketing without Parts Inventory).
  - Document the dependency graph and module interaction contracts.
- [ ] **System Settings Feature Management**:
  - Expose feature activation toggles in `/dashboard/admin/system-settings` under System Governance.
  - Dynamically disable or hide navigation items and API routes based on enabled system modules.

---

## 4. UI Layout & Component Polish

- [ ] **Component Tree Flattening & Layout Responsiveness**:
  - Streamline nested container hierarchies across dashboard manager views (`MachineGroupManager.vue`, `AssetTemplatesManager.vue`).
  - Eliminate excessive DOM wrapper layers to prevent deep component nesting and layout overflow.
  - Ensure right-hand configuration drawers adapt smoothly to diverse viewport widths (from mobile screens to ultra-wide displays).
