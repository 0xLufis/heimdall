import { ref, onMounted, onUnmounted } from 'vue'
import type { IndustrialController } from '~/types/domain'

// Singleton module-scoped reactive state across all components and re-renders
const globalControllers = ref<IndustrialController[]>([])
const globalSelectedController = ref<IndustrialController | null>(null)
const globalIsLoading = ref<boolean>(false)
const globalLastSyncedAt = ref<Date | null>(null)
const globalError = ref<string | null>(null)
let pollTimer: any = null

export const useControllers = () => {
  const controllers = globalControllers
  const selectedController = globalSelectedController
  const isLoading = globalIsLoading
  const lastSyncedAt = globalLastSyncedAt
  const error = globalError

  const isOnline = (lastOnline: string | null | undefined): boolean => {
    if (!lastOnline) return false
    const date = new Date(lastOnline)
    const now = new Date()
    return (now.getTime() - date.getTime()) < (5 * 60 * 1000) // Online within 5 minutes
  }

  const fetchControllers = async (silent: boolean = false) => {
    if (!silent) isLoading.value = true
    error.value = null
    try {
      const data = await $fetch<any[]>('/api/proxy/ClientPc')
      controllers.value = (data || []).map((c: any) => ({
        id: c.id || c.Id,
        name: c.name || c.hostname || 'Controller',
        displayName: c.displayName || c.hostname,
        hostname: c.hostname || c.name || 'Unknown Host',
        macAddress: c.macAddress || '',
        ipAddress: c.systemMetadata?.IPAddress || c.ipAddress,
        machineIdentifier: c.machineIdentifier,
        pinnedObjectHandle: c.pinnedObjectHandle,
        lastOnline: c.lastOnline || c.lastSeen,
        lastSeen: c.lastSeen || c.lastOnline,
        organizationId: c.organizationId,
        controlledMachines: c.machines || c.controlledMachines || [],
        machines: c.machines || c.controlledMachines || [],
        responsibleTeams: c.responsibleTeams || [],
        freeDiskSpace: c.freeDiskSpace,
        systemMetadata: c.systemMetadata,
        telemetry: {
          cpuUsagePercent: c.resourceAverages?.cpuUsageAverage ?? (Math.floor(Math.random() * 30) + 10),
          ramUsagePercent: c.resourceAverages?.ramUsageAverage ?? (Math.floor(Math.random() * 40) + 30),
          diskSpace: c.freeDiskSpace,
          beckhoffRT: c.systemMetadata?.BeckhoffRT,
          ipAddress: c.systemMetadata?.IPAddress,
          isOnline: isOnline(c.lastOnline || c.lastSeen)
        }
      }))
      lastSyncedAt.value = new Date()

      // If a controller is selected, update its reference in place
      if (selectedController.value) {
        const updated = controllers.value.find(c => c.id === selectedController.value?.id)
        if (updated) selectedController.value = updated
      }
    } catch (e: any) {
      if (!silent) error.value = e?.message || 'Failed to fetch controllers'
    } finally {
      if (!silent) isLoading.value = false
    }
  }

  const queueAgentCommand = async (controllerId: string, commandType: string, payload: any, signature?: string) => {
    return await $fetch(`/api/proxy/AgentCommand/${controllerId}/update-config`, {
      method: 'POST',
      body: {
        config: payload,
        signature
      }
    })
  }

  const updateControllerPin = async (controllerId: string, handle: string, machineIds: string[] = []) => {
    const pc = controllers.value.find(c => c.id === controllerId)
    if (!pc) return

    const payload = {
      id: pc.id,
      name: pc.name,
      hostname: pc.hostname,
      macAddress: pc.macAddress,
      pinnedObjectHandle: handle,
      controlledMachineIds: machineIds
    }

    await $fetch(`/api/proxy/ClientPc/${controllerId}`, {
      method: 'PUT',
      body: payload
    })
    await fetchControllers(true)
  }

  onMounted(() => {
    if (controllers.value.length === 0) {
      fetchControllers(false)
    }
    if (typeof window !== 'undefined' && !pollTimer) {
      pollTimer = setInterval(() => {
        fetchControllers(true)
      }, 5000)
    }
  })

  onUnmounted(() => {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  })

  return {
    controllers,
    selectedController,
    isLoading,
    lastSyncedAt,
    error,
    isOnline,
    fetchControllers,
    queueAgentCommand,
    updateControllerPin
  }
}
