import { defineEventHandler, createError } from 'h3'
import { findTicketById, fetchAndCacheBackendTicket } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id)
  }
  if (!ticket) {
    throw createError({ statusCode: 404, statusMessage: `Ticket with ID '${id}' not found` })
  }

  return {
    ticket
  }
})
