# Frontend UI & UX Engineering Tasks

This document outlines component styling standards, custom interaction patterns, and design consistency requirements across the Heimdall frontend web interface.

---

## 1. Base Element Styling & Design System Consistency

- [x] **Standardized HTML Base Elements**:
    - Unify styling across native form controls (inputs, select dropdowns, checkboxes, radios, range sliders, textareas) using Tailwind semantic tokens.
    - Enforce consistent focus rings (`ring-2 ring-primary/40 focus:outline-none`) and border states across light and dark themes.
    - Ensure uniform corner radii (`rounded-lg` for inputs/buttons, `rounded-2xl` for cards and modals) matching Google Material Design and engineering guidelines.
- [x] **Card & Container Outlines**:
    - Align border radius with outline contours to eliminate clipping or inconsistent border shadows.
    - Provide fill-mode styling variants: `outline`, `semi-filled`, and `solid-filled` across cards and status badges.

---

## 2. Context Menu System Retrofitting

- [x] **Unified Context Menu Architecture**:
    - Retrofit the custom context menu component across data tables, tree nodes, Kanban cards, and machine status tiles.
    - Support keyboard-driven navigation (Arrow keys, Enter, Escape) and right-click positioning with boundary detection.
    - Provide quick actions: Open in new tab, Copy ID/Link, Trigger Maintenance Ticket, Assign Technician, Export Data.
    - Ensure mobile and touch-device fallback via long-press gestures.

---

## 3. Responsive Layout & Accessibility

- [x] **Floor-Ready Responsive Design**:
    - Optimize key operational views for shopfloor tablet displays (iPad / Android tablets) and ruggedized handheld terminals.
    - Maintain minimum touch target size (44x44px) for interactive elements on touch screens.
    - Standardize ARIA roles and labels across all custom interactive widgets.
