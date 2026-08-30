import { ref, onMounted, onUnmounted, computed } from 'vue'
import type {
  MaintenanceTicket,
  CreateTicketInput,
  TicketFilter,
  TicketMetrics,
  TicketStatus,
  TicketComment,
  MaintenanceEvent
} from '~/types/maintenance'
import { getMaintenanceService } from '~/services/maintenance'

// Global shared singleton state across all tabs, boards, and drawers
const globalTickets = ref<MaintenanceTicket[]>([])
const globalMetrics = ref<TicketMetrics | null>(null)
const globalSelectedTicket = ref<MaintenanceTicket | null>(null)
const globalIsLoading = ref(false)
const globalIsSyncing = ref(false)
const globalPendingOfflineCount = ref(0)
let globalUnsubscribeEvents: (() => void) | null = null
let listenerCount = 0

export const useMaintenance = () => {
  const service = getMaintenanceService()

  const tickets = globalTickets
  const metrics = globalMetrics
  const selectedTicket = globalSelectedTicket
  const isLoading = globalIsLoading
  const isSyncing = globalIsSyncing
  const pendingOfflineCount = globalPendingOfflineCount

  // Helper to recalculate metrics on-demand in-place
  const recalculateMetrics = () => {
    const list = tickets.value
    const total = list.length
    const open = list.filter(t => t.status === 'Open').length
    const inProgress = list.filter(t => t.status === 'In_Progress').length
    const pendingParts = list.filter(t => t.status === 'Pending_Parts').length
    const resolved = list.filter(t => t.status === 'Resolved' || t.status === 'Closed').length
    const criticalOpen = list.filter(t => t.priority === 'Critical' && t.status !== 'Resolved' && t.status !== 'Closed').length

    metrics.value = {
      openTickets: open,
      inProgressTickets: inProgress,
      pendingPartsTickets: pendingParts,
      resolvedToday: resolved,
      meanTimeToRepairMinutes: metrics.value?.meanTimeToRepairMinutes || 42,
      criticalUnresolvedAlerts: criticalOpen,
      slaComplianceRate: metrics.value?.slaComplianceRate || 96.5,
      activeTechniciansCount: metrics.value?.activeTechniciansCount || 4
    }
  }

  // Atomic On-Demand Live Event Handler
  const handleLiveEvent = (event: MaintenanceEvent) => {
    switch (event.type) {
      case 'TicketCreated': {
        if (event.ticket && !tickets.value.some(t => t.id === event.ticket.id)) {
          tickets.value.unshift(event.ticket)
          recalculateMetrics()
        }
        break
      }

      case 'TicketUpdated': {
        if (event.ticket) {
          const idx = tickets.value.findIndex(t => t.id === event.ticket.id)
          if (idx !== -1) {
            tickets.value[idx] = { ...tickets.value[idx], ...event.ticket }
          } else {
            tickets.value.unshift(event.ticket)
          }
          if (selectedTicket.value?.id === event.ticket.id) {
            selectedTicket.value = { ...selectedTicket.value, ...event.ticket }
          }
          recalculateMetrics()
        }
        break
      }

      case 'StatusChanged': {
        const target = tickets.value.find(t => t.id === event.ticketId)
        if (target) {
          target.status = event.status
          target.updatedAt = new Date().toISOString()
          if (event.status === 'Resolved' && !target.resolvedAt) {
            target.resolvedAt = new Date().toISOString()
          }
          if (selectedTicket.value?.id === event.ticketId) {
            selectedTicket.value = { ...selectedTicket.value, status: event.status, resolvedAt: target.resolvedAt }
          }
          recalculateMetrics()
        }
        break
      }

      case 'TicketDeleted': {
        tickets.value = tickets.value.filter(t => t.id !== event.ticketId)
        if (selectedTicket.value?.id === event.ticketId) {
          selectedTicket.value = null
        }
        recalculateMetrics()
        break
      }

      case 'NewComment': {
        const target = tickets.value.find(t => t.id === event.ticketId)
        if (target && event.comment) {
          if (!target.comments.some(c => c.id === event.comment.id)) {
            target.comments.push(event.comment)
          }
          if (selectedTicket.value?.id === event.ticketId) {
            if (!selectedTicket.value.comments.some(c => c.id === event.comment.id)) {
              selectedTicket.value.comments.push(event.comment)
            }
          }
        }
        break
      }
    }
  }

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
    if (!tickets.value.some(t => t.id === created.id)) {
      tickets.value.unshift(created)
    }
    recalculateMetrics()
    return created
  }

  // Optimistic UI status transition
  const updateStatus = async (id: string, status: TicketStatus, technicianName?: string) => {
    const target = tickets.value.find(t => t.id === id)
    const prevStatus = target?.status

    // 1. Optimistically update local state for zero-latency feedback
    if (target) {
      target.status = status
      target.updatedAt = new Date().toISOString()
      if (selectedTicket.value?.id === id) {
        selectedTicket.value.status = status
      }
      recalculateMetrics()
    }

    try {
      // 2. Submit to backend / BFF
      const updated = await service.updateTicketStatus(id, status, technicianName)
      if (target && updated) {
        Object.assign(target, updated)
      }
      return updated
    } catch (err) {
      // 3. Rollback on failure
      if (target && prevStatus) {
        target.status = prevStatus
        if (selectedTicket.value?.id === id) {
          selectedTicket.value.status = prevStatus
        }
        recalculateMetrics()
      }
      throw err
    }
  }

  const addComment = async (ticketId: string, authorName: string, content: string): Promise<TicketComment> => {
    const comment = await service.addComment(ticketId, authorName, content)
    const target = tickets.value.find(t => t.id === ticketId)
    if (target && !target.comments.some(c => c.id === comment.id)) {
      target.comments.push(comment)
    }
    if (selectedTicket.value && selectedTicket.value.id === ticketId) {
      if (!selectedTicket.value.comments.some(c => c.id === comment.id)) {
        selectedTicket.value.comments.push(comment)
      }
    }
    return comment
  }

  onMounted(() => {
    listenerCount++
    if (tickets.value.length === 0) {
      fetchTickets()
    }

    // Subscribe to live SignalR / WebSocket push events on first mount
    if (!globalUnsubscribeEvents) {
      globalUnsubscribeEvents = service.subscribeToEvents(handleLiveEvent)
    }
  })

  onUnmounted(() => {
    listenerCount--
    if (listenerCount <= 0 && globalUnsubscribeEvents) {
      globalUnsubscribeEvents()
      globalUnsubscribeEvents = null
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
    addComment,
    handleLiveEvent,
    recalculateMetrics
  }
}
