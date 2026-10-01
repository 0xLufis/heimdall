# Packaging, Distribution & Deployment Automation

This document specifies package targets, enterprise deployment installers, and platform support guidelines across Heimdall subsystems.

---

## 1. Backend Server Distribution

- [ ] **Cross-Platform Signed Executables**:
  - Produce Authenticode-signed standalone binaries for Windows (`heimdall-backend.exe`) and signed Linux binaries (`heimdall-backend`).
  - Standardize configuration file search paths across platforms (`/etc/heimdall/` on Linux, `%ProgramData%\Heimdall\` on Windows).
- [ ] **Windows MSI Installer**:
  - Author a WiX-based MSI installer for Heimdall Backend Server, configuring Windows Service registration, port reservation, and firewall rules.
- [ ] **Linux & Container Deployments**:
  - Maintain production multi-stage Docker images (`docker-compose.prod.yml`).
  - Provide systemd service unit files and system packages (.deb / .rpm) for bare-metal Linux servers.

---

## 2. Edge Agent Packaging & Enterprise MDM

- [ ] **Enterprise Endpoint Management (MDM) Deployment**:
  - Package `App.Agent.Daemon` as an MSI installer optimized for silent deployment via Microsoft Endpoint Configuration Manager (Software Center) and Microsoft Intune.
  - Support command-line installation arguments (`/quiet`, `SERVER_URL=...`, `ENROLLMENT_TOKEN=...`).
- [ ] **Linux Packages**:
  - Provide distribution-ready PKGBUILD for Arch Linux, Debian `.deb`, and standalone tarballs (`dist/heimdall-agent-linux-x64.tar.gz`).
- [ ] **Automated Device Enrollment & Certificate Issuance**:
  - Implement a streamlined initial enrollment workflow where newly installed agents register with an initial enrollment secret.
  - Allow administrators in the PKI dashboard to approve new machines with a single click, automatically generating and dispatching unique mTLS client certificates.

---

## 3. Web Application Deployment & Redundancy

- [ ] **Standalone Production Container**:
  - Maintain standalone, unprivileged multi-stage Docker build for the Nuxt 4 / Nitro frontend server.
  - Ensure zero runtime development dependencies and minimal image footprint (< 120 MB).
- [ ] **High Availability & Horizontal Scalability**:
  - Document stateless frontend operation behind reverse proxies (Nginx / Traefik / HAProxy) with session sharing via Redis.
  - Implement health check and readiness endpoints (`/api/health`, `/api/ready`) for Kubernetes and container orchestration.
