import { defineEventHandler, createError } from 'h3'
import { findTicketById } from '../../utils/ticketsStore'

export default defineEventHandler((event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const ticket = findTicketById(id)
  if (!ticket) {
    throw createError({ statusCode: 404, statusMessage: `Ticket with ID '${id}' not found` })
  }

  return {
    ticket
  }
})
