import { defineEventHandler, getQuery } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const machineId = query.machineId as string
  const discipline = (query.discipline as string) || 'General'
  const scheduleType = (query.scheduleType as 'Shift' | 'OfficeTime' | 'OnCall') || 'Shift'

  if (!machineId) {
    throw createError({ statusCode: 400, message: 'machineId query parameter is required.' })
  }

  return componentsAndDelegationsStore.resolveResponder(machineId, discipline, scheduleType)
})
