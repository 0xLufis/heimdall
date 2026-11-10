import { defineEventHandler } from 'h3'
import { machinesStore } from '../utils/machinesStore'
import { getTicketsStore } from '../utils/ticketsStore'

export default defineEventHandler(async () => {
  const machineCount = machinesStore.getAllMachines({ includeDecommissioned: true }).length
  const ticketCount = getTicketsStore().length
  const memoryUsage = process.memoryUsage()

  return {
    status: 'Ready',
    subsystem: 'heimdall-web-frontend',
    checks: {
      machinesStore: machineCount > 0 ? 'Healthy' : 'Degraded',
      ticketsStore: ticketCount >= 0 ? 'Healthy' : 'Degraded',
      memory: {
        rssMb: Math.round(memoryUsage.rss / (1024 * 1024)),
        heapUsedMb: Math.round(memoryUsage.heapUsed / (1024 * 1024))
      }
    },
    timestamp: new Date().toISOString()
  }
})
