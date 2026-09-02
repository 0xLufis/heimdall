import { defineEventHandler, readBody, createError } from 'h3'
import { addTicketToStore, MaintenanceTicket } from '../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)

  if (!body || !body.title || !body.description) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Title and description are required for maintenance tickets.'
    })
  }

  const now = new Date()
  const priority = body.priority || 'Medium'

  // Calculate SLA due timestamp based on priority
  let slaHours = 24
  if (priority === 'Critical') slaHours = 4
  else if (priority === 'High') slaHours = 8
  else if (priority === 'Medium') slaHours = 24
  else if (priority === 'Low') slaHours = 48

  const slaDueAt = new Date(now.getTime() + slaHours * 3600 * 1000).toISOString()

  // Generate unique ticket number
  const randomSuffix = Math.floor(1000 + Math.random() * 9000)
  const dateStr = now.toISOString().slice(0, 10).replace(/-/g, '')
  const ticketNumber = body.ticketNumber || `TKT-${dateStr}-${randomSuffix}`

  const newTicket: MaintenanceTicket = {
    id: `tkt-${Date.now()}-${randomSuffix}`,
    ticketNumber,
    stationId: body.stationId || 'GENERAL-FACTORY',
    stationName: body.stationName || body.stationId || 'General Factory Station',
    controllerId: body.controllerId,
    title: body.title,
    description: body.description,
    status: 'Open',
    priority,
    reportedByUserId: body.reportedByUserId || 'usr-current',
    reportedByUserName: body.reportedByUserName || 'Operator User',
    assignedTechnicianId: body.assignedTechnicianId,
    assignedTechnicianName: body.assignedTechnicianName,
    createdAt: now.toISOString(),
    updatedAt: now.toISOString(),
    slaDueAt,
    comments: [],
    attachments: body.attachments || []
  }

  addTicketToStore(newTicket)

  return {
    success: true,
    ticket: newTicket
  }
})
