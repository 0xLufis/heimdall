import { defineEventHandler, readBody, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'
import { createBackendPart } from '../../../utils/inventoryIntake'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body || typeof body !== 'object') {
    throw createError({ statusCode: 400, statusMessage: 'Request body is required.' })
  }
  const [manufacturers, suppliers] = await Promise.all([
    backendFetch<Array<{ id: string; name: string }>>(event, 'inventory/manufacturers').catch(() => []),
    backendFetch<Array<{ id: string; name: string }>>(event, 'inventory/suppliers').catch(() => [])
  ])
  const part = await createBackendPart(event, body, { manufacturers, suppliers })
  return { success: true, part }
})
