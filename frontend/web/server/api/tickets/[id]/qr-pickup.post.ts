import { defineEventHandler, readBody, createError } from 'h3'
import { qrPickupTicketInStore } from '../../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = event.context.params?.id
  if (!id) {
    throw createError({ statusCode: 400, statusMessage: 'Ticket ID is required' })
  }

  const body = await readBody(event).catch(() => ({}))
  const sessionUser = (event.context as any)?.auth?.user
  const technician = body?.technician || sessionUser?.name || 'On-Floor Technician'

  const updated = qrPickupTicketInStore(id, technician)
  if (!updated) {
    throw createError({ statusCode: 404, statusMessage: 'Ticket not found' })
  }

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5001'
  $fetch(`${backendBase}/api/v1/tickets/${id}/qr-pickup`, {
    method: 'POST',
    body: { technician }
  }).catch(() => {})

  return {
    success: true,
    ticket: updated
  }
})
