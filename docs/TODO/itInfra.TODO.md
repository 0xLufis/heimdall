# IT Infrastructure Integration & Enterprise Governance

This document defines architectural boundaries and synchronization rules between Heimdall and corporate IT infrastructure (Active Directory, LDAP, PKI, and Automated Ticketing).

---

## 1. Single Source of Truth (SSoT) Directory Synchronization

- [ ] **Strict Read-Only Active Directory & LDAP Operations**:
  - Enforce one-way read-only synchronization from enterprise directories (Active Directory / LDAP / Entra ID).
  - Corporate IT remains the sole Single Source of Truth (SSoT) for security groups, organizational units (OUs), and user accounts.
  - Heimdall shall never attempt to write back, modify, or create security groups or OUs in external directory services.
  - Automatically reconcile local Heimdall role assignments with upstream group memberships during periodic sync cycles.

---

## 2. Infrastructure Incident Auto-Ticketing

- [ ] **Automated Ticket Generation on Infrastructure Alerts**:
  - Automatically generate maintenance tickets when infrastructure thresholds are breached (e.g. edge agent communication loss, controller unreachable, disk spool space critical).
  - Classify auto-generated tickets with appropriate priority, department allocation (`IT` / `Controls`), and affected machine linkages.
  - Prevent duplicate ticket creation during persistent or recurring infrastructure flapping events through deduplication windows.

---

## 3. Client Certificate Authority & Validation Pipeline

- [ ] **X.509 Endpoint Certificate Validation**:
  - Validate edge agent client certificates against the Heimdall CA and configured trusted enterprise roots during TLS / mTLS handshakes.
  - Reject connections from revoked, expired, or untrusted machine certificates and log audit records for unauthorized connection attempts.
  - Provide administrative visibility in the PKI dashboard for certificate thumbprints, issuance dates, and expiration warnings.
