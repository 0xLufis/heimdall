import { ref, onMounted, onUnmounted } from 'vue'
import type {
  MaintenanceTicket,
  CreateTicketInput,
  TicketFilter,
  TicketMetrics,
  TicketStatus,
  TicketComment
} from '~/types/maintenance'
import { getMaintenanceService } from '~/services/maintenance'

export const useMaintenance = () => {
  const service = getMaintenanceService()

  const tickets = ref<MaintenanceTicket[]>([])
  const metrics = ref<TicketMetrics | null>(null)
  const selectedTicket = ref<MaintenanceTicket | null>(null)
  const isLoading = ref(false)
  const isSyncing = ref(false)
  const pendingOfflineCount = ref(0)
  let unsubscribeEvents: (() => void) | null = null

  const fetchTickets = async (filter?: TicketFilter) => {
    isLoading.value = true
    try {
      const [ticketList, metricsData] = await Promise.all([
        service.getTickets(filter),
        service.getMetrics()
      ])
      tickets.value = ticketList
      metrics.value = metricsData
    } finally {
      isLoading.value = false
    }
  }

  const createTicket = async (input: CreateTicketInput) => {
    const created = await service.createTicket(input)
    await fetchTickets()
    return created
  }

  const updateStatus = async (id: string, status: TicketStatus, technicianName?: string) => {
    const updated = await service.updateTicketStatus(id, status, technicianName)
    await fetchTickets()
    return updated
  }

  const addComment = async (ticketId: string, authorName: string, content: string): Promise<TicketComment> => {
    const comment = await service.addComment(ticketId, authorName, content)
    if (selectedTicket.value && selectedTicket.value.id === ticketId) {
      selectedTicket.value.comments.push(comment)
    }
    return comment
  }

  onMounted(() => {
    fetchTickets()

    // Subscribe to live SignalR / WebSocket push events
    unsubscribeEvents = service.subscribeToEvents(event => {
      if (event.type === 'TicketCreated' || event.type === 'TicketUpdated' || event.type === 'StatusChanged') {
        fetchTickets()
      }
    })
  })

  onUnmounted(() => {
    if (unsubscribeEvents) {
      unsubscribeEvents()
    }
  })

  return {
    tickets,
    metrics,
    selectedTicket,
    isLoading,
    isSyncing,
    pendingOfflineCount,
    fetchTickets,
    createTicket,
    updateStatus,
    addComment
  }
}
