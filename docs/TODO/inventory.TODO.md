# Inventory & Asset Provisioning Refinements

This document outlines UI streamlining, serialization workflow adjustments, template improvements, and database performance optimizations for parts and asset management.

---

## 1. UI Streamlining & Non-Serialized Stock Defaults

- [x] **Default to Non-Serialized Stock Views**:
    - Scale back primary inventory views to aggregate stock counts by default rather than exposing individual serial records upfront.
    - Defaulted `tracking` ref to `'stock'` in `inventory.vue` and `filterTracking` to `'bulk'` in `PartsInventoryList.vue` and `selectedTrackingFilter` to `'bulk'` in `PartProvisioningStation.vue`.
    - Reserve serialized instance views for physical identification lookups (e.g. IPC, robot controller, camera serials).
- [x] **Clean & Sleek Workstation UI**:
    - Streamlined `PartProvisioningStation.vue` into a sleek workstation layout with clear 2-column division and dedicated card workflows.
    - Replaced the unscalable flat 640px list at line 310 of `PartProvisioningStation.vue` with paginated catalog navigation (`paginatedCatalog`, 8 items per page) with Previous/Next controls.
- [x] **Serialize-on-Use Machine Tree UI**:
    - Added explicit item property `serializeOnUse?: boolean` in `inventory.ts`, `partsInventoryStore.ts`, and catalog UI badges.
    - Auto-enables the `Serialize on Use` workflow when selecting a part with `serializeOnUse: true`, prompting for production machine allocation or justification and generating discrete serial numbers.

---

## 2. Asset Templates & Tagging Polish

- [x] **Collapsed Tree View by Default**:
    - Set template and asset hierarchies to default to a collapsed state (`expandedTemplates = ref<Set<string>>(new Set())`) in `AssetTemplatesManager.vue`.
    - Clicking the template header expands the instance tree dynamically.
- [x] **Fix Field Key Lookups & Tagging**:
    - Sanitized dynamic field key lookups (`getValidSpecs`) in template schemas to filter out empty or undefined keys.
    - Added `#tag` badges rendering for `tmpl.tags` in template headers and standardized tag associations.

---

## 3. Search Integration

- [x] **OmniSearch Engine Integration**:
    - Connected parts inventory and asset templates directly to the context-driven OmniSearch module via `INVENTORY_SEARCH_CONTEXT` and `defaultTemplatesSource`.
    - Added OmniSearch vs Classic Search mode toggle in `inventory.vue` with tag-based filtering (`tag:ipc`, `manufacturer:Siemens`, inverted `!tag:value`) and auto-complete.

---

## 4. Data Model & Database Performance

- [x] **Suppliers Entity Table**:
    - Added `Supplier` entity enhancements (`PhoneNumber`, `LeadTimeDays`, `Address`, `Notes`, `CreatedAt`) in `shared/App.Shared/Entities.cs`.
    - Added SQL migration patch to `infra/database/init/02-schema-patches.sh` and live database migration.
    - Added Nitro API endpoints `/api/inventory/suppliers.get` and `/api/inventory/suppliers.post`.
- [x] **Relational Tags Indexing**:
    - Added relational entities `KnownTag` and `InventoryItemTag` in `shared/App.Shared/Entities.cs` and `AppDbContext.cs`.
    - Added `backend.known_tags` and `backend.inventory_item_tags` tables with compound index `(tag_key, tag_value)` in SQL patch script to eliminate slow full JSONB table scans.
    - Added Nitro API endpoint `/api/inventory/tags.get` and store methods in `partsInventoryStore.ts`.

