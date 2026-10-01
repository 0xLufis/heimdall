# Mobile Application, PWA & Field Hardware Support

This document outlines mobile responsiveness requirements, Progressive Web App (PWA) device integrations, and automated Android packaging for Heimdall field operations.

---

## 1. Core Field Workflows on Mobile Viewports

- [ ] **Maintenance Ticketing (Mandatory)**:
  - Full mobile flow for viewing assigned tickets, changing status (in progress, pending, resolved), reserving tickets, adding notes, and attaching photos.
  - Swipe gestures for common ticket lifecycle transitions.
- [ ] **Asset, Part & Machine Lookup (Mandatory)**:
  - Rapid QR / barcode scan lookup for machines, controllers, and warehouse parts.
  - Compact detail views displaying current telemetry status, installed parts list, and recent maintenance events.
- [ ] **Operational Metrics & Telemetry (Essential)**:
  - Streamlined mobile dashboards displaying key line metrics, downtime counters, and active alarm feeds.
- [ ] **System Preferences & Configuration (Secondary)**:
  - Responsive layout for personal settings, language preference, and notification channels.

---

## 2. Progressive Web App (PWA) Hardware Integration

- [ ] **Camera Barcode & QR Code Scanning**:
  - Native `BarcodeDetector` API and fallback camera stream scanning (`@zxing/library` or modern WASM scanner) with flash/torch control.
  - Audio and haptic feedback upon successful scan in industrial plant environments.
- [ ] **Push Notifications & Service Worker Offline Caching**:
  - Web Push API integration for immediate notifications on ticket assignments, line stoppages, and critical machine alarms.
  - Offline caching of active floor tickets and cached parts catalog for degraded network conditions.

---

## 3. Android Packaging & Containerized Build Pipelines

- [ ] **Automated Android Packaging (TWA / Capacitor / Justfile)**:
  - Configure Trusted Web Activity (TWA) or Capacitor Android wrapper for Heimdall Web.
  - Add Justfile recipes (`just mobile-build`, `just mobile-apk`) for building release-ready unsigned and signed APKs.
- [ ] **Containerized Build Verification**:
  - Provide a containerized build environment containing the Android SDK and Gradle to build APKs reproducibly without local Android Studio installation.
