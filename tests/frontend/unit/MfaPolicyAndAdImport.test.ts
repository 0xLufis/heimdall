import { describe, it, expect } from 'vitest'
import {
  getMfaPolicy,
  updateMfaPolicy,
  evaluateMfa,
} from '../../../frontend/web/server/utils/mfaPolicyStore'
import {
  getActiveDirectoryOus,
  resolvePattern,
  previewAdImport,
  commitImportedHosts,
} from '../../../frontend/web/server/utils/activeDirectoryStore'
import {
  getRootCertificate,
  importRootCertificate,
  getOuRules,
  saveOuRule,
  deleteOuRule,
  syncOuCertificates,
} from '../../../frontend/web/server/utils/pkiStore'

describe('MFA Policy Governance & Timeout Thresholds', () => {
  it('should evaluate SysAdmin role requiring MFA on every sign-in (always)', () => {
    const res = evaluateMfa({
      role: 'SystemAdministrator',
      lastMfaAt: new Date(Date.now() - 5 * 60 * 1000).toISOString(), // 5 mins ago
    })

    expect(res.mfaRequired).toBe(true)
    expect(res.isExpired).toBe(true)
    expect(res.appliedThreshold).toBe('always')
  })

  it('should enforce weekly (7d) threshold for Engineers: valid within 7d, expired after 7d', () => {
    // Valid within 7 days (e.g. 3 days ago)
    const validRes = evaluateMfa({
      role: 'Engineer',
      lastMfaAt: new Date(Date.now() - 3 * 24 * 3600 * 1000).toISOString(),
    })
    expect(validRes.mfaRequired).toBe(false)
    expect(validRes.isExpired).toBe(false)
    expect(validRes.appliedThreshold).toBe('7d')

    // Expired after 7 days (e.g. 8 days ago)
    const expiredRes = evaluateMfa({
      role: 'Engineer',
      lastMfaAt: new Date(Date.now() - 8 * 24 * 3600 * 1000).toISOString(),
    })
    expect(expiredRes.mfaRequired).toBe(true)
    expect(expiredRes.isExpired).toBe(true)
  })

  it('should enforce monthly (30d) threshold for Technicians: valid within 30d, expired after 30d', () => {
    // Valid within 30 days (e.g. 15 days ago)
    const validRes = evaluateMfa({
      role: 'Technician',
      lastMfaAt: new Date(Date.now() - 15 * 24 * 3600 * 1000).toISOString(),
    })
    expect(validRes.mfaRequired).toBe(false)
    expect(validRes.isExpired).toBe(false)
    expect(validRes.appliedThreshold).toBe('30d')

    // Expired after 30 days (e.g. 32 days ago)
    const expiredRes = evaluateMfa({
      role: 'Technician',
      lastMfaAt: new Date(Date.now() - 32 * 24 * 3600 * 1000).toISOString(),
    })
    expect(expiredRes.mfaRequired).toBe(true)
    expect(expiredRes.isExpired).toBe(true)
  })

  it('should allow forcing MFA for ANY custom security group with custom timeout threshold', () => {
    // Add custom group rule for Quality Assurance
    const current = getMfaPolicy()
    updateMfaPolicy({
      rules: [
        ...current.rules,
        {
          id: 'rule-qa-test',
          targetType: 'group',
          targetName: 'Quality Assurance',
          forceMfa: true,
          timeoutThreshold: 'custom',
          customDays: 3,
        },
      ],
    })

    // User in QA group signed in 4 days ago (> 3 days)
    const res = evaluateMfa({
      role: 'Viewer',
      groups: ['Quality Assurance', 'Auditors'],
      lastMfaAt: new Date(Date.now() - 4 * 24 * 3600 * 1000).toISOString(),
    })

    expect(res.mfaRequired).toBe(true)
    expect(res.isExpired).toBe(true)
    expect(res.matchedRuleTarget).toBe('Quality Assurance')
  })
})

