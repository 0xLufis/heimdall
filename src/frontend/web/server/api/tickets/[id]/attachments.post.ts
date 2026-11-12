import { defineEventHandler, readBody, createError } from 'h3'
import {
  addAttachmentToTicket,
  findTicketById,
  fetchAndCacheBackendTicket,
  ensureTicketInStore
} from '../../../utils/ticketsStore'
import type { TicketAttachment } from '../../../utils/ticketsStore'
import { backendAuthHeaders } from '../../../utils/backendProxy'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = await readBody(event)
  if (!body || !body.fileName) {
    throw createError({ statusCode: 400, statusMessage: 'fileName is required' })
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

  const attachment: TicketAttachment = {
    id: body.id || `att_${Date.now()}_${Math.random().toString(36).slice(2, 9)}`,
    ticketId: ticket.id,
    commentId: body.commentId || undefined,
    fileName: body.fileName,
    contentType: body.contentType || 'application/octet-stream',
    fileSize: body.fileSize || 0,
    uploadedAt: new Date().toISOString(),
    url: body.url || undefined
  }

  addAttachmentToTicket(ticket.id, attachment)

  return {
    success: true,
    attachment,
    ticket
  }
})
