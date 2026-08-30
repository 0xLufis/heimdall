import { ref, onMounted } from 'vue'

export interface OfflineTicketPayload {
  id?: string
  ticketNumber?: string
  stationId: string
  stationName: string
  controllerId?: string
  title: string
  description: string
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  reportedByUserId?: string
  reportedByUserName?: string
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  createdAt?: string
}

const STORAGE_KEY = 'heimdall_pending_tickets'

export function useOfflineTickets() {
  const pendingTickets = ref<OfflineTicketPayload[]>([])
  const isSyncing = ref(false)

  const loadPendingTickets = () => {
    if (typeof localStorage === 'undefined') return
    try {
      const stored = localStorage.getItem(STORAGE_KEY)
      if (stored) {
        pendingTickets.value = JSON.parse(stored)
      }
    } catch (e) {
      console.error('Error reading pending tickets from storage:', e)
    }
  }

  const savePendingTickets = () => {
    if (typeof localStorage === 'undefined') return
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(pendingTickets.value))
    } catch (e) {
      console.error('Error writing pending tickets to storage:', e)
    }
  }

  const queueOfflineTicket = async (ticketData: OfflineTicketPayload) => {
    const timestamp = new Date().toISOString()
    const payload: OfflineTicketPayload = {
      ...ticketData,
      id: `offline-${Date.now()}-${Math.floor(Math.random() * 1000)}`,
      createdAt: timestamp
    }

    pendingTickets.value.push(payload)
    savePendingTickets()

    // Register Background Sync if available in browser Service Worker
    if (typeof navigator !== 'undefined' && 'serviceWorker' in navigator && 'SyncManager' in window) {
      try {
        const registration = await navigator.serviceWorker.ready
        if ((registration as any).sync) {
          await (registration as any).sync.register('sync-tickets')
        }
      } catch (err) {
        console.warn('Background Sync registration failed, will retry when back online:', err)
      }
    }

    return payload
  }

  const syncPendingTickets = async () => {
    if (pendingTickets.value.length === 0 || isSyncing.value) return
    isSyncing.value = true

    const queue = [...pendingTickets.value]
    const remaining: OfflineTicketPayload[] = []

    for (const ticket of queue) {
      try {
        const res = await $fetch<{ success: boolean; ticket: any }>('/api/tickets', {
          method: 'POST',
          body: ticket
        })

        if (!res || !res.success) {
          remaining.push(ticket)
        }
      } catch (err) {
        console.error('Failed to sync offline ticket:', err)
        remaining.push(ticket)
      }
    }

    pendingTickets.value = remaining
    savePendingTickets()
    isSyncing.value = false
  }

  onMounted(() => {
    loadPendingTickets()
    if (typeof window !== 'undefined') {
      window.addEventListener('online', syncPendingTickets)
    }
  })

  return {
    pendingTickets,
    isSyncing,
    queueOfflineTicket,
    syncPendingTickets,
    loadPendingTickets
  }
}