describe('Active Directory VLAN Host Discovery & Templating', () => {
  it('should partition factory OUs by VLAN and report candidate hosts', () => {
    const ous = getActiveDirectoryOus()
    expect(ous.length).toBeGreaterThanOrEqual(4)

    const vlan10 = ous.find(o => o.vlanId === 10)
    expect(vlan10).toBeDefined()
    expect(vlan10?.vlanName).toContain('Production Line')
    expect(vlan10?.subnet).toBe('10.10.10.0/24')
    expect(vlan10?.candidateHosts.length).toBeGreaterThanOrEqual(1)

    const vlan50 = ous.find(o => o.vlanId === 50)
    expect(vlan50).toBeDefined()
    expect(vlan50?.vlanName).toContain('Joining')
  })

  it('should substitute template tokens for Location, Purpose, VLAN, and MachineType', () => {
    const ous = getActiveDirectoryOus()
    const roboticsOu = ous.find(o => o.name === 'Robotics')!
    const host = roboticsOu.candidateHosts[0]

    const pattern = '{HOSTNAME} on {VLAN_NAME} ({LOCATION})'
    const resolved = resolvePattern(pattern, host, roboticsOu)

    expect(resolved).toContain(host.hostname)
    expect(resolved).toContain('VLAN 10 - Production Line')
    expect(resolved).toContain('Line 06 - Hall A')
  })

  it('should generate preview and commit mass import with extracted OU tags', () => {
    const preview = previewAdImport({
      selectedOuPaths: ['OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp'],
      tagTemplates: {
        location: '{LOCATION}',
        purpose: '{PURPOSE}',
        vlan: 'VLAN-{VLAN_ID}',
        machine_type: '{MACHINE_TYPE}',
      },
    })

    expect(preview.totalFound).toBeGreaterThanOrEqual(2)
    const first = preview.preview[0]
    expect(first.vlanId).toBe(10)
    expect(first.ouTags.location).toBe('Line 06 - Hall A')
    expect(first.ouTags.purpose).toContain('Robotic')
    expect(first.ouTags.vlan).toBe('VLAN-10')

    const commitRes = commitImportedHosts(preview.preview)
    expect(commitRes.totalProcessed).toBe(preview.preview.length)
  })

  it('should support dynamic key and value templating in tagRules', () => {
    const preview = previewAdImport({
      selectedOuPaths: ['OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp'],
      tagRules: [
        { keyTemplate: 'zone.{LOCATION}', valueTemplate: '{MACHINE_TYPE}' },
        { keyTemplate: 'net.vlan_{VLAN_ID}', valueTemplate: '{SUBNET}' },
        { keyTemplate: 'workstation.role', valueTemplate: '{PURPOSE}' },
        { keyTemplate: 'fqdn', valueTemplate: '{HOSTNAME}.factory.corp' },
      ],
    })

    expect(preview.totalFound).toBeGreaterThanOrEqual(2)
    const first = preview.preview[0]
    expect(first.ouTags['zone.Line 06 - Hall A']).toBe('Manipulator')
    expect(first.ouTags['net.vlan_10']).toBe('10.10.10.0/24')
    expect(first.ouTags['workstation.role']).toContain('Robotic')
    expect(first.ouTags['fqdn']).toBe(`${first.hostname}.factory.corp`)
  })
})

describe('PKI, Root CA Import & OU Certificate Rules', () => {
  it('should return active Project Root Certificate with valid X.509 thumbprint', () => {
    const root = getRootCertificate()
    expect(root).toBeDefined()
    expect(root.isRootCa).toBe(true)
    expect(root.thumbprint).toBeTruthy()
    expect(root.keyAlgorithm).toBe('RSA-4096')
  })

  it('should import external certificate as Project Root CA', () => {
    const samplePem = `-----BEGIN CERTIFICATE-----
MIIDXTCCAkWgAwIBAgIUWk+RLjsRAwEQAgEAMA0GCSqGSIb3DQEBCwUAMEUxCzAJ
BgNVBAYTAlVTMScwJQYDVQQKDB5FbnRlcnByaXNlIEZhY3RvcnkgQXV0b21hdGlv
bjETMBEGA1UEAwwKSGVpbWRhbGwgUDEwHhcNMjYwODA1MTIwMDAwWhcNMzYwODAy
-----END CERTIFICATE-----`

    const imported = importRootCertificate(samplePem, 'External-Corporate-Root-CA')
    expect(imported).toBeDefined()
    expect(imported.isRootCa).toBe(true)
    expect(imported.profileName).toBe('External-Corporate-Root-CA')
    expect(imported.thumbprint).toBeTruthy()
  })

  it('should manage OU Certificate Assignment Rules and auto-enroll hosts', () => {
    const rulesBefore = getOuRules()
    expect(rulesBefore.length).toBeGreaterThanOrEqual(3)

    const newRule = saveOuRule({
      ouPath: 'OU=Pressing,OU=VLAN15-Stamping,DC=factory,DC=corp',
      profileName: 'Stamping-Press-mTLS',
      validityYears: 3,
      autoEnroll: true,
      keyAlgorithm: 'RSA-4096',
    })

    expect(newRule.id).toBeTruthy()
    expect(newRule.profileName).toBe('Stamping-Press-mTLS')

    const syncRes = syncOuCertificates()
    expect(syncRes.syncedCount).toBeGreaterThanOrEqual(0)

    deleteOuRule(newRule.id)
    const rulesAfter = getOuRules()
    expect(rulesAfter.find(r => r.id === newRule.id)).toBeUndefined()
  })
})
