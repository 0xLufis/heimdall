import { describe, it, expect } from 'vitest'
import { generateQrMatrix, generateQrSvg, generateQrDataUrl } from '~/utils/qrSvgRenderer'
import { getEnterpriseDataset } from '~~/server/utils/datasetLoader'

describe('Pure SVG QR Code Renderer Suite', () => {
  it('generates non-empty QR matrix with corner finder patterns', () => {
    const matrix = generateQrMatrix('heimdall://report-incident?stationId=STATION-01')
    expect(matrix.length).toBeGreaterThanOrEqual(21)
    expect(matrix[0].length).toBe(matrix.length)

    // Top-left finder pattern corner should be true
    expect(matrix[0][0]).toBe(true)
    expect(matrix[0][6]).toBe(true)
    expect(matrix[6][0]).toBe(true)
    expect(matrix[6][6]).toBe(true)
    // Center of finder pattern
    expect(matrix[3][3]).toBe(true)
  })

  it('generates valid SVG string with viewBox and dark module paths', () => {
    const svg = generateQrSvg('http://localhost:3000/dashboard/tickets?action=report-incident', {
      width: 280,
      darkColor: '#0f172a',
      lightColor: '#ffffff'
    })

    expect(svg).toContain('<svg xmlns="http://www.w3.org/2000/svg"')
    expect(svg).toContain('viewBox="0 0')
    expect(svg).toContain('width="280"')
    expect(svg).toContain('height="280"')
    expect(svg).toContain('<rect width="100%" height="100%" fill="#ffffff"/>')
    expect(svg).toContain('<path d="')
    expect(svg).toContain('fill="#0f172a"')
  })

  it('generates encoded SVG data URI for <img> tags', () => {
    const dataUrl = generateQrDataUrl('heimdall://inspect-machine?stationId=OP10')
    expect(dataUrl.startsWith('data:image/svg+xml;utf8,')).toBe(true)
    expect(dataUrl).toContain('%3Csvg')
    expect(dataUrl).toContain('%3C%2Fsvg%3E')
  })
})

describe('Mock Active Directory & Entra ID Graph Suite', () => {
  const dataset = getEnterpriseDataset()

  it('resolves directory security groups for Synthetica Botman', () => {
    const synth = dataset.users.find(u => u.name.includes('Synthetica Botman'))!
    expect(synth).toBeDefined()
    expect(synth.email).toBe('synthetica.botman.ai@fake-factory.internal')
    const synthGroups = dataset.securityGroups.filter(g =>
      synth.securityGroupIds.includes(g.groupIdentifier)
    )
    expect(synthGroups.length).toBeGreaterThanOrEqual(1)
    expect(synthGroups[0].displayName).toBe('Line 01 Controls Engineers')
  })

  it('provides Microsoft Graph user representations with valid UPN', () => {
    const graphUsers = dataset.users.map(u => ({
      id: u.id,
      displayName: u.name,
      userPrincipalName: `${u.username}@${dataset.metadata.domain}`,
      mail: u.email,
      department: u.department,
      accountEnabled: true
    }))

    expect(graphUsers.length).toBeGreaterThanOrEqual(8)
    const admin = graphUsers.find(u => u.displayName.includes('Dr. Algorithmus Prime'))
    expect(admin?.userPrincipalName).toBe('dr.algorithmus.prime.ai@fake-factory.internal')
    expect(admin?.mail).toBe('dr.algorithmus.prime.ai@fake-factory.internal')
  })

  it('provides Microsoft Graph computer device representations', () => {
    const devices = dataset.clientPcs.map(pc => ({
      id: pc.machineIdentifier,
      displayName: pc.name,
      deviceHostName: pc.hostname,
      operatingSystem: pc.osVersion,
      trustType: 'ServerAdJoined',
      network: {
        ipAddress: pc.ipAddress,
        macAddress: pc.macAddress,
        vlanId: pc.vlanId
      }
    }))

    expect(devices.length).toBeGreaterThanOrEqual(10)
    const edgePc = devices.find(d => d.deviceHostName === 'IPC-L01-OP030-DEDICATED')
    expect(edgePc).toBeDefined()
    expect(edgePc?.network.vlanId).toBe(101)
    expect(edgePc?.trustType).toBe('ServerAdJoined')
  })

  it('supports LDAP search simulation by objectClass and baseDN', () => {
    const baseDN = 'DC=factory,DC=corp'
    const ldapOus = dataset.activeDirectoryOUs.map(ou => ({
      dn: ou.ouPath,
      objectClass: ['top', 'organizationalUnit'],
      ou: ou.name,
      vlanId: ou.vlanId
    }))

    expect(ldapOus.length).toBeGreaterThanOrEqual(6)
    const robotics = ldapOus.find(o => o.ou === 'Robotics')
    expect(robotics?.dn).toContain('DC=factory,DC=corp')
    expect(robotics?.vlanId).toBe(10)
  })
})
