import { ref, onMounted, onUnmounted, getCurrentInstance } from 'vue'

export interface OfflineCachedTicket {
  id: string
  ticketNumber: string
  title: string
  status: string
  priority: string
  stationName: string
  cachedAt: string
}

export interface OfflineCachedPart {
  id: string
  partNumber: string
  name: string
  stockLevel: number
  location: string
  cachedAt: string
}

function safeSetStorage(key: string, val: string) {
  try {
    if (typeof window !== 'undefined' && typeof window.localStorage !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(key, val)
    }
  } catch {}
}

function safeGetStorage(key: string): string | null {
  try {
    if (typeof window !== 'undefined' && typeof window.localStorage !== 'undefined' && window.localStorage) {
      return window.localStorage.getItem(key)
    }
  } catch {}
  return null
}

export function useOfflineCache() {
  const isOnline = ref(true)
  const isServiceWorkerRegistered = ref(false)
  const isPushSubscribed = ref(false)
  const cachedTickets = ref<OfflineCachedTicket[]>([])
  const cachedParts = ref<OfflineCachedPart[]>([])
  const offlineMutationQueue = ref<Array<{ url: string; method: string; body: any; timestamp: string }>>([])

  function checkOnlineStatus() {
    if (typeof navigator !== 'undefined') {
      isOnline.value = navigator.onLine
    }
  }

  function loadFromLocalStorage() {
    try {
      const ticketsStr = safeGetStorage('heimdall_offline_tickets')
      if (ticketsStr) cachedTickets.value = JSON.parse(ticketsStr)

      const partsStr = safeGetStorage('heimdall_offline_parts')
      if (partsStr) cachedParts.value = JSON.parse(partsStr)

      const queueStr = safeGetStorage('heimdall_offline_queue')
      if (queueStr) offlineMutationQueue.value = JSON.parse(queueStr)
    } catch {}
  }

  function cacheActiveTickets(tickets: any[]) {
    const formatted: OfflineCachedTicket[] = tickets.map(t => ({
      id: t.id,
      ticketNumber: t.ticketNumber,
      title: t.title,
      status: t.status,
      priority: t.priority,
      stationName: t.stationName || t.stationId,
      cachedAt: new Date().toISOString()
    }))

    cachedTickets.value = formatted
    safeSetStorage('heimdall_offline_tickets', JSON.stringify(formatted))
  }

  function cachePartsCatalog(parts: any[]) {
    const formatted: OfflineCachedPart[] = parts.map(p => ({
      id: p.id,
      partNumber: p.partNumber || p.id,
      name: p.name,
      stockLevel: p.stockQuantity ?? p.stockLevel ?? 0,
      location: p.storageBin || p.location || 'Warehouse',
      cachedAt: new Date().toISOString()
    }))

    cachedParts.value = formatted
    safeSetStorage('heimdall_offline_parts', JSON.stringify(formatted))
  }

  function queueOfflineMutation(mutation: { url: string; method: string; body: any }) {
    offlineMutationQueue.value.push({
      ...mutation,
      timestamp: new Date().toISOString()
    })
    safeSetStorage('heimdall_offline_queue', JSON.stringify(offlineMutationQueue.value))
  }

  async function syncOfflineQueue(): Promise<number> {
    if (!isOnline.value || offlineMutationQueue.value.length === 0) return 0
    let synced = 0
    const remaining: typeof offlineMutationQueue.value = []

    for (const item of offlineMutationQueue.value) {
      try {
        await $fetch(item.url, {
          method: item.method as any,
          body: item.body
        })
        synced++
      } catch {
        remaining.push(item)
      }
    }

    offlineMutationQueue.value = remaining
    safeSetStorage('heimdall_offline_queue', JSON.stringify(remaining))
    return synced
  }

  async function subscribeToPushNotifications(publicVapidKey: string): Promise<boolean> {
    if (typeof window === 'undefined' || !('serviceWorker' in navigator) || !('PushManager' in window)) {
      return false
    }

    try {
      const reg = await navigator.serviceWorker.ready
      let sub = await reg.pushManager.getSubscription()
      if (!sub) {
        sub = await reg.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: publicVapidKey
        })
      }
      isPushSubscribed.value = Boolean(sub)
      return isPushSubscribed.value
    } catch {
      return false
    }
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      checkOnlineStatus()
      loadFromLocalStorage()
      if (typeof window !== 'undefined') {
        window.addEventListener('online', checkOnlineStatus)
        window.addEventListener('offline', checkOnlineStatus)
      }
    })

    onUnmounted(() => {
      if (typeof window !== 'undefined') {
        window.removeEventListener('online', checkOnlineStatus)
        window.removeEventListener('offline', checkOnlineStatus)
      }
    })
  } else {
    checkOnlineStatus()
    loadFromLocalStorage()
  }

  return {
    isOnline,
    isServiceWorkerRegistered,
    isPushSubscribed,
    cachedTickets,
    cachedParts,
    offlineMutationQueue,
    cacheActiveTickets,
    cachePartsCatalog,
    queueOfflineMutation,
    syncOfflineQueue,
    subscribeToPushNotifications
  }
}
