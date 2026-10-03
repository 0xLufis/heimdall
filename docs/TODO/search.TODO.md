# Omnipresent Tag-Context Search Engine & OmniSearch Architecture

This document defines the requirements for a redesigned, unified tag-and-context-based search system across Heimdall, replacing legacy search components with high-performance indexing, fzf-style fuzzy matching, and context-driven filtering.

---

## 1. Search Architecture & Core Context System

- [ ] **Context-Driven Search Scoping**:
    - Implement a `SearchContext` engine defining which tables, views, or TPT (Table-Per-Type) entity types are queried in each search instance (e.g. `Machine`, `ClientPc`, `Ticket`, `InventoryItem`, `AssetTemplate`, `User`).
    - Dynamically shrink the active context and eliminate incompatible input options based on currently active tags and filters.
    - Funnel disparate search buttons and fragmented page filters across the UI into this unified search component.

- [ ] **Data Sources & Compile-Time/Runtime Indexing**:
    - Support arbitrary search sources: database tables, DB views, and custom JSON / dictionary / map structures.
    - Implement indexing builder: `indexTable(SearchSource source, string indexedKey, string[] columns)` returning structured lookup indices.
    - Support both compile-time / startup indexing and runtime dynamic index extraction.
    - Expose indexed column values to the UI to render available tags per context and available values per tag (`key -> indexed column content`).

---

## 2. Tagging, Inversion, and Fuzzy Completion

- [ ] **Tag and Value Syntax (`tag:value` & `!tag:value`)**:
    - Support explicit tag filtering with key-value pairs (e.g., `type:machine`, `status:active`, `location:Building-A`).
    - Support inverse filtering prefix with `!` (e.g., `!manufacturer:Siemens`, `!status:decommissioned`).
    - Provide an entity indicator badge next to search results displaying their originating table/type.

- [ ] **IDE-Style Code Completion Popup**:
    - Trigger autocomplete suggestions when typing `tag:` or `tag:sub-string` (with empty substring showing all unique values for that key).
    - Enable keyboard arrow navigation (`Up` / `Down` / `Enter` / `Tab`) to select suggestions.
    - Integrate fast `fzf` fuzzy-matching algorithm for substring and token ranking.

---

## 3. UI Display Modes & Omnipresent Integration

- [ ] **Docked & Overlay Display Modes**:
    - **Docked Mode**: Embedded inline in page headers, toolbars, and workstation views.
    - **Overlay Mode**: Modal / Command Palette triggered by keyboard shortcut (`Ctrl+K` or `/`).
    - **Simple Mode**: Streamlined layout for end-users, hide advanced developer filters while retaining tag speed.

- [ ] **Omnipresent Deployment**:
    - Replace broken and spaghetti legacy search code with this unified module across every search instance in Heimdall (Global Header, Inventory, Ticketing, Machine Management, Directory).
