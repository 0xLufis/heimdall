import { defineEventHandler, readBody, createError } from 'h3'
import {
  autoCloseTicketInStore,
  findTicketById,
  fetchAndCacheBackendTicket,
  ensureTicketInStore
} from '../../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = (await readBody(event).catch(() => ({}))) || {}

  let ticket = findTicketById(id)
  if (!ticket) {
    ticket = await fetchAndCacheBackendTicket(id)
  }
  if (!ticket) {
    ticket = ensureTicketInStore(id)
  }

  const updated = autoCloseTicketInStore(id, {
    machineId: body.machineId,
    controllerId: body.controllerId,
    clearedReason: body.clearedReason || body.reason,
    resolutionNotes: body.resolutionNotes
  })

  if (!updated) {
    throw createError({ statusCode: 404, statusMessage: 'Ticket not found' })
  }

  // Sync with backend if available
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  $fetch(`${backendBase}/api/v1/tickets/${id}/auto-close`, {
    method: 'POST',
    body: {
      machineId: body.machineId,
      controllerId: body.controllerId,
      clearedReason: body.clearedReason || 'Machine fault condition cleared',
      resolutionNotes: body.resolutionNotes
    }
  }).catch(() => {
    if (backendBase.includes('5099')) {
      $fetch(`http://localhost:5001/api/v1/tickets/${id}/auto-close`, {
        method: 'POST',
        body: {
          machineId: body.machineId,
          controllerId: body.controllerId,
          clearedReason: body.clearedReason || 'Machine fault condition cleared'
        }
      }).catch(() => {})
    }
  })

  return {
    success: true,
    ticket: updated
  }
})
