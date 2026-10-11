import { defineEventHandler } from 'h3'
import { getStoppageStatsFromStore } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5001'
  try {
    const res = await $fetch<any>(`${backendBase}/api/v1/tickets/stoppage-stats`, {
      headers: event.headers as any
    })
    if (res && res.totalStoppageMinutesThisWeek !== undefined) {
      return res
    }
  } catch {
    // Fallback to local store
  }

  return getStoppageStatsFromStore()
})
