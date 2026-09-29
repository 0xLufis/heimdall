import { defineEventHandler } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(() => {
  const logs = partsInventoryStore.getAuditLog()
  return {
    success: true,
    totalCount: logs.length,
    auditLog: logs
  }
})
