import { defineEventHandler, readBody, createError } from 'h3'
import { updateTicketInStore } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = await readBody(event) || {}

  const updatedTicket = updateTicketInStore(id, body)
  if (!updatedTicket) {
    throw createError({ statusCode: 404, statusMessage: `Ticket '${id}' not found for update` })
  }

  return {
    success: true,
    ticket: updatedTicket
  }
})
