# Inventory & Asset Provisioning Refinements

This document outlines UI streamlining, serialization workflow adjustments, template improvements, and database performance optimizations for parts and asset management.

---

## 1. UI Streamlining & Non-Serialized Stock Defaults

- [ ] **Default to Non-Serialized Stock Views**:
    - Scale back primary inventory views to aggregate stock counts by default rather than exposing individual serial records upfront.
    - Reserve serialized instance views for physical identification lookups (e.g. IPC, robot controller, camera serials).
- [ ] **Clean & Sleek Workstation UI**:
    - Remove excessive buttons and clutter in `PartProvisioningStation.vue`.
    - Provide a clean, dedicated full-page workstation view rather than cramped modal dialogs.
    - Eliminate the unscalable nested tree widget at line 310 of `PartProvisioningStation.vue`.
- [ ] **Serialize-on-Use Machine Tree UI**:
    - Add explicit item property: `serializeOnUse` (`boolean`).
    - Provide an intuitive visual tree of production lines and machines for provisioning parts and binding serials upon usage.

---

## 2. Asset Templates & Tagging Polish

- [ ] **Collapsed Tree View by Default**:
    - Set template and asset hierarchies to default to a collapsed state for improved navigation.
- [ ] **Fix Field Key Lookups & Tagging**:
    - Fix specification dynamic field key lookups in template schemas.
    - Implement standardized template tagging with default tags and category templates.

---

## 3. Search Integration

- [ ] **OmniSearch Engine Integration**:
    - Connect parts inventory and asset templates directly to the context-driven OmniSearch module.
    - Support direct item and template lookups via tag-based filters (e.g. `tag:ipc`, `manufacturer:Siemens`).

---

## 4. Data Model & Database Performance

- [ ] **Suppliers Entity Table**:
    - Create a dedicated `Suppliers` table / entity tracking vendor details, contact info, and lead times.
- [ ] **Relational Tags Indexing**:
    - Optimize JSONB lookups by introducing a dedicated relational tags table / mapping (`known_tags` / `inventory_item_tags`) to replace slow full JSONB table scans at scale.
