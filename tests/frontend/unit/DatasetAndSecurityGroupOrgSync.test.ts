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
    expect(meta.plantName).toBe('Smart Factory Giga-01')
    expect(meta.plantCode).toBe('SF-GIGA-01')
    expect(meta.domain).toBe('factory.corp')
    expect(meta.entraTenantId).toBe('72f988bf-86f1-41af-91ab-2d7cd011db47')
  })

  it('loads predefined enterprise organizations', () => {
    const orgs = getPlantOrganizations()
    expect(orgs.length).toBeGreaterThanOrEqual(4)
    const line06 = orgs.find(o => o.slug === 'line-06-battery-module-line')
    expect(line06).toBeDefined()
    expect(line06?.name).toContain('Line 06')
  })

  it('loads directory users with credentials, roles, and group memberships', () => {
    const users = getPlantUsers()
    expect(users.length).toBeGreaterThanOrEqual(8)

    const sally = users.find(u => u.name === 'Sally Vance')
    expect(sally).toBeDefined()
    expect(sally?.email).toBe('sally.vance@factory.corp')
    expect(sally?.securityGroupIds).toContain('CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp')

    const orwell = users.find(u => u.name === 'George Orwell')
    expect(orwell).toBeDefined()
    expect(orwell?.securityGroupIds).toContain('CN=OT-Maintenance-Technicians,OU=Groups,DC=factory,DC=corp')
  })

  it('loads client PCs with rich CMI / WMI hardware specifications', () => {
    const pcs = getPlantClientPcs()
    expect(pcs.length).toBeGreaterThanOrEqual(10)

    const rob01 = pcs.find(p => p.hostname === 'CPC-L06-ROB-01')
    expect(rob01).toBeDefined()
    expect(rob01?.cmiHardware.cpu.NumberOfCores).toBe(8)
    expect(rob01?.cmiHardware.bios.SerialNumber).toBe('BIOS-L06-ROB-01')
    expect(rob01?.cmiHardware.os.Caption).toContain('Windows 10 IoT')

    const vis01 = pcs.find(p => p.hostname === 'CPC-L09-VIS-01')
    expect(vis01).toBeDefined()
    expect(vis01?.cmiHardware.computerSystem.Manufacturer).toContain('Advantech')
    expect(vis01?.cmiHardware.computerSystem.Model).toContain('MIC-770')
  })

  it('loads active directory OUs partitioned by VLAN', () => {
    const ous = getPlantActiveDirectoryOUs()
    expect(ous.length).toBeGreaterThanOrEqual(6)
    const vlan10 = ous.find(o => o.vlanId === 10)
    expect(vlan10?.name).toBe('Robotics')
    expect(vlan10?.candidateHostnames).toContain('CPC-L06-ROB-01')
  })
})

describe('Better-Auth & Entra ID Security Group Org Governance Suite', () => {
  it('converts organization names to URL-safe kebab-case slugs', () => {
    expect(slugify('Factory Operations')).toBe('factory-operations')
    expect(slugify('Line 06 – Battery Module Line')).toBe('line-06-battery-module-line')
    expect(slugify('Line 09 – Optical Quality Inspection')).toBe('line-09-optical-quality-inspection')
  })

  it('evaluates security group claims and maps to tenant organizations and roles', () => {
    // Sally Vance's group
    const evalSally = evaluateSecurityGroupOrgMapping([
      'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
    ])
    expect(evalSally.matchedGroups.length).toBe(1)
    expect(evalSally.matchedGroups[0].displayName).toBe('On-Prem Controls Engineers')
    expect(evalSally.matchedGroups[0].mappedRole).toBe('controls_engineer')
    expect(evalSally.targetOrganizations.length).toBe(1)
    expect(evalSally.targetOrganizations[0].name).toBe('Line 06 – Battery Module Line')
    expect(evalSally.targetOrganizations[0].role).toBe('admin')
    expect(evalSally.suggestedActiveOrganization).toBe('line-06-battery-module-line')

    // Root Admin group
    const evalAdmin = evaluateSecurityGroupOrgMapping([
      '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c',
    ])
    expect(evalAdmin.matchedGroups.length).toBe(1)
    expect(evalAdmin.matchedGroups[0].mappedRole).toBe('system_admin')
    expect(evalAdmin.targetOrganizations[0].name).toBe('Factory Operations')
    expect(evalAdmin.targetOrganizations[0].role).toBe('owner')
  })

  it('resolves multiple groups into compound organization memberships with highest privileges', () => {
    const evalMulti = evaluateSecurityGroupOrgMapping([
      'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
      'CN=Facility-Shift-Leaders,OU=Groups,DC=factory,DC=corp',
    ])
    expect(evalMulti.matchedGroups.length).toBe(2)
    expect(evalMulti.targetOrganizations.length).toBe(2)
    const orgNames = evalMulti.targetOrganizations.map(o => o.name)
    expect(orgNames).toContain('Line 06 – Battery Module Line')
    expect(orgNames).toContain('Factory Operations')
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
    const syncRes = await syncUserSecurityGroupsToOrganizations('usr-sally-01', [
      'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
    ])
    expect(syncRes.userId).toBe('usr-sally-01')
    expect(syncRes.matchedGroups).toContain('CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp')
    expect(syncRes.enrolledOrganizations.length).toBe(1)
    expect(syncRes.enrolledOrganizations[0].organizationSlug).toBe('line-06-battery-module-line')
    expect(syncRes.activeOrganizationId).toBeDefined()
  })
})
