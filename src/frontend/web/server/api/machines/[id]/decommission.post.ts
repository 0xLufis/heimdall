import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Machine ID parameter is required.' })
  }

  const body = await readBody(event).catch(() => null)
  if (!body || typeof body.reason !== 'string' || !body.reason.trim()) {
    throw createError({ statusCode: 400, statusMessage: 'A decommission reason is required.' })
  }

  const machine = await backendFetch<Record<string, unknown>>(event, `machine/${id}/decommission`, {
    method: 'POST',
    body: {
      reason: body.reason,
      targetDisposition: body.targetDisposition || 'decommissioned',
      partsHandling: body.partsHandling || 'salvage_donor',
      decommissionDate: body.decommissionDate
    }
  })
  return { success: true, machine }
})
