import { defineEventHandler, getRouterParam, getQuery, createError } from 'h3'
import { loadBackendPart, toInventoryPart } from '../../../../utils/inventoryBackend'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, statusMessage: 'Part ID parameter is required.' })
  const currency = (getQuery(event).currency as string) || 'EUR'
  const raw = await loadBackendPart(event, id)
  return { success: true, part: toInventoryPart(raw, currency) }
})
