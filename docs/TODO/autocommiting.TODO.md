# Automated PLC Project Extraction & Version Control Sync

This document defines the pipeline for pulling active runtime projects from Beckhoff TwinCAT controllers, extracting their boot configurations, and synchronizing changes with Git / Copia Automation repositories.

---

## 1. TwinCAT Runtime Config Extraction (`CurrentConfig.tnzip`)

- [x] **Automated Retrieval from Controller**:
  - Pull `CurrentConfig.tnzip` from the target controller via TwinCAT ADS or secure local file retrieval.
  - Safely decompress the archive and normalize files into standard TwinCAT XAE project directory structures.
  - Filter out temporary runtime artifacts and transient compiler state according to `.gitignore` rules defined in `TWINCAT_COPIA_INTEGRATION.md`.

---

## 2. Branch Rebase & Local Git Tracking

- [x] **Runtime Controller Branch Rebase**:
  - Commit extracted changes locally onto the configured controller runtime branch (e.g. `controller/<hostname>-running`).
  - Perform clean three-way merge or rebase against the upstream line master branch.
  - Record controller hardware metadata, TwinCAT runtime version, and timestamp within the Git commit payload.

---

## 3. Cloud Copia Synchronization & Notifications

- [x] **User-Prompted Copia Push / Sync Notification**:
  - In compliance with Copia ToS (Anti-User-Pooling), stage changes locally and create a dashboard notification for the responsible automation engineer.
  - Enable the engineer to review the visual TwinCAT diff in Heimdall or Copia and trigger the remote push using their authenticated Copia PAT / API key.
