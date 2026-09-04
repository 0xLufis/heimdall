import { createHash } from 'node:crypto'

export interface ClientCertificateEntry {
  id: string
  clientPcId?: string
  commonName: string
  thumbprint: string
  validFrom: string
  validTo: string
  status: 'Active' | 'Revoked' | 'Expired' | 'Superseded'
  createdAt: string
  adOuPath?: string
  profileName?: string
  isRootCa?: boolean
  rawPem?: string
  issuer?: string
  keyAlgorithm?: string
  serialNumber?: string
}

export interface OuCertificateRuleEntry {
  id: string
  ouPath: string
  profileName: string
  validityYears: number
  autoEnroll: boolean
  keyAlgorithm: string
  createdAt: string
  updatedAt: string
}

let activeRootCert: ClientCertificateEntry = {
  id: 'root-ca-default',
  commonName: 'CN=Heimdall Project Industrial Root CA, O=Enterprise Factory Automation, C=US',
  issuer: 'CN=Heimdall Project Industrial Root CA, O=Enterprise Factory Automation, C=US',
  thumbprint: '9E328FB29038234D93C987F02A91D32C192E88AF',
  validFrom: new Date(Date.now() - 30 * 24 * 3600 * 1000).toISOString(),
  validTo: new Date(Date.now() + 3650 * 24 * 3600 * 1000).toISOString(),
  status: 'Active',
  isRootCa: true,
  profileName: 'Root-CA-Profile',
  keyAlgorithm: 'RSA-4096',
  serialNumber: '5A:4F:91:2E:3B:11',
  rawPem: `-----BEGIN CERTIFICATE-----
MIIDXTCCAkWgAwIBAgIUWk+RLjsRAwEQAgEAMA0GCSqGSIb3DQEBCwUAMEUxCzAJ
BgNVBAYTAlVTMScwJQYDVQQKDB5FbnRlcnByaXNlIEZhY3RvcnkgQXV0b21hdGlv
bjETMBEGA1UEAwwKSGVpbWRhbGwgUDEwHhcNMjYwODA1MTIwMDAwWhcNMzYwODAy
-----END CERTIFICATE-----`,
  createdAt: new Date().toISOString(),
}

