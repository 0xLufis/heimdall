import { defineEventHandler, readBody, createError } from 'h3'
import {
  retractTicketInStore,
  findTicketById,
  fetchAndCacheBackendTicket,
  ensureTicketInStore
} from '../../../utils/ticketsStore'
import { backendAuthHeaders } from '../../../utils/backendProxy'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = (await readBody(event).catch(() => ({}))) || {}
  const sessionUser = (event.context as any)?.auth?.user
  const retractedBy = body.retractedBy || sessionUser?.name || 'Kiosk Operator'
  const reason = body.reason || 'Retracted from terminal kiosk'

  // Backend calls run as the caller: session bearer and the session's organisation, never raw client headers.
  const authHeaders = await backendAuthHeaders(event)
  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id, authHeaders)
  }
  if (!ticket) {
    ticket = ensureTicketInStore(id)
  }

  const updated = retractTicketInStore(id, retractedBy, reason)
  if (!updated) {
    throw createError({ statusCode: 404, statusMessage: 'Ticket not found' })
  }

  // Sync with backend if available
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  $fetch(`${backendBase}/api/v1/tickets/${id}/retract`, {
    method: 'POST',
    headers: authHeaders,
    body: {
      retractedBy,
      reason
    }
  }).catch(() => {
    if (backendBase.includes('5099')) {
      $fetch(`http://localhost:5001/api/v1/tickets/${id}/retract`, {
        method: 'POST',
        headers: authHeaders,
        body: { retractedBy, reason }
      }).catch(() => {})
    }
  })

  return {
    success: true,
    ticket: updated
  }
})
