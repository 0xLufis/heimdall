import { defineEventHandler, readBody, createError } from 'h3'
import { addCommentToTicket, findTicketById } from '../../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = await readBody(event)
  if (!body || !body.content) {
    throw createError({ statusCode: 400, statusMessage: 'Comment content is required' })
  }

  const ticket = findTicketById(id)
  if (!ticket) {
    throw createError({ statusCode: 404, statusMessage: `Ticket '${id}' not found` })
  }

  const newComment = {
    id: `c-${Date.now()}`,
    ticketId: ticket.id,
    authorUserId: body.authorUserId || 'usr-tech-01',
    authorName: body.authorName || 'Technician User',
    content: body.content,
    createdAt: new Date().toISOString()
  }

  addCommentToTicket(ticket.id, newComment)

  return {
    success: true,
    comment: newComment,
    ticket
  }
})
