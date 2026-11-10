# Automated Device Enrollment & Certificate Issuance Architecture

This document defines the zero-touch registration and automated mTLS certificate provisioning workflow for edge client PCs and industrial controllers.

---

## 1. Enrollment Lifecycle Stages

```
+--------------------+            1. Register with Secret             +-----------------------+
| Newly Installed    | ---------------------------------------------> | Heimdall Backend      |
| Edge Daemon Agent  |                                                | /api/pki/enroll-device|
| (App.Agent.Daemon) | <--------------------------------------------- | Status: PendingApproval
+--------------------+       Enrollment ID: enr-XXXX                  +-----------------------+
                                                                                  |
                                                                                  v
                                                                      +-----------------------+
                                                                      | Admin PKI Dashboard   |
                                                                      | (Single-Click Approve)|
                                                                      +-----------------------+
                                                                                  |
+--------------------+       3. Fetch Issued mTLS Cert & Key          | 2. Generate X.509
| Secure IPC Node    | <============================================= | Client Certificate
| Operational Link   |                                                +-----------------------+
+--------------------+
```

### Stage 1: Initial Registration
1. Upon installation via MSI or systemd, the agent reads its initial configuration:
   - `SERVER_URL`
   - `ENROLLMENT_SECRET` (e.g. one-time factory token)
2. Agent calls `POST /api/pki/enroll-device`:
   - Submits machine hardware invariants (hostname, MAC, CPU ID, OS version).
   - Generates an ephemeral enrollment ID (`enr-XXXX`).

### Stage 2: Administrator Approval
1. In the Heimdall Web PKI Dashboard, the new machine appears in the "Pending Device Approvals" queue.
2. The security administrator clicks "Approve Device".
3. Heimdall CA signs an X.509 client certificate dedicated to the controller:
   - `CN={hostname}.factory.corp`
   - Key usage: Client Authentication (mTLS)
   - Status: Active in PKI store

### Stage 3: Certificate Dispatch
1. The agent polls the enrollment endpoint using its enrollment ID.
2. Upon approval, the signed client certificate is securely dispatched to the edge device.
3. Agent installs certificate into local certificate store or `/etc/heimdall/certs/` and transitions to authenticated mTLS communication.
