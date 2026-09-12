import { describe, it, expect } from 'vitest'
import {
  getEnterpriseDataset,
  getPlantMetadata,
  getPlantOrganizations,
  getPlantSecurityGroups,
  getPlantUsers,
  getPlantClientPcs,
  getPlantActiveDirectoryOUs,
} from '~~/server/utils/datasetLoader'
import {
  slugify,
  evaluateSecurityGroupOrgMapping,
  syncUserSecurityGroupsToOrganizations,
} from '~~/server/utils/securityGroupOrgSync'

describe('Enterprise Plant Dataset Loader Suite', () => {
  it('loads canonical dataset with plant metadata', () => {
    const meta = getPlantMetadata()
    expect(meta.plantName).toBe('Smart Factory Giga-01 (AI Synthetic Facility)')
    expect(meta.plantCode).toBe('SF-GIGA-01')
    expect(meta.domain).toBe('fake-factory.internal')
    expect(meta.entraTenantId).toBe('72f988bf-86f1-41af-91ab-2d7cd011db47')
  })

  it('loads predefined enterprise organizations', () => {
    const orgs = getPlantOrganizations()
    expect(orgs.length).toBeGreaterThanOrEqual(16)
    const line06 = orgs.find(o => o.slug.includes('line-06'))
    expect(line06).toBeDefined()
    expect(line06?.name).toContain('Line 06')
  })

  it('loads directory users with credentials, roles, and group memberships', () => {
    const users = getPlantUsers()
    expect(users.length).toBe(60)

    const synth1 = users.find(u => u.name.includes('Synthetica Botman'))
    expect(synth1).toBeDefined()
    expect(synth1?.email).toBe('synthetica.botman.ai@fake-factory.internal')
    expect(synth1?.securityGroupIds).toContain('CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp')

    const synth2 = users.find(u => u.name.includes('Robo McControlsFace'))
    expect(synth2).toBeDefined()
    expect(synth2?.securityGroupIds).toContain('CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp')
  })

  it('loads client PCs with rich CMI / WMI hardware specifications', () => {
    const pcs = getPlantClientPcs()
    expect(pcs.length).toBe(56)

    const rob01 = pcs.find(p => p.hostname === 'IPC-L06-ROB-ALPHA')
    expect(rob01).toBeDefined()
    expect(rob01?.cmiHardware.cpu.NumberOfCores).toBe(8)
    expect(rob01?.cmiHardware.bios.SerialNumber).toBe('BIOS-IPC-L06-ROB-ALPHA')
    expect(rob01?.cmiHardware.os.Caption).toContain('Windows 10 IoT')

    const ded01 = pcs.find(p => p.hostname === 'IPC-L01-OP030-DEDICATED')
    expect(ded01).toBeDefined()
    expect(ded01?.cmiHardware.computerSystem.Manufacturer).toContain('Beckhoff')
    expect(ded01?.cmiHardware.computerSystem.Model).toContain('C6030')
  })

  it('loads active directory OUs partitioned by VLAN', () => {
    const ous = getPlantActiveDirectoryOUs()
    expect(ous.length).toBeGreaterThanOrEqual(8)
    const vlan10 = ous.find(o => o.vlanId === 10)
    expect(vlan10?.name).toBe('Robotics')
    expect(vlan10?.candidateHostnames).toContain('IPC-L06-ROB-ALPHA')
  })
})

describe('Better-Auth & Entra ID Security Group Org Governance Suite', () => {
  it('converts organization names to URL-safe kebab-case slugs', () => {
    expect(slugify('Factory Operations')).toBe('factory-operations')
    expect(slugify('Line 01 – Synthetic Audi E-Tron Module Line')).toBe('line-01-synthetic-audi-e-tron-module-line')
    expect(slugify('Platform Operations (Synthetic AI Guild)')).toBe('platform-operations-synthetic-ai-guild')
  })

  it('evaluates security group claims and maps to tenant organizations and roles', () => {
    // Line 01 Controls Engineers
    const evalControls = evaluateSecurityGroupOrgMapping([
      'CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp',
    ])
    expect(evalControls.matchedGroups.length).toBe(1)
    expect(evalControls.matchedGroups[0].displayName).toBe('Line 01 Controls Engineers')
    expect(evalControls.matchedGroups[0].mappedRole).toBe('controls_engineer')
    expect(evalControls.targetOrganizations.length).toBe(1)
    expect(evalControls.targetOrganizations[0].name).toContain('Line 01')
    expect(evalControls.targetOrganizations[0].role).toBe('admin')
    expect(evalControls.suggestedActiveOrganization).toContain('line-01')

    // Root Admin group
    const evalAdmin = evaluateSecurityGroupOrgMapping([
      '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c',
    ])
    expect(evalAdmin.matchedGroups.length).toBe(1)
    expect(evalAdmin.matchedGroups[0].mappedRole).toBe('system_admin')
    expect(evalAdmin.targetOrganizations[0].name).toContain('Platform Operations')
    expect(evalAdmin.targetOrganizations[0].role).toBe('owner')
  })

  it('resolves multiple groups into compound organization memberships with highest privileges', () => {
    const evalMulti = evaluateSecurityGroupOrgMapping([
      'CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp',
      'CN=SG-Line02-Assembly,OU=AudiLine02,OU=ProductionLines,DC=factory,DC=corp',
    ])
    expect(evalMulti.matchedGroups.length).toBe(2)
    expect(evalMulti.targetOrganizations.length).toBe(2)
    const orgNames = evalMulti.targetOrganizations.map(o => o.name)
    expect(orgNames.some(n => n.includes('Line 01'))).toBe(true)
    expect(orgNames.some(n => n.includes('Line 02'))).toBe(true)
  })

  it('returns graceful empty result for unknown directory group claims', () => {
    const evalEmpty = evaluateSecurityGroupOrgMapping([
      'CN=Unknown-Group,DC=corp',
      '00000000-0000-0000-0000-000000000000',
    ])
    expect(evalEmpty.matchedGroups.length).toBe(0)
    expect(evalEmpty.targetOrganizations.length).toBe(0)
    expect(evalEmpty.suggestedActiveOrganization).toBeUndefined()
  })

  it('runs syncUserSecurityGroupsToOrganizations and yields enrolled organizations', async () => {
    const syncRes = await syncUserSecurityGroupsToOrganizations('usr-synth-01', [
      'CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp',
    ])
    expect(syncRes.userId).toBe('usr-synth-01')
    expect(syncRes.matchedGroups).toContain('CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp')
    expect(syncRes.enrolledOrganizations.length).toBe(1)
    expect(syncRes.enrolledOrganizations[0].organizationSlug).toContain('line-01')
    expect(syncRes.activeOrganizationId).toBeDefined()
  })
})
