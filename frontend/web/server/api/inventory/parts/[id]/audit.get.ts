import { defineEventHandler, getRouterParam } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const id = getRouterParam(event, 'id')
  const logs = partsInventoryStore.getAuditLog(id)
  return {
    success: true,
    partId: id,
    totalCount: logs.length,
    auditLog: logs
  }
})
