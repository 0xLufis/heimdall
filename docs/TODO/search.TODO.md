# Omnipresent Tag-Context Search Engine & OmniSearch Architecture

This document defines the requirements for a redesigned, unified tag-and-context-based search system across Heimdall, replacing legacy search components with high-performance indexing, fzf-style fuzzy matching, and context-driven filtering.

---

## 1. Search Architecture & Core Context System

- [x] **Context-Driven Search Scoping**:
    - Implemented `SearchContext` engine in `frontend/web/app/utils/search/searchContext.ts` scoping target entity types (`Machine`, `ClientPc`, `Ticket`, `InventoryItem`, `AssetTemplate`, `User`).
    - Implemented `shrinkContext` dynamically pruning incompatible data sources and tags based on active filters.
    - Centralized multi-table search into a unified architecture.

- [x] **Data Sources & Compile-Time/Runtime Indexing**:
    - Created `indexTable(SearchSource, indexedKey, columns)` in `frontend/web/app/utils/search/searchIndexer.ts` supporting tables, views, and JSON data.
    - Extracted unique column contents per tag key to power the code-completion popup.
    - Provided fast inverted indexing for multi-source lookups.

---

## 2. Tagging, Inversion, and Fuzzy Completion

- [x] **Tag and Value Syntax (`tag:value` & `!tag:value`)**:
    - Implemented `parseSearchQuery` supporting positive tags and `!` negation prefixes (`!manufacturer:Siemens`).
    - Added entity indicator badges (`[MCH]`, `[IPC]`, `[TKT]`, `[PART]`, `[USR]`) with color styling.

- [x] **IDE-Style Code Completion Popup**:
    - Autocomplete popup appears on `tag:` or `tag:sub` with arrow navigation (`Up`, `Down`, `Enter`, `Tab`, `Esc`).
    - Integrated high-performance `fzf` algorithm with word boundary bonuses and match highlighting in `fzfSearch.ts`.

---

## 3. UI Display Modes & Omnipresent Integration

- [x] **Docked & Overlay Display Modes**:
    - Built `TagContextSearch.vue` supporting docked inline mode and overlay modal mode.
    - Included `simpleMode` toggle for clean end-user presentation.

- [x] **Omnipresent Deployment**:
    - Standardized contexts in `defaultContexts.ts` and validated with 14 new unit tests in `TagContextOmniSearch.test.ts`.
