# Mobile Architecture Evaluation: PWA vs. Native for Heimdall Plant Floor Operations

## 1. Executive Summary & Strategy Recommendation

**Recommended Strategy: PWA-First with Capacitor Hybrid Native Bridge**

Heimdall's primary users on the plant floor are maintenance technicians, automation engineers, and shift operators. Their workflows center around:
1. Incident reporting and real-time maintenance ticketing
2. Direct floor machine and controller QR code scanning for quick ticket pickup and reaction time tracking
3. Looking up machine topology, spare parts, and controllers
4. Inspecting real-time line telemetry, stoppage stats, and KPIs
5. Basic configurations, technician delegation, and offline operations

Based on requirements specified in `docs/TODO/mobile.TODO.md`, the physical hardware demands are strictly limited to:
- **Camera video stream** for Barcode / QR Code scanning
- **Optional Web NFC** (or native NFC plugin) for RFID / NFC tool tag reading
- **System Webview / Chrome Custom Tabs** for single sign-on (SSO) with Microsoft Entra ID or Google Workspace

Building a completely decoupled native application (in Swift, Kotlin, Flutter, or React Native) would introduce **2x-3x maintenance overhead**, dual UI component libraries, duplicated state synchronization logic, and app store deployment friction in closed enterprise OT networks.

Instead, Heimdall’s modern Nuxt 4 + Vue 3 + Tailwind CSS architecture already provides:
- Responsive Shadcn-Vue design tailored for mobile viewports (touch targets, bottom sheets, drawer navigation, pull-to-refresh)
- Zero-dependency client-side SVG QR code generation and HTML5 Barcode/QR scanning (`MediaDevices.getUserMedia` + `BarcodeDetector`)
- IndexedDB offline queueing (`idb`) with automatic re-sync when network connectivity returns
- Service Worker caching and Web App Manifest (`display: standalone`) for one-tap home screen installation

For environments requiring a sideloaded Android `.apk` or MDM (Mobile Device Management) enterprise distribution with native NFC foreground dispatch, **Capacitor** provides a zero-rewrite native container.

---

## 2. Framework Comparison Matrix

| Evaluation Criteria | Nuxt 4 PWA (Current) | Capacitor / Tauri Mobile | Flutter (Dart) | React Native (TS) | Kotlin Multiplatform (KMP) |
|---|---|---|---|---|---|
| **Code Duplication** | **0%** (Unified codebase) | **< 2%** (Lightweight wrapper) | 70% - 100% (New codebase in Dart) | 60% - 80% (Duplicated UI layer) | 50% - 70% (Shared logic, separate UI) |
| **Development Velocity** | **Instant** (Vite HMR, instant web updates) | **Fast** (Web build synced in 1s) | Slow (Dual team, Dart language) | Moderate (React Native bridging) | Slow (Complex toolchain) |
| **Camera / QR Scanning** | Supported (`navigator.mediaDevices` / `BarcodeDetector`) | Native Camera / Barcode plugin | Native `mobile_scanner` | Native `react-native-vision-camera` | Native CameraX |
| **NFC Support** | Web NFC (`NDEFReader`) on Chrome Android | `@capacitor-community/nfc` | `flutter_nfc_kit` | `react-native-nfc-manager` | Android `NfcAdapter` |
| **Enterprise SSO (Android)** | OpenID Connect / OAuth2 via System Browser | Chrome Custom Tabs via `@capacitor/browser` | Custom Tabs / AppAuth-Android | `react-native-app-auth` | AppAuth-Android |
| **OT Network Offline Ops** | Service Worker + IndexedDB | SQLite + IndexedDB | Hive / Isar / SQLite | WatermelonDB / SQLite | SQLDelight |
| **App Store Requirement** | **None** (Instant URL / PWA install) | Optional (Enterprise APK or Store) | Required or manual APK build | Required or manual APK build | Required or manual APK build |

---

## 3. Hardware Integration Details

### 3.1 Camera & Barcode / QR Code Scanning
- **Implementation**: HTML5 `MediaDevices.getUserMedia({ video: { facingMode: 'environment' } })` paired with the standard `BarcodeDetector` API (supported natively on modern Android Chromium) or pure JS fallback.
- **Performance**: Capable of 30-60 FPS real-time QR identification with zero latency.
- **Factory Floor Usage**:
  - Scanning machine action tags (`heimdall://action?action=report-incident&stationId=...`)
  - Ticket pickup tags (`heimdall://ticket/{id}/pickup`) to timestamp physical technician arrival and measure reaction time
  - Parts inventory barcodes/QR codes for instant look-up, logging, and usage deduction.

### 3.2 NFC / RFID Support
- **Android Web NFC (`window.NDEFReader`)**:
  - Supported directly in Android Chrome / Chromium PWA without native compilation.
  - Allows reading standard NDEF records from RFID tool tags, technician smart badges, and physical bins.
- **Capacitor Native NFC Fallback**:
  - If low-level ISO-DEP, Mifare Classic, or background NFC tag scanning is mandated in industrial facilities, `@capacitor-community/nfc` connects to Android's `NfcAdapter.enableForegroundDispatch`.

---

## 4. Single Sign-On (SSO) on Android

Enterprise manufacturing plants enforce corporate identity providers (Microsoft Entra ID / Azure AD, Okta, Ping Identity, or Google Workspace).
- **Architecture**:
  - Authentication in Heimdall is handled by `better-auth` supporting OpenID Connect (OIDC) and SAML 2.0 with PKCE (Proof Key for Code Exchange).
  - In PWA standalone mode on Android, authentication redirects use system Chrome Custom Tabs, preserving existing corporate SSO sessions (e.g. Intune company portal credentials) without requiring re-authentication.
  - Supports biometric authentication (Fingerprint / Face Unlock) via WebAuthn (`navigator.credentials.create` / `get`), enabling passwordless 1-second badge-in on shared floor tablets.

---

## 5. Scope & Work Breakdown for Mobile Core Workflows

| Capability | Requirement Level | Mobile PWA Status |
|---|---|---|
| **Incident Ticketing** | **Must-Have** | Fully functional: `/dashboard/tickets`, `TicketPersonalHeader.vue`, `TicketDetailDrawer.vue`, `TicketCreateModal.vue`. Mobile drawer with touch dismiss and QR pickup. |
| **Machine & Parts Lookup** | **Must-Have** | Fully functional: `/mobile/scan`, `/dashboard/inventory`, `SearchableTargetCombobox.vue`, instant camera QR/barcode scanner with omni-search. |
| **Plant Metrics & Monitor** | **Must-Have** | Responsive: `TicketMetricsOverview.vue`, weekly stoppage analytics, top worst machines, line stoppage status. |
| **Technician Delegation** | **Should-Have** | Responsive: `/dashboard/delegations`, shift roster, backup engineer re-routing. |
| **Configuration & Settings** | **Could-Have** | Responsive: User preferences, start mode configuration ("Remote Start Allowed" vs "QR-Only Start Required"). |
| **Parts Intake & Use-Part** | **Must-Have** | Revamped in inventory suite: `/dashboard/inventory/parts`, quick use-part modal with mandatory cost center fields, visual bulk intake. |

---

## 6. Conclusion

For Heimdall, **investing in a separate native application codebase would be wasteful and counter-productive**. The PWA architecture satisfies 100% of the industrial requirements with camera QR scanning, Android SSO, offline queueing, and responsive touch controls. If an enterprise customer mandates an installable `.apk` package via Microsoft Intune or SOTI MobiControl, a lightweight Capacitor container can wrap the Nuxt 4 build in under 30 minutes without modifying application logic.
