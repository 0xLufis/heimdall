import { defineEventHandler, readBody, createError } from 'h3'
import { addCommentToTicket, findTicketById } from '../../../utils/ticketsStore'
import { featureFlags } from '../../../utils/featureFlags'

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

  const sessionUser = (event.context as any)?.auth?.user
  const authorUserId = body.authorUserId || sessionUser?.id || (featureFlags.enableDevFeatures ? 'usr-tech-01' : 'anonymous')
  const authorName = body.authorName || sessionUser?.name || (featureFlags.enableDevFeatures ? 'Technician User' : 'Anonymous User')

  const newComment = {
    id: `c-${Date.now()}`,
    ticketId: ticket.id,
    authorUserId,
    authorName,
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
