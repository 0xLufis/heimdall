import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { backendFetch } from '../../../utils/backendFetch'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Machine ID parameter is required.' })
  }

  const body = await readBody(event).catch(() => null)
  if (!body || typeof body.status !== 'string' || !body.status) {
    throw createError({ statusCode: 400, statusMessage: 'Status is required in request body.' })
  }

  // The acting user is derived from the session by the backend; client-supplied "actor" is ignored.
  const machine = await backendFetch(event, `machine/${id}/status`, {
    method: 'PUT',
    body: { status: body.status }
  })
  return { success: true, machine }
})
