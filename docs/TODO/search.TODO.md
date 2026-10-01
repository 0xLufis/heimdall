# OmniSearch Architecture & UI Refinement

This document outlines the redesign, stability fixes, and UX improvements for the global search system (`OmniSearch`), taking architectural inspiration from Neovim's `telescope.nvim`.

---

## 1. Interaction Model & Stability Refinements

- [x] **Elimination of Jitter & Premature Modal Close**:
  - Eliminate layout jumping caused by asynchronous result height shifts and unbuffered search results.
  - Fix focus loss and accidental dropdown dismissal during rapid typing or filter navigation.
  - Enforce smooth debouncing (150ms) with instant local cache lookup.
- [x] **Predictable Keyboard Navigation (`telescope.nvim` Style)**:
  - Implement full keyboard control:
    - `Ctrl+j` / `Down Arrow`: Navigate down result list.
    - `Ctrl+k` / `Up Arrow`: Navigate up result list.
    - `Tab` / `Shift+Tab`: Cycle through category filters.
    - `Enter`: Activate selected result.
    - `Ctrl+v` / `Ctrl+x`: Open result in split view or new tab.
    - `Esc`: Close modal or clear query.
  - Highlight matching fuzzy substrings within search results in real time.

---

## 2. Advanced Search Menu & Split Preview

- [x] **Streamlined UI with Advanced Filter Menu**:
  - Provide a clean, minimal search bar by default with an expandable drawer or hotkey (`Ctrl+f`) for advanced scoping.
  - Filter scoping tags: `@machine`, `#ticket`, `!controller`, `*part`, `%alarm`.
  - Dedicated side-by-side preview panel showing entity details (e.g. machine live status, ticket description, part inventory count) without navigating away from search results.
