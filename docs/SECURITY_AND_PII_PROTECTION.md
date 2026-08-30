# Heimdall Security & PII Protection Manual (TISAX VDA ISA 6.0 & GDPR)

This document specifies the security controls, cryptographic storage schemes, and strict PII/personal data exclusion policies implemented across the Heimdall Edge Agent.

---

## 1. Compliance Mapping

| Standard / Framework | Requirement | Heimdall Agent Control |
| :--- | :--- | :--- |
| **TISAX (VDA ISA 6.0)** | High Protection (AL3) for Operational & Prototype Data | Cryptographic signing of recipes, TLS 1.3 / mTLS gRPC, hardware-bound AES-256-GCM config encryption. |
| **GDPR (EU 2016/679)** | Privacy by Design & Zero Personal Data Ingestion | Strict directory blacklist (browsers, cookies, user personal files) + regex scrubbing of user names in file paths. |
| **IEC 62443-4-2** | Component Security Assurance | Authenticity verification of incoming commands and signed configuration updates. |

---

## 2. Strict PII & Personal Data Exclusion Engine

### 2.1 Directory Blacklist (Zero-Traverse Policy)
The recursive file scanner automatically prunes and refuses to enter any of the following directory patterns:
- `AppData/Local/Google/Chrome`
- `AppData/Local/Microsoft/Edge`
- `AppData/Roaming/Mozilla`
- `AppData/Local/BraveSoftware`
- `AppData/Local/Opera Software`
- `.config/google-chrome`, `.config/chromium`, `.mozilla`
- `Cookies`, `History`, `Login Data`, `Web Data`, `Sessions`
- `Windows/WinSxS`, `Windows/System32/config`
- `Users/Default`, `node_modules`, `.git`

### 2.2 Sensitive File Blacklist (Zero-Read Policy)
- `NTUSER.DAT*`, `UsrClass.dat*`
- `.bash_history`, `.zsh_history`, `.history`
- SSH keys: `id_rsa`, `id_ed25519`, `id_ecdsa`, `known_hosts`
- System hives: `sam`, `security`, `system`
- Swap & memory dumps: `pagefile.sys`, `hiberfil.sys`, `swapfile.sys`

### 2.3 Process Argument Secret & PII Scrubber
The `ProcessSecretScrubber` sanitizes all running process command-line strings:
- **Credentials**: Masks flags matching `--password`, `--token`, `--secret`, `--apikey`, `connectionstring` with `[REDACTED]`.
- **JWTs**: Matches and redacts base64-encoded bearer tokens with `[REDACTED_JWT]`.
- **User Account Names**: Replaces `/home/username/` or `C:\Users\username\` with `[USER_ACCOUNT]`.

---

## 3. Hardware-Bound Config Encryption

### 3.1 Windows DPAPI
Uses `ProtectedData.Protect` under `DataProtectionScope.LocalMachine` combined with application-unique secondary entropy (`Heimdall.OT.Agent.Entropy.v1`).

### 3.2 Linux / Cross-Platform AES-256-GCM Envelope Encryption
Uses authenticated encryption (AEAD) with a 256-bit key derived via **HKDF-SHA256**:
$$\text{IKM} = \text{MachineID} \mathbin{\Vert} \text{LocalMasterSeed}$$
Where:
- `MachineID` is read from `/etc/machine-id` (or Windows `MachineGuid`).
- `LocalMasterSeed` is a 32-byte cryptographically secure random seed stored with `0600` permissions (`/etc/heimdall/master.seed`).
- Format: `[Version (1B)] || [Nonce (12B)] || [Tag (16B)] || [Ciphertext (NB)]`.
