import { defineEventHandler, getQuery } from 'h3'
import { verifyAutomationApiKey } from '../../../../utils/verifyApiKey'
import { getAllSecurityGroupMappings } from '../../../../utils/securityGroupMappingsStore'

export default defineEventHandler((event) => {
  // 1. Authenticate API Key with required scope
  const keyRecord = verifyAutomationApiKey(event, 'security_groups:read')

  // 2. Optional query filters
  const query = getQuery(event)
  let mappings = getAllSecurityGroupMappings()

  if (query.identityProvider && typeof query.identityProvider === 'string') {
    mappings = mappings.filter(
      m => m.identityProvider.toLowerCase() === (query.identityProvider as string).toLowerCase()
    )
  }

  if (query.role && typeof query.role === 'string') {
    mappings = mappings.filter(
      m => m.mappedRole.toLowerCase() === (query.role as string).toLowerCase()
    )
  }

  if (query.organizationId && typeof query.organizationId === 'string') {
    mappings = mappings.filter(
      m => m.organizationId?.toLowerCase() === (query.organizationId as string).toLowerCase()
    )
  }

  return {
    success: true,
    count: mappings.length,
    authenticatedAs: {
      keyId: keyRecord.id,
      name: keyRecord.name,
      systemType: keyRecord.systemType
    },
    data: mappings
  }
})
