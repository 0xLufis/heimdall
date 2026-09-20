import { defineEventHandler } from 'h3'
import { getAllAbsences } from '../../../utils/technicianRulesStore'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const data = await $fetch<any[]>(`${backendBase}/api/v1/technician/absences`, {
      headers: event.headers as any
    })
    if (data && Array.isArray(data) && data.length > 0) {
      return data
    }
  } catch {}

  return getAllAbsences()
})
