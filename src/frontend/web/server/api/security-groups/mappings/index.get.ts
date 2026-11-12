import { defineEventHandler } from 'h3'
import { getAllSecurityGroupMappings } from '../../../utils/securityGroupMappingsStore'
import { requirePolicy } from '../../../utils/authz'

// Directory-group -> role mappings are IT administration data (same policy as the backend's
// SecurityGroupMappingController). This route used to be anonymous.
export default defineEventHandler(async (event) => {
  await requirePolicy(event, 'ItAdministration')
  return getAllSecurityGroupMappings()
})
