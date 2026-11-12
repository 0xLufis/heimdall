import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { backendFetch } from '../../../../utils/backendFetch'
import { loadBackendPart, toBackendUpdatePayload, toInventoryPart, type BackendInventoryItem } from '../../../../utils/inventoryBackend'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Part ID parameter is required.' })
  const body = await readBody(event).catch(() => null)
  if (!body || typeof body !== 'object') throw createError({ statusCode: 400, statusMessage: 'Request body is required.' })

  const existing = await loadBackendPart(event, id)
  const update = toBackendUpdatePayload(body, !!existing.isStockItem)
  const saved = Object.keys(update).length
    ? await backendFetch<BackendInventoryItem>(event, `inventory/${encodeURIComponent(id)}`, { method: 'PUT', body: update })
    : existing
  return { success: true, part: toInventoryPart(saved) }
})
