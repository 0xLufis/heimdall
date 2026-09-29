import { defineEventHandler, readBody, createError } from 'h3'
import { editTicketWithHistoryInStore, updateTicketInStore } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = (await readBody(event)) || {}
  const sessionUser = (event.context as any)?.auth?.user
  const editorName = body.editorName || sessionUser?.name || 'Administrator'
  const reason = body.changeReason || body.reason || 'Field update / correction'

  const updatedTicket = editTicketWithHistoryInStore(id, body, editorName, reason) || updateTicketInStore(id, body)
  if (!updatedTicket) {
    throw createError({ statusCode: 404, statusMessage: `Ticket '${id}' not found for update` })
  }

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5001'
  $fetch(`${backendBase}/api/v1/tickets/${id}`, {
    method: 'PUT',
    body: {
      ...updatedTicket,
      machineId: updatedTicket.stationId,
      clientPcId: updatedTicket.controllerId,
      createdBy: updatedTicket.reportedByUserName,
      assignedTo: updatedTicket.assignedTechnicianName
    }
  }).catch(() => {})

  return {
    success: true,
    ticket: updatedTicket
  }
})
