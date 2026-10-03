# Heimdall Mobile Application, PWA & Android Packaging Guide

This document defines the Progressive Web App (PWA) configuration, Trusted Web Activity (TWA) / Capacitor Android wrapper setup, and containerized build pipelines for generating release-ready APKs.

---

## 1. PWA & Field Hardware Capabilities

Heimdall Web operates as an installable Progressive Web Application (PWA) optimized for mobile viewports, industrial tablets, and ruggedized handheld scanners (e.g. Zebra TC52 / Honeywell ScanPal).

### Key Mobile Capabilities:
- **Offline Caching**: Service worker precaches active floor tickets and spare parts catalogs for operation in Wi-Fi blackspots.
- **Hardware Scanning**: Utilizes the native browser `BarcodeDetector` API with fallback to WASM scanning, providing camera torch control, 880Hz audio feedback, and haptic vibration.
- **Touch Workflows**: Swipe-right gestures advance maintenance ticket lifecycle (`Open` -> `InProgress` -> `Resolved`), swipe-left marks tickets `Pending`.
- **Web Push API**: Instant alerts for emergency line stoppages, critical robot alarms, and technician ticket assignments.

---

## 2. Android Wrapper Options: TWA vs. Capacitor

### Option A: Trusted Web Activity (TWA) with Bubblewrap (Recommended for Pure Web)
A Trusted Web Activity opens the Heimdall PWA inside an Android app shell using Chrome Custom Tabs without URL bars or browser controls.
- **Prerequisites**: `bubblewrap` CLI or Node package `@bubblewrap/cli`.
- **Digital Asset Links**: Requires `/.well-known/assetlinks.json` hosted on the Heimdall server matching the Android signing key SHA-256 fingerprint.

### Option B: Capacitor Wrapper (Recommended for Deep Hardware Integration)
Capacitor wraps the web assets inside a native Android Studio project:
```bash
npm install @capacitor/core @capacitor/android
npx cap init "Heimdall Field" "io.heimdall.field"
npx cap add android
```

---

## 3. Justfile Automation Recipes

The following recipes are provided in the root `justfile`:

```just
# Build the mobile PWA frontend bundle
mobile-build:
    cd frontend/web && npm run build

# Package Android APK via containerized Gradle environment
mobile-apk:
    docker build -t heimdall-android-builder -f deployment/android/Dockerfile.android-build .
    docker run --rm -v $(pwd)/dist/mobile:/out heimdall-android-builder cp /app/app-release-unsigned.apk /out/
```

---

## 4. Containerized Build Verification

The containerized Android build environment (`deployment/android/Dockerfile.android-build`) encapsulates:
- Eclipse Temurin JDK 17
- Android SDK Command-line Tools (cmdline-tools 11.0)
- Android API Level 34 (Android 14) and Build-Tools 34.0.0
- Gradle 8.5

This eliminates the need for field engineers or CI runners to maintain local Android Studio installations.
