import { defineEventHandler } from 'h3'
import { getAllSecurityGroupMappings } from '../../../utils/securityGroupMappingsStore'

export default defineEventHandler(() => {
  return getAllSecurityGroupMappings()
})
