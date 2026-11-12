import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { backendFetch } from '../../../../utils/backendFetch'
import { loadBackendPart, requireCostCenter, stockAfterUse, toInventoryPart, type BackendInventoryItem } from '../../../../utils/inventoryBackend'

/**
 * Takes stock out of a bulk part. The backend has no decrement endpoint and no usage ledger:
 * this reads the current stock and writes the new level (`PUT /api/v1/inventory/{id}`), so the
 * cost center is validated but not stored.
 */
export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Part ID parameter is required.' })
  const body = await readBody(event)
  if (!body) throw createError({ statusCode: 400, statusMessage: 'Request body is required.' })

  const costCenter = requireCostCenter(body.costCenter || {
    prodLine: body.prodLine,
    project: body.project,
    department: body.department,
    technician: body.technician,
    notes: body.notes
  })
  const existing = await loadBackendPart(event, id)
  const stockQuantity = stockAfterUse(existing, Number(body.quantity ?? 1))
  const saved = await backendFetch<BackendInventoryItem>(event, `inventory/${encodeURIComponent(id)}`, { method: 'PUT', body: { stockQuantity } })
  return { success: true, part: toInventoryPart(saved), costCenter }
})
