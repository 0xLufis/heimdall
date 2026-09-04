import { defineEventHandler, readBody, createError } from 'h3'
import { addCommentToTicket, findTicketById } from '../../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = await readBody(event)
  // Allow empty content for pure transition comments
  if (!body) {
    throw createError({ statusCode: 400, statusMessage: 'Request body is required' })
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
    content: body.content ?? '',
    createdAt: new Date().toISOString(),
    ...(body.transition ? { transition: body.transition } : {}),
    ...(body.attachments && Array.isArray(body.attachments) && body.attachments.length > 0
      ? { attachments: body.attachments }
      : {})
  }

  addCommentToTicket(ticket.id, newComment)

  return {
    success: true,
    comment: newComment,
    ticket
  }
})
