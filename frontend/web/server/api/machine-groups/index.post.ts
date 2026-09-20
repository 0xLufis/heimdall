import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { createGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body?.name) {
    throw createError({ statusCode: 400, message: 'Group name is required' })
  }

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const created = await $fetch(`${backendBase}/api/v1/machinegroup`, {
      method: 'POST',
      body,
      headers: event.headers as any
    })
    setResponseStatus(event, 201)
    return created
  } catch {}

  const created = createGroup(body)
  setResponseStatus(event, 201)
  return created
})
