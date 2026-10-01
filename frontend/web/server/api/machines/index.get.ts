import { defineEventHandler, getQuery } from 'h3'
import { machinesStore } from '../../../utils/machinesStore'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const status = query.status as string | undefined
  const includeDecommissioned = query.includeDecommissioned === 'true' || query.includeDecommissioned === '1'
  const groupId = query.groupId as string | undefined

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const data = await $fetch<any[]>(`${backendBase}/api/v1/machine`, {
      headers: event.headers as any
    })
    if (data && Array.isArray(data) && data.length > 0) {
      return data
    }
  } catch {
    // Backend offline / development mode
  }

  return machinesStore.getAllMachines({
    status,
    includeDecommissioned,
    groupId
  })
})
