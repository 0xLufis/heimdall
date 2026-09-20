import { defineEventHandler } from 'h3'
import { getAllGroups } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const data = await $fetch<any[]>(`${backendBase}/api/v1/machinegroup`, {
      headers: event.headers as any
    })
    if (data && Array.isArray(data) && data.length > 0) {
      return data
    }
  } catch {}

  return getAllGroups()
})
