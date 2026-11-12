import { ref, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { getMaintenanceService } from '~/services/maintenance'

let maintenanceEventUnsub: (() => void) | null = null
let pollTimer: any = null
const subscribers = new Set<() => void>()
const isLiveConnected = ref<boolean>(false)
const lastEvent = ref<any>(null)
const lastSyncedAt = ref<Date>(new Date())
let activeInstances = 0

/**
 * The backend sends InventoryUpdated for every agent heartbeat, several times a second on a busy plant,
 * and every subscriber refetches its whole dataset. Updates are therefore coalesced: the first refreshes
 * at once, and any that follow within the window collapse into one trailing refresh.
 */
export const INVENTORY_REFRESH_COALESCE_MS = 3000
let refreshTimer: ReturnType<typeof setTimeout> | null = null
let lastRefreshAt = 0

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

const scheduleRefresh = () => {
  if (refreshTimer) return // a refresh is already pending and will include this update
  const wait = Math.max(0, lastRefreshAt + INVENTORY_REFRESH_COALESCE_MS - Date.now())
  refreshTimer = setTimeout(() => {
    refreshTimer = null
    lastRefreshAt = Date.now()
    notifySubscribers()
  }, wait)
}

const cancelScheduledRefresh = () => {
  if (refreshTimer) {
    clearTimeout(refreshTimer)
    refreshTimer = null
  }
}

export const useInventoryLive = () => {
  const connectionError = ref<string | null>(null)

  const connectSignalR = async () => {
    if (typeof window === 'undefined') return

    // Subscribe to central maintenance service event stream
    if (!maintenanceEventUnsub) {
      try {
        const maintenanceService = getMaintenanceService()
        maintenanceEventUnsub = maintenanceService.subscribeToEvents((event) => {
          lastEvent.value = event
          isLiveConnected.value = true
          if (event.type === 'InventoryUpdated') {
            scheduleRefresh()
          }
        })
        isLiveConnected.value = true
      } catch (e: any) {
        connectionError.value = e?.message || 'Failed to initialize maintenance service'
      }
    }
  }

  const stopPolling = () => {
    if (pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }

  // Setup auto-poll fallback if SignalR is unavailable
  const startPolling = (intervalMs: number = 8000) => {
    if (typeof window === 'undefined') return
    stopPolling()
    pollTimer = setInterval(() => {
      // If SignalR is not connected, poll
      if (!isLiveConnected.value) {
        notifySubscribers()
      }
    }, intervalMs)
  }

  const onInventoryUpdate = (callback: () => void) => {
    subscribers.add(callback)
    if (getCurrentInstance()) {
      onUnmounted(() => {
        subscribers.delete(callback)
      })
    }
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      activeInstances++
      connectSignalR()
      startPolling()
    })

    onUnmounted(() => {
      activeInstances = Math.max(0, activeInstances - 1)
      if (activeInstances === 0) {
        stopPolling()
        cancelScheduledRefresh()
        if (maintenanceEventUnsub) {
          maintenanceEventUnsub()
          maintenanceEventUnsub = null
        }
        subscribers.clear()
        isLiveConnected.value = false
      }
    })
  }

  return {
    isLiveConnected,
    lastEvent,
    lastSyncedAt,
    connectionError,
    onInventoryUpdate,
    connectSignalR,
    notifySubscribers,
    startPolling,
    stopPolling
  }
}
