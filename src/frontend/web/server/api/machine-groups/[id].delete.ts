import { defineEventHandler, getRouterParam, createError, setResponseStatus } from 'h3'
import { deleteGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing group id' })

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    await $fetch(`${backendBase}/api/v1/machinegroup/${id}`, {
      method: 'DELETE',
      headers: event.headers as any
    })
    deleteGroup(id)
    setResponseStatus(event, 204)
    return null
  } catch {
    // Fall back to local in-memory store if backend is offline or unavailable
    const success = deleteGroup(id)
    if (!success) throw createError({ statusCode: 404, message: 'Group not found' })
    setResponseStatus(event, 204)
    return null
  }
})
