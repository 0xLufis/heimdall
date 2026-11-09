import { defineEventHandler, readBody, createError, setResponseStatus, getRequestHeader } from 'h3'
import { createGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.name) {
    throw createError({ statusCode: 400, message: 'Group name is required' })
  }

  const backendPayload = {
    ...body,
    machineIdsJson: Array.isArray(body.machineIds) ? JSON.stringify(body.machineIds) : (body.machineIdsJson || '[]'),
    machineTypesJson: Array.isArray(body.machineTypes) ? JSON.stringify(body.machineTypes) : (body.machineTypesJson || null)
  }

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const authHeader = getRequestHeader(event, 'authorization')
    const cookieHeader = getRequestHeader(event, 'cookie')
    const headers: Record<string, string> = { 'Content-Type': 'application/json' }
    if (authHeader) headers['authorization'] = authHeader
    if (cookieHeader) headers['cookie'] = cookieHeader

    const created: any = await $fetch(`${backendBase}/api/v1/machinegroup`, {
      method: 'POST',
      body: backendPayload,
      headers,
      timeout: 4000
    })
    setResponseStatus(event, 201)
    return {
      ...created,
      machineIds: created.machineIds || (created.machineIdsJson ? JSON.parse(created.machineIdsJson) : (body.machineIds || [])),
      machineTypes: created.machineTypes || (created.machineTypesJson ? JSON.parse(created.machineTypesJson) : (body.machineTypes || []))
    }
  } catch {}

  const created = createGroup(body)
  setResponseStatus(event, 201)
  return created
})
