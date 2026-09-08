import { ref, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { getMaintenanceService } from '~/services/maintenance'

let maintenanceEventUnsub: (() => void) | null = null
let pollTimer: any = null
const subscribers = new Set<() => void>()
const isLiveConnected = ref<boolean>(false)
const lastEvent = ref<any>(null)
const lastSyncedAt = ref<Date>(new Date())

export const useInventoryLive = () => {
  const connectionError = ref<string | null>(null)

  const notifySubscribers = () => {
    lastSyncedAt.value = new Date()
    subscribers.forEach(cb => {
      try {
        cb()
      } catch (err) {
        console.error('Error executing inventory refresh callback:', err)
      }
    })
  }

  const connectSignalR = async () => {
    if (typeof window === 'undefined') return

    // Subscribe to central maintenance service event stream
    if (!maintenanceEventUnsub) {
      try {
        const maintenanceService = getMaintenanceService()
        maintenanceEventUnsub = maintenanceService.subscribeToEvents((event) => {
          if (event.type === 'InventoryUpdated' || event.type === 'TelemetryReceived') {
            lastEvent.value = event
            isLiveConnected.value = true
            notifySubscribers()
          }
        })
        isLiveConnected.value = true
      } catch (e: any) {
        connectionError.value = e?.message || 'Failed to initialize maintenance service'
      }
    }
  }

  const onInventoryUpdate = (callback: () => void) => {
    subscribers.add(callback)
    if (getCurrentInstance()) {
      onUnmounted(() => {
        subscribers.delete(callback)
      })
    }
  }

  // Setup auto-poll fallback if SignalR is unavailable
  const startPolling = (intervalMs: number = 8000) => {
    if (typeof window === 'undefined') return
    if (pollTimer) clearInterval(pollTimer)
    pollTimer = setInterval(() => {
      // If SignalR is not connected, poll
      if (!isLiveConnected.value) {
        notifySubscribers()
      }
    }, intervalMs)
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      connectSignalR()
      startPolling()
    })
  }

  return {
    isLiveConnected,
    lastEvent,
    lastSyncedAt,
    connectionError,
    onInventoryUpdate,
    connectSignalR,
    notifySubscribers
  }
}
