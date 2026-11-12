import { defineEventHandler, readBody, createError } from 'h3'
import {
  editTicketWithHistoryInStore,
  updateTicketInStore,
  findTicketById,
  fetchAndCacheBackendTicket,
  ensureTicketInStore,
  escalationHandoverStateNumber
} from '../../utils/ticketsStore'
import { backendAuthHeaders } from '../../utils/backendProxy'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = (await readBody(event)) || {}
  const sessionUser = (event.context as any)?.auth?.user
  const editorName = body.editorName || sessionUser?.name || 'Administrator'
  const reason = body.changeReason || body.reason || 'Field update / correction'

  // Backend calls run as the caller. The sync below runs after the response: without the session the
  // backend answers 401 outside development and the change never reaches it.
  const authHeaders = await backendAuthHeaders(event)
  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id, authHeaders)
  }
  if (!ticket) {
    ticket = ensureTicketInStore(id, body)
  }

  const updatedTicket = editTicketWithHistoryInStore(id, body, editorName, reason) || updateTicketInStore(id, body) || ticket

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)
  const syncFailed = (step: string) => (err: any) => {
    console.warn(`[tickets] backend sync of ${id} (${step}) failed: ${err?.statusCode || err?.status || ''} ${err?.message || err}`)
  }

  const syncBackend = async (baseUrl: string) => {
    if (body.isEscalated === true) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}/escalate`, {
        method: 'POST',
        headers: authHeaders,
        body: {
          reason: updatedTicket.escalationReason || 'Incident Escalation',
          escalatedBy: updatedTicket.escalatedBy || editorName,
          handoverState: escalationHandoverStateNumber(updatedTicket.escalationHandoverState, 1),
          target: updatedTicket.escalationTarget || 'DedicatedEngineer'
        }
      }).catch(syncFailed('escalate'))
    } else if (body.isEscalated === false) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}/deescalate`, {
        method: 'POST',
        headers: authHeaders,
        body: {
          resolvedBy: updatedTicket.escalatedBy || editorName,
          resolutionNotes: 'De-escalated via web interface'
        }
      }).catch(syncFailed('de-escalate'))
    }

    if (body.status) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}/status`, {
        method: 'PATCH',
        body: JSON.stringify(body.status),
        headers: { ...authHeaders, 'Content-Type': 'application/json' }
      }).catch(syncFailed('status'))
    }

    if (isGuid) {
      await $fetch(`${baseUrl}/api/v1/tickets/${id}`, {
        method: 'PUT',
        headers: authHeaders,
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
          escalationHandoverState: escalationHandoverStateNumber(updatedTicket.escalationHandoverState, 0),
          escalatedBy: updatedTicket.escalatedBy,
          escalatedAt: updatedTicket.escalatedAt,
          machineId: (updatedTicket.stationId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(updatedTicket.stationId)) ? updatedTicket.stationId : null,
          clientPcId: (updatedTicket.controllerId && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(updatedTicket.controllerId)) ? updatedTicket.controllerId : null,
          createdBy: updatedTicket.reportedByUserName || 'Operator',
          assignedTo: updatedTicket.assignedTechnicianName || null
        }
      }).catch(syncFailed('update'))
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
