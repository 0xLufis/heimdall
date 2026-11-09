import { defineEventHandler, getRouterParam, readBody, createError, getRequestHeader } from 'h3'
import { updateGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing group id' })
  const body = await readBody(event)

  const backendPayload = {
    ...body,
    machineIdsJson: Array.isArray(body.machineIds) ? JSON.stringify(body.machineIds) : (body.machineIdsJson || undefined),
    machineTypesJson: Array.isArray(body.machineTypes) ? JSON.stringify(body.machineTypes) : (body.machineTypesJson || undefined)
  }

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const authHeader = getRequestHeader(event, 'authorization')
    const cookieHeader = getRequestHeader(event, 'cookie')
    const headers: Record<string, string> = { 'Content-Type': 'application/json' }
    if (authHeader) headers['authorization'] = authHeader
    if (cookieHeader) headers['cookie'] = cookieHeader

    const updated: any = await $fetch(`${backendBase}/api/v1/machinegroup/${id}`, {
      method: 'PUT',
      body: backendPayload,
      headers,
      timeout: 4000
    })
    return {
      ...updated,
      machineIds: updated.machineIds || (updated.machineIdsJson ? JSON.parse(updated.machineIdsJson) : (body.machineIds || [])),
      machineTypes: updated.machineTypes || (updated.machineTypesJson ? JSON.parse(updated.machineTypesJson) : (body.machineTypes || []))
    }
  } catch {}

  const updated = updateGroup(id, body)
  if (!updated) throw createError({ statusCode: 404, message: 'Group not found' })
  return updated
})
