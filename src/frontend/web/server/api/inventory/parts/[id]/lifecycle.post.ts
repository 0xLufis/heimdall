import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Part ID parameter is required.' })
  }

  const body = (await readBody(event).catch(() => null)) || (event as any)._body || (event.node?.req as any)?.body
  if (!body || !body.toState) {
    throw createError({ statusCode: 400, message: 'Request body must specify "toState".' })
  }

  const actor = body.actor || { id: 'usr-current', name: 'Technician' }

  try {
    const result = partsInventoryStore.transitionLifecycle(
      id,
      body.toState,
      body.options || {},
      actor
    )
    return {
      success: true,
      part: result.part,
      auditRecord: result.auditRecord
    }
  } catch (err: any) {
    throw createError({
      statusCode: 400,
      message: err.message || 'Failed to transition part lifecycle.'
    })
  }
})
