import { defineEventHandler, getRouterParam } from 'h3'
import { componentsAndDelegationsStore } from '../../../utils/componentsAndDelegationsStore'

export default defineEventHandler((event) => {
  const machineId = getRouterParam(event, 'id')
  if (!machineId) return { slots: [], software: [], licenses: [], documents: [], serviceLogs: [] }

  const slots = componentsAndDelegationsStore.getSlotsByMachineId(machineId)
  const software = componentsAndDelegationsStore.getSoftwareAssets(machineId)
  const licenses = componentsAndDelegationsStore.getLicenses(machineId)
  const documents = componentsAndDelegationsStore.getDocuments(machineId)
  const serviceLogs = componentsAndDelegationsStore.getServiceLogs(machineId)

  return {
    slots,
    software,
    licenses,
    documents,
    serviceLogs
  }
})
