import { defineEventHandler } from 'h3'
import { listAutomationApiKeys } from '../../../utils/automationApiKeysStore'

export default defineEventHandler(() => {
  return listAutomationApiKeys()
})
