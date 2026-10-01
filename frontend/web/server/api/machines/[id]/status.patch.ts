import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { machinesStore } from '../../../../utils/machinesStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Machine ID parameter is required.' })
  }

  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body
  if (!body || !body.status) {
    throw createError({ statusCode: 400, message: 'Status is required in request body.' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Engineer' }

  try {
    const updated = machinesStore.updateMachineStatus(id, body.status, actor)
    return {
      success: true,
      machine: updated
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to update machine status.'
    })
  }
})
