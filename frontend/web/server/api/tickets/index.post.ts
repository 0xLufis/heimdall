import { defineEventHandler, readBody, createError } from 'h3'
import { addTicketToStore, MaintenanceTicket } from '../../utils/ticketsStore'
import { ensureBetterAuthProfile } from '../../utils/authProfiles'

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

  // Calculate SLA due timestamp based on user override or priority matrix
  let slaHours = body.slaHours ?? 24
  if (!body.slaHours) {
    if (priority === 'Critical') slaHours = 4
    else if (priority === 'High') slaHours = 8
    else if (priority === 'Medium') slaHours = 24
    else if (priority === 'Low') slaHours = 48
  }

  const slaDueAt = new Date(now.getTime() + slaHours * 3600 * 1000).toISOString()

  // Generate unique ticket number
  const randomSuffix = Math.floor(1000 + Math.random() * 9000)
  const dateStr = now.toISOString().slice(0, 10).replace(/-/g, '')
  const ticketNumber = body.ticketNumber || `TKT-${dateStr}-${randomSuffix}`

  const sessionUser = (event.context as any)?.auth?.user
  const reportedByUserId = body.reportedByUserId || sessionUser?.id || 'usr-admin-primary'
  const reportedByUserName = body.reportedByUserName || sessionUser?.name || 'System Administrator'

  // Guarantee that ticket reporter has a Better-Auth user profile
  await ensureBetterAuthProfile(reportedByUserId, { name: reportedByUserName, role: 'operator' })

  // Guarantee assigned technician has a Better-Auth user profile if assigned
  if (body.assignedTechnicianId) {
    await ensureBetterAuthProfile(body.assignedTechnicianId, {
      name: body.assignedTechnicianName,
      role: 'technician'
    })
  }

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
    reportedByUserId,
    reportedByUserName,
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
