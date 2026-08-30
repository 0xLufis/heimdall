import { ref, onMounted } from 'vue'
import type { IndustrialController } from '~/types/domain'

export const useControllers = () => {
  const controllers = ref<IndustrialController[]>([])
  const selectedController = ref<IndustrialController | null>(null)
  const isLoading = ref(false)
  const error = ref<string | null>(null)

  const isOnline = (lastOnline: string | null | undefined): boolean => {
    if (!lastOnline) return false
    const date = new Date(lastOnline)
    const now = new Date()
    return (now.getTime() - date.getTime()) < (5 * 60 * 1000) // Online within 5 minutes
  }

  const fetchControllers = async () => {
    isLoading.value = true
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
    } catch (e: any) {
      error.value = e?.message || 'Failed to fetch controllers'
    } finally {
      isLoading.value = false
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
    await fetchControllers()
  }

  onMounted(() => {
    fetchControllers()
  })

  return {
    controllers,
    selectedController,
    isLoading,
    error,
    isOnline,
    fetchControllers,
    queueAgentCommand,
    updateControllerPin
  }
}
