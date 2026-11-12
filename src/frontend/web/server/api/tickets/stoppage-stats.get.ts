import { defineEventHandler } from 'h3'
import { getStoppageStatsFromStore } from '../../utils/ticketsStore'
import { backendAuthHeaders } from '../../utils/backendProxy'

export default defineEventHandler(async (event) => {
  // Backend calls run as the caller: session bearer and the session's organisation, never raw client headers.
  const authHeaders = await backendAuthHeaders(event)
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5001'
  try {
    const res = await $fetch<any>(`${backendBase}/api/v1/tickets/stoppage-stats`, {
      headers: authHeaders
    })
    if (res && res.totalStoppageMinutesThisWeek !== undefined) {
      return res
    }
  } catch {
    // Fallback to local store
  }

  return getStoppageStatsFromStore()
})
