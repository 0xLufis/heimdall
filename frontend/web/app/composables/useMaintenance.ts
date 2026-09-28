import { ref, onMounted, onUnmounted, computed, getCurrentInstance } from 'vue'
import type {
  MaintenanceTicket,
  CreateTicketInput,
  TicketFilter,
  TicketMetrics,
  TicketStatus,
  PendingReason,
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
    const inProgress = list.filter(t => t.status === 'InProgress').length
    const pending = list.filter(t => t.status === 'Pending').length
    const resolved = list.filter(t => t.status === 'Resolved').length
    const closed = list.filter(t => t.status === 'Closed').length
    const escalated = list.filter(t => t.isEscalated).length
    const criticalOpen = list.filter(t => t.priority === 'Critical' && t.status !== 'Resolved' && t.status !== 'Closed').length
    const now = Date.now()
    const overdue = list.filter(t => t.slaDueAt && new Date(t.slaDueAt).getTime() < now && t.status !== 'Resolved' && t.status !== 'Closed').length
    const slaCompliance = total > 0 ? Math.round(((total - overdue) / total) * 100) : 100

    metrics.value = {
      totalTickets: total,
      openCount: open,
      inProgressCount: inProgress,
      pendingCount: pending,
      resolvedCount: resolved,
      closedCount: closed,
      escalatedCount: escalated,
      criticalCount: criticalOpen,
      overdueCount: overdue,
      slaCompliancePercent: slaCompliance
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

      case 'TicketDeleted': {
        if (event.ticketId) {
          tickets.value = tickets.value.filter(t => t.id !== event.ticketId)
          if (selectedTicket.value?.id === event.ticketId) {
            selectedTicket.value = null
          }
          recalculateMetrics()
        }
        break
      }

      case 'StatusChanged': {
        if (event.ticketId && event.status) {
          const t = tickets.value.find(item => item.id === event.ticketId)
          if (t) {
            t.status = event.status
            t.updatedAt = event.timestamp
            if (selectedTicket.value?.id === event.ticketId) {
              selectedTicket.value.status = event.status
              selectedTicket.value.updatedAt = event.timestamp
            }
            recalculateMetrics()
          }
        }
        break
      }

      case 'NewComment': {
        if (event.ticketId && event.comment) {
          const t = tickets.value.find(item => item.id === event.ticketId)
          if (t && !t.comments.some(c => c.id === event.comment?.id)) {
            t.comments.push(event.comment)
          }
          if (selectedTicket.value?.id === event.ticketId) {
            if (!selectedTicket.value.comments.some(c => c.id === event.comment?.id)) {
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
  const updateStatus = async (
    id: string,
    status: TicketStatus,
    technicianName?: string,
    extra?: Record<string, any>
  ) => {
    const target = tickets.value.find(t => t.id === id)
    const prevStatus = target?.status
    const prevReason = target?.pendingReason
    const prevDetails = target?.pendingDetails

    if (target) {
      target.status = status
      if (extra?.pendingReason !== undefined) target.pendingReason = extra.pendingReason
      if (extra?.pendingDetails !== undefined) target.pendingDetails = extra.pendingDetails
      target.updatedAt = new Date().toISOString()
      if (selectedTicket.value?.id === id) {
        selectedTicket.value.status = status
        if (extra?.pendingReason !== undefined) selectedTicket.value.pendingReason = extra.pendingReason
        if (extra?.pendingDetails !== undefined) selectedTicket.value.pendingDetails = extra.pendingDetails
      }
      recalculateMetrics()
    }

    try {
      const updated = await (service as any).updateTicketStatus(id, status, technicianName, extra)
      if (target && updated) {
        Object.assign(target, updated)
      }
      return updated
    } catch (err) {
      if (target && prevStatus) {
        target.status = prevStatus
        target.pendingReason = prevReason
        target.pendingDetails = prevDetails
        if (selectedTicket.value?.id === id) {
          selectedTicket.value.status = prevStatus
          selectedTicket.value.pendingReason = prevReason
          selectedTicket.value.pendingDetails = prevDetails
        }
        recalculateMetrics()
      }
      throw err
    }
  }

  const setPending = async (
    ticketId: string,
    pendingReason: PendingReason,
    pendingDetails?: string
  ) => {
    return await updateStatus(ticketId, 'Pending', undefined, { pendingReason, pendingDetails })
  }

  const escalateTicket = async (ticketId: string, reason: string, escalatedBy?: string) => {
    const target = tickets.value.find(t => t.id === ticketId)
    if (target) {
      target.isEscalated = true
      target.escalationReason = reason
      target.escalatedBy = escalatedBy || 'Operator'
      target.escalatedAt = new Date().toISOString()
      if (selectedTicket.value?.id === ticketId) {
        selectedTicket.value.isEscalated = true
        selectedTicket.value.escalationReason = reason
        selectedTicket.value.escalatedBy = escalatedBy || 'Operator'
        selectedTicket.value.escalatedAt = new Date().toISOString()
      }
      recalculateMetrics()
    }

    try {
      return await (service as any).escalate(ticketId, reason, escalatedBy || 'Operator')
    } catch (err) {
      // Fallback to PATCH /api/tickets/{id}
      return await $fetch(`/api/tickets/${ticketId}`, {
        method: 'PATCH',
        body: {
          isEscalated: true,
          escalationReason: reason,
          escalatedBy: escalatedBy || 'Operator',
          escalatedAt: new Date().toISOString()
        }
      })
    }
  }

  const resolveEscalation = async (ticketId: string, resolvedBy?: string) => {
    const target = tickets.value.find(t => t.id === ticketId)
    if (target) {
      target.isEscalated = false
      target.escalationReason = null
      target.escalationClosedBy = resolvedBy || 'Operator'
      target.escalationClosedAt = new Date().toISOString()
      if (selectedTicket.value?.id === ticketId) {
        selectedTicket.value.isEscalated = false
        selectedTicket.value.escalationReason = null
        selectedTicket.value.escalationClosedBy = resolvedBy || 'Operator'
        selectedTicket.value.escalationClosedAt = new Date().toISOString()
      }
      recalculateMetrics()
    }

    try {
      return await (service as any).resolveEscalation(ticketId, resolvedBy || 'Operator')
    } catch (err) {
      return await $fetch(`/api/tickets/${ticketId}`, {
        method: 'PATCH',
        body: {
          isEscalated: false,
          escalationReason: null,
          escalationClosedBy: resolvedBy || 'Operator',
          escalationClosedAt: new Date().toISOString()
        }
      })
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

  let syncTimer: any = null
  if (getCurrentInstance()) {
    onMounted(() => {
      listenerCount++
      fetchTickets()

      if (!globalUnsubscribeEvents) {
        globalUnsubscribeEvents = service.subscribeToEvents(handleLiveEvent)
      }

      syncTimer = setInterval(async () => {
        try {
          if (service.getPendingSyncCount) {
            pendingOfflineCount.value = await service.getPendingSyncCount()
          }
        } catch {}
      }, 5000)
    })

    onUnmounted(() => {
      listenerCount--
      if (syncTimer) clearInterval(syncTimer)
      if (listenerCount <= 0 && globalUnsubscribeEvents) {
        globalUnsubscribeEvents()
        globalUnsubscribeEvents = null
      }
    })
  }

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
    setPending,
    escalateTicket,
    resolveEscalation,
    addComment,
    recalculateMetrics,
    handleLiveEvent
  }
}
