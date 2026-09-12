import { describe, it, expect } from 'vitest'
import { DEMO_PERSONAS } from '../../../frontend/web/app/composables/useAuthSession'
import {
  getActiveDirectoryOus,
  setOuGovernance,
  getOuGovernances,
  commitImportedHosts
} from '../../../frontend/web/server/utils/activeDirectoryStore'

describe('System Role Expansion & Governance Taxonomy Suite', () => {
  it('defines the 4 specialized system admin personas and separates engineering admin from line management', () => {
    const roles = DEMO_PERSONAS.map(p => p.role)

    // 1. System Admin (God user)
    const sysAdmin = DEMO_PERSONAS.find(p => p.role === 'system_admin')
    expect(sysAdmin).toBeDefined()
    expect(sysAdmin?.description).toContain('Superuser')

    // 2. Heimdall Admin
    const heimdallAdmin = DEMO_PERSONAS.find(p => p.role === 'heimdall_admin')
    expect(heimdallAdmin).toBeDefined()
    expect(heimdallAdmin?.description).toContain('Platform Admin')

    // 3. IT Admin
    const itAdmin = DEMO_PERSONAS.find(p => p.role === 'it_admin')
    expect(itAdmin).toBeDefined()
    expect(itAdmin?.description).toContain('IT Admin')

    // 4. Engineering Admin
    const engAdmin = DEMO_PERSONAS.find(p => p.role === 'engineering_admin')
    expect(engAdmin).toBeDefined()
    expect(engAdmin?.description).toContain('Engineering Admin')

    // Line Management (manager) is distinct from engineering admin
    const plantManager = DEMO_PERSONAS.find(p => p.role === 'manager')
    expect(plantManager).toBeDefined()
    expect(plantManager?.description).toContain('Line Management')
    expect(engAdmin?.role).not.toBe(plantManager?.role)
  })

  it('manages Active Directory OU governance approvals via IT Admin operations', () => {
    const testOu = 'OU=Battery-Assembly,OU=VLAN50-Power,DC=factory,DC=corp'

    // Initial state: unapproved
    setOuGovernance(testOu, 'unapproved', 'it_admin', 'Revoked')
    let ous = getActiveDirectoryOus()
    let match = ous.find(o => o.ouPath.toLowerCase() === testOu.toLowerCase())
    if (!match) {
      // If not in default plant dataset, verify in governance map directly
      const gov = getOuGovernances().find(g => g.ouPath.toLowerCase() === testOu.toLowerCase())
      expect(gov?.isApproved).toBe(false)
      expect(gov?.accessLevel).toBe('unapproved')
    } else {
      expect(match.isApproved).toBe(false)
      expect(match.accessLevel).toBe('unapproved')
    }

    // IT Admin approves for Read-Only
    setOuGovernance(testOu, 'read_only', 'it.admin@factory.corp', 'Discovery and monitoring only')
    ous = getActiveDirectoryOus()
    match = ous.find(o => o.ouPath.toLowerCase() === testOu.toLowerCase())
    if (match) {
      expect(match.isApproved).toBe(true)
      expect(match.accessLevel).toBe('read_only')
      expect(match.approvedBy).toBe('it.admin@factory.corp')
    }

    // IT Admin approves for Read/Write
    setOuGovernance(testOu, 'read_write', 'it.admin@factory.corp', 'Production ingestion authorized')
    ous = getActiveDirectoryOus()
    match = ous.find(o => o.ouPath.toLowerCase() === testOu.toLowerCase())
    if (match) {
      expect(match.isApproved).toBe(true)
      expect(match.accessLevel).toBe('read_write')
    }

    // Verify commitImportedHosts succeeds with read_write approval
    const host = {
      hostname: 'CPC-TEST-BAT-01',
      name: 'Battery Fastener IPC',
      macAddress: 'EE:FF:11:22:33:44',
      ipAddress: '10.50.0.15',
      machineIdentifier: 'HW-BAT-001',
      osVersion: 'Ubuntu 24.04',
      vlanId: 50,
      vlanName: 'VLAN 50',
      subnet: '10.50.0.0/24',
      adOuPath: testOu,
      ouTags: {}
    }
    const result = commitImportedHosts([host])
    expect(result.totalProcessed).toBe(1)
  })
})