let ouRulesList: OuCertificateRuleEntry[] = [
  {
    id: 'rule-rob-mtls',
    ouPath: 'OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp',
    profileName: 'High-Assurance-Robotics-mTLS',
    validityYears: 2,
    autoEnroll: true,
    keyAlgorithm: 'RSA-2048',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: 'rule-join-mtls',
    ouPath: 'OU=Fastening,OU=VLAN50-Joining,DC=factory,DC=corp',
    profileName: 'Line-Gateway-Joining-mTLS',
    validityYears: 2,
    autoEnroll: true,
    keyAlgorithm: 'RSA-2048',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
  {
    id: 'rule-aoi-mtls',
    ouPath: 'OU=AOI-Vision,OU=VLAN20-Inspection,DC=factory,DC=corp',
    profileName: 'Vision-Edge-Telemetry-Profile',
    validityYears: 3,
    autoEnroll: true,
    keyAlgorithm: 'ECDSA-P256',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  },
]

let clientCertificatesList: ClientCertificateEntry[] = [
  {
    id: 'cert-01',
    commonName: 'CPC-L06-ROB-01',
    thumbprint: 'A4F2C99B87D10E45E276943C129A88F410294711',
    validFrom: new Date().toISOString(),
    validTo: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString(),
    status: 'Active',
    adOuPath: 'OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp',
    profileName: 'High-Assurance-Robotics-mTLS',
    keyAlgorithm: 'RSA-2048',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'cert-02',
    commonName: 'CPC-L06-SCR-01',
    thumbprint: '77B3EA014F982C9E4D1A650C10B238129C4459AA',
    validFrom: new Date().toISOString(),
    validTo: new Date(Date.now() + 365 * 24 * 3600 * 1000).toISOString(),
    status: 'Active',
    adOuPath: 'OU=Fastening,OU=VLAN50-Joining,DC=factory,DC=corp',
    profileName: 'Line-Gateway-Joining-mTLS',
    keyAlgorithm: 'RSA-2048',
    createdAt: new Date().toISOString(),
  },
]

export function getRootCertificate(): ClientCertificateEntry {
  return JSON.parse(JSON.stringify(activeRootCert))
}

export function importRootCertificate(rawPem: string, profileName?: string): ClientCertificateEntry {
  if (!rawPem || !rawPem.trim()) {
    throw new Error('PEM text is empty.')
  }

  // Compute SHA-1 thumbprint of base64 content
  const cleaned = rawPem
    .replace(/-----BEGIN CERTIFICATE-----/g, '')
    .replace(/-----END CERTIFICATE-----/g, '')
    .replace(/\s+/g, '')

  const hash = createHash('sha1').update(Buffer.from(cleaned, 'base64')).digest('hex').toUpperCase()

  activeRootCert = {
    id: `root-${Date.now()}`,
    commonName: 'Imported Corporate Enterprise Root CA',
    issuer: 'Corporate PKI Services Authority',
    thumbprint: hash,
    validFrom: new Date().toISOString(),
    validTo: new Date(Date.now() + 3650 * 24 * 3600 * 1000).toISOString(),
    status: 'Active',
    isRootCa: true,
    profileName: profileName || 'Imported-Project-Root-CA',
    keyAlgorithm: 'RSA-4096',
    serialNumber: `SN-${Date.now().toString(16).toUpperCase()}`,
    rawPem: rawPem.trim(),
    createdAt: new Date().toISOString(),
  }

  return getRootCertificate()
}

export function getOuRules(): OuCertificateRuleEntry[] {
  return JSON.parse(JSON.stringify(ouRulesList))
}

export function saveOuRule(rule: Partial<OuCertificateRuleEntry> & { ouPath: string; profileName: string }): OuCertificateRuleEntry {
  const existingIndex = rule.id ? ouRulesList.findIndex(r => r.id === rule.id) : -1

  if (existingIndex >= 0) {
    ouRulesList[existingIndex] = {
      ...ouRulesList[existingIndex],
      ...rule,
      updatedAt: new Date().toISOString(),
    }
    return ouRulesList[existingIndex]
  }

  const newRule: OuCertificateRuleEntry = {
    id: rule.id || `rule-${Date.now()}`,
    ouPath: rule.ouPath,
    profileName: rule.profileName,
    validityYears: rule.validityYears || 2,
    autoEnroll: rule.autoEnroll !== false,
    keyAlgorithm: rule.keyAlgorithm || 'RSA-2048',
    createdAt: new Date().toISOString(),
    updatedAt: new Date().toISOString(),
  }

  ouRulesList.push(newRule)
  return newRule
}

export function deleteOuRule(id: string): void {
  ouRulesList = ouRulesList.filter(r => r.id !== id)
}

export function getAllCertificates(): ClientCertificateEntry[] {
  return JSON.parse(JSON.stringify(clientCertificatesList))
}

export function revokeCertificate(id: string): ClientCertificateEntry | null {
  const cert = clientCertificatesList.find(c => c.id === id)
  if (!cert) return null
  cert.status = 'Revoked'
  return cert
}

export function syncOuCertificates(): { syncedCount: number; message: string } {
  let synced = 0
  for (const rule of ouRulesList.filter(r => r.autoEnroll)) {
    // Generate cert for rule if not present
    const existing = clientCertificatesList.find(c => c.adOuPath === rule.ouPath && c.status === 'Active')
    if (!existing) {
      clientCertificatesList.unshift({
        id: `cert-sync-${Date.now()}-${synced}`,
        commonName: `DEVICE-${rule.profileName.split('-')[0].toUpperCase()}-${synced + 1}`,
        thumbprint: createHash('sha1').update(`sync-${rule.ouPath}-${Date.now()}`).digest('hex').toUpperCase(),
        validFrom: new Date().toISOString(),
        validTo: new Date(Date.now() + rule.validityYears * 365 * 24 * 3600 * 1000).toISOString(),
        status: 'Active',
        adOuPath: rule.ouPath,
        profileName: rule.profileName,
        keyAlgorithm: rule.keyAlgorithm,
        createdAt: new Date().toISOString(),
      })
      synced++
    }
  }

  return {
    syncedCount: synced,
    message: `Synchronized certificates across active OU rules. ${synced} certificates provisioned.`,
  }
}
