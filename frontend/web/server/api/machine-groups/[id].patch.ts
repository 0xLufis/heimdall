import { defineEventHandler, getRouterParam, readBody, createError } from 'h3'
import { updateGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing group id' })
  const body = await readBody(event)

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const updated = await $fetch(`${backendBase}/api/v1/machinegroup/${id}`, {
      method: 'PATCH',
      body,
      headers: event.headers as any
    })
    return updated
  } catch {}

  const updated = updateGroup(id, body)
  if (!updated) throw createError({ statusCode: 404, message: 'Group not found' })
  return updated
})
