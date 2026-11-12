import { ref, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import type { IndustrialController } from '~/types/domain'

let pollTimer: any = null
// Components currently relying on the shared poll timer; it stops with the last one.
let pollSubscribers = 0

export interface UseControllersOptions {
  /** Keep the shared fleet list fresh with a 5 s poll while mounted (default true). */
  poll?: boolean
}

export const useControllers = (options: UseControllersOptions = {}) => {
  const shouldPoll = options.poll !== false
  const controllers = useState<IndustrialController[]>('global_controllers', () => [])
  const selectedController = useState<IndustrialController | null>('global_selected_controller', () => null)
  const isLoading = ref<boolean>(false)
  const lastSyncedAt = ref<Date | null>(null)
  const error = ref<string | null>(null)

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
        ipAddress: c.systemMetadata?.IPAddress || c.ipAddress || undefined,
        osVersion: c.systemMetadata?.OsVersion || c.osVersion || (c.inventoryItems || c.InventoryItems || []).find((i: any) => i.name === 'Software')?.metadata?.OsVersion || undefined,
        machineIdentifier: c.machineIdentifier,
        pinnedObjectHandle: c.pinnedObjectHandle,
        lastOnline: c.lastOnline || c.lastSeen,
        lastSeen: c.lastSeen || c.lastOnline,
        organizationId: c.organizationId,
        controlledMachines: c.machines || c.controlledMachines || [],
        machines: c.machines || c.controlledMachines || [],
        responsibleTeams: c.responsibleTeams || [],
        inventoryItems: c.inventoryItems || c.InventoryItems || [],
        adOuPath: c.adOuPath || c.AdOuPath || c.systemMetadata?.AdOuPath || '',
        ouTags: c.ouTags || c.OuTags || (typeof c.ouTagsJson === 'string' ? (() => { try { return JSON.parse(c.ouTagsJson) } catch { return {} } })() : (c.ouTagsJson || {})),
        telemetry: {
          cpuUsagePercent: c.resourceAverages?.cpuUsageAverage,
          ramUsagePercent: c.resourceAverages?.ramUsageAverage,
          diskSpace: c.freeDiskSpace,
          beckhoffRT: c.systemMetadata?.BeckhoffRT,
          ipAddress: c.systemMetadata?.IPAddress || c.ipAddress,
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

  /**
   * Sets the controller's floor-plan pin (an empty handle unpins it). Station associations are
   * replaced only when `machineIds` is passed; omitting it leaves them untouched, so unpinning a
   * controller does not also detach it from its stations.
   */
  const updateControllerPin = async (controllerId: string, handle: string, machineIds?: string[]) => {
    const pc = controllers.value.find(c => c.id === controllerId)
    if (!pc) return

    const payload: Record<string, unknown> = {
      id: pc.id,
      name: pc.name,
      hostname: pc.hostname,
      macAddress: pc.macAddress,
      pinnedObjectHandle: handle
    }
    if (machineIds) payload.controlledMachineIds = machineIds

    await $fetch(`/api/proxy/ClientPc/${controllerId}`, {
      method: 'PUT',
      body: payload
    })
    await fetchControllers(true)
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      if (controllers.value.length === 0) {
        fetchControllers(false)
      }
      if (!shouldPoll || typeof window === 'undefined') return
      pollSubscribers++
      if (!pollTimer) {
        pollTimer = setInterval(() => {
          fetchControllers(true)
        }, 5000)
      }
    })

    onUnmounted(() => {
      if (!shouldPoll || typeof window === 'undefined') return
      // One consumer unmounting must not stop the poll another mounted consumer still relies on.
      pollSubscribers = Math.max(0, pollSubscribers - 1)
      if (pollSubscribers === 0 && pollTimer) {
        clearInterval(pollTimer)
        pollTimer = null
      }
    })
  }

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
