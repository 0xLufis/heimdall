import { getQuery, readBody } from 'h3'
import {
  getEnterpriseDataset,
  getPlantUsers,
  getPlantSecurityGroups,
  getPlantClientPcs,
  getPlantActiveDirectoryOUs
} from '../../utils/datasetLoader'

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path || ''
  const method = event.method
  const dataset = getEnterpriseDataset()

  // 1. Microsoft Graph Mock: /api/ad-mock/v1.0/me/memberOf
  if (path.includes('v1.0/me/memberOf') || path === 'v1.0/me/memberOf') {
    const query = getQuery(event)
    const userId = (query.userId as string) || 'usr-sally-01'
    const user = dataset.users.find(u => u.id === userId || u.email === userId || u.username === userId)
    
    const userGroupIds = user ? user.securityGroupIds : []
    const groups = dataset.securityGroups.filter(g =>
      userGroupIds.some(gid => gid.toLowerCase() === g.groupIdentifier.toLowerCase() || gid.toLowerCase() === g.id.toLowerCase())
    )

    return {
      '@odata.context': 'https://graph.microsoft.com/v1.0/$metadata#directoryObjects',
      value: groups.map(g => ({
        '@odata.type': '#microsoft.graph.group',
        id: g.groupIdentifier.startsWith('CN=') ? g.id : g.groupIdentifier,
        displayName: g.displayName,
        description: `Simulated AD/Entra Group: ${g.displayName}`,
        securityEnabled: true,
        distinguishedName: g.groupIdentifier.startsWith('CN=') ? g.groupIdentifier : undefined
      }))
    }
  }

  // 2. Microsoft Graph Mock: /api/ad-mock/v1.0/users
  if (path.includes('v1.0/users') || path === 'v1.0/users') {
    return {
      '@odata.context': 'https://graph.microsoft.com/v1.0/$metadata#users',
      value: dataset.users.map(u => ({
        id: u.id,
        displayName: u.name,
        userPrincipalName: `${u.username}@${dataset.metadata.domain}`,
        mail: u.email,
        jobTitle: u.jobTitle,
        department: u.department,
        accountEnabled: true,
        assignedRoles: [u.primaryRole],
        securityGroups: u.securityGroupIds
      }))
    }
  }

  // 3. Microsoft Graph Mock: /api/ad-mock/v1.0/groups
  if (path.includes('v1.0/groups') || path === 'v1.0/groups') {
    return {
      '@odata.context': 'https://graph.microsoft.com/v1.0/$metadata#groups',
      value: dataset.securityGroups.map(g => ({
        id: g.id,
        groupIdentifier: g.groupIdentifier,
        displayName: g.displayName,
        identityProvider: g.identityProvider,
        mappedRole: g.mappedRole,
        mappedOrganizationName: g.mappedOrganizationName,
        members: g.memberUserIds
      }))
    }
  }

  // 4. Microsoft Graph Mock: /api/ad-mock/v1.0/devices
  if (path.includes('v1.0/devices') || path === 'v1.0/devices') {
    return {
      '@odata.context': 'https://graph.microsoft.com/v1.0/$metadata#devices',
      value: dataset.clientPcs.map(pc => ({
        id: pc.machineIdentifier,
        displayName: pc.name,
        deviceHostName: pc.hostname,
        operatingSystem: pc.osVersion,
        approximateLastSignInDateTime: new Date().toISOString(),
        isManaged: true,
        trustType: 'ServerAdJoined',
        network: {
          ipAddress: pc.ipAddress,
          macAddress: pc.macAddress,
          vlanId: pc.vlanId
        }
      }))
    }
  }

  // 5. LDAP Query Mock: /api/ad-mock/ldap/search
  if (path.includes('ldap/search') || path === 'ldap/search') {
    let body: any = {}
    if (method === 'POST') {
      try {
        body = await readBody(event)
      } catch {
        body = {}
      }
    }
    const filter = (body.filter || getQuery(event).filter || '').toLowerCase()
    const baseDN = body.baseDN || getQuery(event).baseDN || `DC=factory,DC=corp`

    const entries: Array<{ dn: string; attributes: Record<string, any> }> = []

    // Search OUs
    if (!filter || filter.includes('organizationalunit') || filter.includes('ou')) {
      for (const ou of dataset.activeDirectoryOUs) {
        entries.push({
          dn: ou.ouPath,
          attributes: {
            objectClass: ['top', 'organizationalUnit'],
            ou: ou.name,
            vlanId: ou.vlanId,
            vlanName: ou.vlanName,
            subnet: ou.subnet,
            location: ou.location,
            purpose: ou.purpose,
            machineType: ou.machineType
          }
        })
      }
    }

    // Search Computers
    if (!filter || filter.includes('computer') || filter.includes('device')) {
      for (const pc of dataset.clientPcs) {
        entries.push({
          dn: `CN=${pc.hostname},${pc.adOuPath}`,
          attributes: {
            objectClass: ['top', 'person', 'organizationalPerson', 'user', 'computer'],
            cn: pc.hostname,
            sAMAccountName: `${pc.hostname}$`,
            dNSHostName: `${pc.hostname.toLowerCase()}.${dataset.metadata.domain}`,
            operatingSystem: pc.osVersion,
            operatingSystemVersion: pc.cmiHardware.os.Version,
            networkAddress: pc.ipAddress,
            macAddress: pc.macAddress,
            vlanId: pc.vlanId,
            machineIdentifier: pc.machineIdentifier,
            machineType: pc.machineType
          }
        })
      }
    }

    // Search Users
    if (!filter || filter.includes('user') || filter.includes('person')) {
      for (const u of dataset.users) {
        entries.push({
          dn: `CN=${u.name},OU=Users,${baseDN}`,
          attributes: {
            objectClass: ['top', 'person', 'organizationalPerson', 'user'],
            cn: u.name,
            sAMAccountName: u.username,
            userPrincipalName: `${u.username}@${dataset.metadata.domain}`,
            mail: u.email,
            title: u.jobTitle,
            department: u.department,
            memberOf: u.securityGroupIds
          }
        })
      }
    }

    return {
      baseDN,
      filter: filter || '(objectClass=*)',
      totalEntries: entries.length,
      entries
    }
  }

  // 6. Root / info
  return {
    service: 'Heimdall Active Directory / Entra ID Simulation Provider',
    status: 'online',
    domain: dataset.metadata.domain,
    tenantId: dataset.metadata.entraTenantId,
    endpoints: [
      '/api/ad-mock/v1.0/me/memberOf',
      '/api/ad-mock/v1.0/users',
      '/api/ad-mock/v1.0/groups',
      '/api/ad-mock/v1.0/devices',
      '/api/ad-mock/ldap/search'
    ]
  }
})
