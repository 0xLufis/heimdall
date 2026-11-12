import { defineEventHandler, createError } from 'h3'
import { findTicketById, fetchAndCacheBackendTicket } from '../../utils/ticketsStore'
import { backendAuthHeaders } from '../../utils/backendProxy'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  // Backend calls run as the caller: session bearer and the session's organisation, never raw client headers.
  const authHeaders = await backendAuthHeaders(event)
  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id, authHeaders)
  }
  if (!ticket) {
    throw createError({ statusCode: 404, statusMessage: `Ticket with ID '${id}' not found` })
  }

  return {
    ticket
  }
})
