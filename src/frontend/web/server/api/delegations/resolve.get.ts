import { defineEventHandler, getQuery, createError } from 'h3'
import { backendFetch } from '../../utils/backendFetch'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const machineId = typeof query.machineId === 'string' ? query.machineId : ''
  if (!machineId) {
    throw createError({ statusCode: 400, statusMessage: 'machineId query parameter is required.' })
  }
  const forwarded: Record<string, string> = {
    discipline: typeof query.discipline === 'string' && query.discipline ? query.discipline : 'General',
    scheduleType: typeof query.scheduleType === 'string' && query.scheduleType ? query.scheduleType : 'Shift'
  }
  if (typeof query.technology === 'string' && query.technology) forwarded.technology = query.technology
  return backendFetch(event, `delegation/resolve/${machineId}`, { query: forwarded })
})
