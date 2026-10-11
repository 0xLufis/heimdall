import { defineEventHandler, getRouterParam } from 'h3'
import { componentsAndDelegationsStore } from '../../../utils/componentsAndDelegationsStore'

export default defineEventHandler((event) => {
  const machineId = getRouterParam(event, 'id')
  if (!machineId) return []
  return componentsAndDelegationsStore.getBindingsByMachineId(machineId)
})
