import { defineEventHandler, getRouterParam, createError } from 'h3'
import { resolveAbsence } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing absence id' })

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    await $fetch(`${backendBase}/api/v1/technician/absences/${id}`, {
      method: 'DELETE',
      headers: event.headers as any
    })
    return { success: true }
  } catch {}

  const success = resolveAbsence(id)
  if (!success) throw createError({ statusCode: 404, message: 'Absence not found' })
  return { success: true }
})
