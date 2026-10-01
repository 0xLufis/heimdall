# Inventory & Asset Provisioning Implementation Status & Tasks

This document outlines the architecture, completed capabilities, and active engineering tasks for parts inventory, asset templating, and warehouse provisioning.

---

## 1. Parts Inventory (Completed)

- [x] **Tracking**: Supports bulk inventory items and discrete serialized units (`trackingType: 'serialized' | 'bulk'`).
- [x] **Actions**: "Use Part" and "Log Part" workflows implemented across modal dialogs and the dedicated `PartProvisioningStation.vue`.
- [x] **Lifecycle Condition**: Tracks `new`, `used`, `donor`, `obsolete`, `scrap`.
- [x] **Operational State**: Serialized units track `working`, `in_service`, `broken`, `evaluation`.
- [x] **Bulk Intake**: Interface for bulk logging parts visually and via structured JSON (`BulkPartIntakeModal.vue`).
- [x] **Stock Alerts & Quantities**: Dynamic alerts (`optimal`, `low_stock`, `out_of_stock`) with scalar floating buffers (`minQuantityScalar` and `effectiveMinQuantity`).
- [x] **Audit Log**: Immutable chronological audit ledger with cost center details and actor tagging (`PartAuditLogDrawer.vue`).

---

## 2. Asset Inventory Linked to Machines & Child Splitting (Completed)

- [x] Installed production assets linked to machines with child assembly splitting (e.g., IPC controller splitting into sub-assemblies).
- [x] AI Machine Document import accepting ChatGPT JSON and raw tabular BOM text (`MachineDocumentImportModal.vue`).

---

## 3. Revised Asset Templating & OOP Inheritance (Completed)

- [x] **Top-level Categorization**: Hardware and Software classification.
- [x] **Inherited Templates**: Support for `extendsTemplateId` enabling hierarchical blueprint inheritance.
- [x] **Serialized Units as Instances**: Serialized parts instantiate from templates, inheriting fixed specs while providing unique instance fields.
- [x] **Template Schema Builder**: UI modal (`CreateEditTemplateModal.vue`) defining dynamic specifications and instance-specific field schemas.
- [x] **RBAC Permissions**: Configured for `use_part`, `log_part`, `inventory_manager`, plus role hierarchy.
- [x] **Core Required Fields**:
  - `manufacturer` & `supplier`
  - `owner`: organization default with machine/user override
  - `priceEur`: base price in EUR with live multi-currency conversion to HUF, USD, GBP
  - `estimatedResellPriceEur`: instance wear depreciation override math: `basePrice * (1 - wear%)`
  - `quantity`: fixed to 1 for serialized instances
  - `location`: warehouse shelf / cabinet / machine
  - `alternatePartIds`: links to compatible drop-in alternatives
- [x] **Template & Instance Tree**: Visual tree view of templates and serialized children with per-instance wear adjustment (`AssetTemplatesManager.vue`).
- [x] **Identifier Pattern**: Custom user-readable pattern schemes with sequential counter auto-increment (e.g. `IPC-{number}`, `CAM-{number}`).

---

## 4. UI Revision & Mobile-Friendly Workstation (Completed)

- [x] Modern, floor-ready dashboard (`inventory.vue`) with 6 primary workspaces:
  1. Warehouse Inventory
  2. Provisioning Workstation
  3. Asset Templates
  4. Machine Spare Parts & Policies
  5. Production Machines & CAD Hierarchy
  6. Audit Ledger
- [x] **Code Scanner**: Interactive QR / barcode scanner modal (`InventoryCodeScannerModal.vue`) for lookup, intake, and provisioning.
- [x] **Mandatory Cost Center Allocation**: Strict **OR relation** enforced (Production Line OR Project OR Department).

---

## 5. Machine Spare Parts & Policy System (Completed)

- [x] **Spare Parts Policy**: Cross-references installed machine parts with warehouse stock and defined alternative parts.
- [x] **Reporting**: Comprehensive spare parts coverage summary (`covered`, `shortage`, `critical`, `surplus`).
- [x] **Fractional Ratios & Bounds**: Calculates required spares based on active production machines (e.g. 0.25 = 1 spare per 4 machines) with scalar safety buffers and budgetary upper bound overrides (`SparePartsPolicyManager.vue`).

---

## 6. Serialize on Use Functionality

- [ ] **Dynamic Serialization Workflow**:
  - Support converting bulk inventory items (e.g. pneumatic cylinders, cables, relays) into uniquely tracked serialized assets upon assignment to a machine.
  - Automatically generate serial numbers according to template identifier patterns or prompt for physical barcode scan.
  - Decrement bulk quantity in warehouse storage and instantiate a new serialized part record assigned to the target machine.
  - Record the transition in the audit ledger with actor and machine context.

---

## 7. Enhanced Asset Template & Intake UI

- [ ] **Autocomplete & Dynamic Field Indexing**:
  - Implement indexed suggestion dropdowns for key fields (`manufacturer`, `supplier`, `category`, `storage location`, `cost center`).
  - Allow users to either select an existing indexed value from the database or enter a new cleartext entry that auto-indexes for future use.
- [ ] **Post-Intake Part Editing**:
  - Provide a dedicated edit modal / drawer allowing authorized inventory managers to update part metadata (wear level, storage bin, notes, resell value) post-intake.
  - Maintain an audit trail of modified fields.
- [ ] **Specification Fields Rendering on Intake**:
  - Dynamically render template-specific specification fields (`specFields`) directly inside the intake dialog when a template is selected.
  - Validate required specification attributes before completing part intake.

---

## 8. Part Lifecycle & IO Validation Pipeline

- [ ] **End-to-End Lifecycle State Machine**:
  - Enforce formal transitions: `Intake` -> `Warehouse Stock` -> `In-Machine` -> `State Change (Maintenance/Degraded)` -> `Removed from Machine` -> `Decommissioned` -> `Donor` or `Scrap`.
  - Validate state prerequisites (e.g. parts must be removed from a machine before being decommissioned or scrapped).
- [ ] **Strict Machine Linkage & Allocation Justification**:
  - Require either a mandatory indexed machine selection OR a documented cleartext justification when a part is marked as used.
  - Enforce optional WBS (Work Breakdown Structure) element and cost center allocation for financial auditing.
