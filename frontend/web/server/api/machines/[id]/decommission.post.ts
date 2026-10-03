import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { machinesStore } from '../../../utils/machinesStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Machine ID parameter is required.' })
  }

  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body
  if (!body) {
    throw createError({ statusCode: 400, message: 'Request body is required.' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Lead Engineer' }

  try {
    const result = machinesStore.decommissionMachine(
      id,
      {
        reason: body.reason,
        targetDisposition: body.targetDisposition || 'decommissioned',
        partsHandling: body.partsHandling || 'salvage_donor',
        decommissionDate: body.decommissionDate
      },
      actor
    )

    return {
      success: true,
      ...result
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to decommission machine.'
    })
  }
})
