import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'
import { createBackendPart } from '../../../utils/inventoryIntake'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body) throw createError({ statusCode: 400, statusMessage: 'Request body is required.' })

  const items = Array.isArray(body) ? body : (body.items || body.parts)
  if (!Array.isArray(items) || items.length === 0) {
    throw createError({ statusCode: 400, statusMessage: 'An array of parts is required for bulk logging.' })
  }

  const [manufacturers, suppliers] = await Promise.all([
    backendFetch<Array<{ id: string; name: string }>>(event, 'inventory/manufacturers').catch(() => []),
    backendFetch<Array<{ id: string; name: string }>>(event, 'inventory/suppliers').catch(() => [])
  ])
  // Sequential: the backend has no batch endpoint, and a failure should stop at the failing row.
  const created = []
  for (const item of items) created.push(await createBackendPart(event, item, { manufacturers, suppliers }))
  return { success: true, count: created.length, parts: created }
})
