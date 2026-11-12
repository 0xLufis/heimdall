import { defineEventHandler, readBody, createError } from 'h3'
import { addCommentToTicket, findTicketById } from '../../../utils/ticketsStore'
import { featureFlags } from '../../../utils/featureFlags'
import { ensureBetterAuthProfile } from '../../../utils/authProfiles'
import { backendAuthHeaders } from '../../../utils/backendProxy'

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

  // Backend calls run as the caller: session bearer and the session's organisation, never raw client headers.
  const authHeaders = await backendAuthHeaders(event)
  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id, authHeaders)
  }
  if (!ticket) {
    ticket = ensureTicketInStore(id)
  }

  const sessionUser = (event.context as any)?.auth?.user
  const authorUserId = body.authorUserId || sessionUser?.id || (featureFlags.enableDevFeatures ? 'usr-tech-01' : 'usr-admin-primary')
  const authorName = body.authorName || sessionUser?.name || (featureFlags.enableDevFeatures ? 'Technician User' : 'System Administrator')

  // Guarantee comment author has a Better-Auth user profile
  await ensureBetterAuthProfile(authorUserId, { name: authorName, role: 'technician' })

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
