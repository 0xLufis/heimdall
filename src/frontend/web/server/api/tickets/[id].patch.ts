import { defineEventHandler, readBody, createError } from 'h3'
import {
  editTicketWithHistoryInStore,
  updateTicketInStore,
  findTicketById,
  fetchAndCacheBackendTicket,
  ensureTicketInStore
} from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = (await readBody(event)) || {}
  const sessionUser = (event.context as any)?.auth?.user
  const editorName = body.editorName || sessionUser?.name || 'Administrator'
  const reason = body.changeReason || body.reason || 'Field update / correction'

  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id)
  }
  if (!ticket) {
    ticket = ensureTicketInStore(id, body)
  }

  const updatedTicket = editTicketWithHistoryInStore(id, body, editorName, reason) || updateTicketInStore(id, body) || ticket

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)

  const syncBackend = async (baseUrl: string) => {
    if (body.isEscalated === true) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}/escalate`, {
        method: 'POST',
        body: {
          reason: updatedTicket.escalationReason || 'Incident Escalation',
          escalatedBy: updatedTicket.escalatedBy || editorName,
          handoverState: updatedTicket.escalationHandoverState || 'Notification',
          target: updatedTicket.escalationTarget || 'DedicatedEngineer'
        }
      }).catch(() => {})
    } else if (body.isEscalated === false) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}/deescalate`, {
        method: 'POST',
        body: {
          resolvedBy: updatedTicket.escalatedBy || editorName,
          resolutionNotes: 'De-escalated via web interface'
        }
      }).catch(() => {})
    }

    if (body.status) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify(body.status),
        headers: { 'Content-Type': 'application/json' }
      }).catch(() => {})
    }

    if (isGuid) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}`, {
        method: 'PUT',
        body: {
          id: updatedTicket.id,
          title: updatedTicket.title,
          description: updatedTicket.description,
          status: updatedTicket.status,
          priority: updatedTicket.priority,
          isLineStop: Boolean(updatedTicket.isLineStop),
          lineStopDurationMinutes: updatedTicket.lineStopDurationMinutes ?? 0,
          responsibleDepartment: updatedTicket.responsibleDepartment,
          originatorType: updatedTicket.originatorType || 'ManualUser',
          issueType: updatedTicket.issueType || 'Maintenance',
          reservedBy: updatedTicket.reservedBy,
          isEscalated: Boolean(updatedTicket.isEscalated),
          escalationReason: updatedTicket.escalationReason,
          escalationTarget: updatedTicket.escalationTarget,
          escalationHandoverState: updatedTicket.escalationHandoverState || 'None',
          escalatedBy: updatedTicket.escalatedBy,
          escalatedAt: updatedTicket.escalatedAt,
          machineId: (updatedTicket.stationId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(updatedTicket.stationId)) ? updatedTicket.stationId : null,
          clientPcId: (updatedTicket.controllerId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(updatedTicket.controllerId)) ? updatedTicket.controllerId : null,
          createdBy: updatedTicket.reportedByUserName || 'Operator',
          assignedTo: updatedTicket.assignedTechnicianName || null
        }
      }).catch(() => {})
    }
  }

  // Fire and sync in background
  syncBackend(backendBase).catch(() => {
    if (backendBase.includes('5099')) {
      syncBackend('http://localhost:5001').catch(() => {})
    }
  })

  return {
    success: true,
    ticket: updatedTicket
  }
})
