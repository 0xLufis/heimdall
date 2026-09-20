import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { createRule } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.technicianName || !body.targetId) {
    throw createError({ statusCode: 400, message: 'technicianName and targetId are required' })
  }

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const created = await $fetch(`${backendBase}/api/v1/technician/rules`, {
      method: 'POST',
      body,
      headers: event.headers as any
    })
    setResponseStatus(event, 201)
    return created
  } catch {}

  const created = createRule(body)
  setResponseStatus(event, 201)
  return created
})
