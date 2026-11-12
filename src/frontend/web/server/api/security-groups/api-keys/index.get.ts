import { defineEventHandler } from 'h3'
import { listAutomationApiKeys } from '../../../utils/automationApiKeysStore'
import { requirePolicy } from '../../../utils/authz'

export default defineEventHandler(async (event) => {
  await requirePolicy(event, 'ItAdministration')
  return listAutomationApiKeys()
})
