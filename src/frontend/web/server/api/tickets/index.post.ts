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

  // Automatic escalation detection for robot collisions or critical threshold
  const textToScan = `${body.title} ${body.description} ${Array.isArray(body.tags) ? body.tags.join(' ') : ''}`.toLowerCase()
  let isEscalated = Boolean(body.isEscalated ?? false)
  let escalationReason = body.escalationReason || null
  let escalationTarget = body.escalationTarget || null
  let escalationHandoverState = body.escalationHandoverState || null

  if (textToScan.includes('collision') || textToScan.includes('robot collision') || textToScan.includes('crash')) {
    isEscalated = true
    escalationReason = escalationReason || 'Automatic Trigger: Robot collision detected on cell'
    escalationTarget = 'DedicatedEngineer'
    escalationHandoverState = 'HandOff'
  }

  const isLineStop = Boolean(body.isLineStop ?? false)
  const lineStopDurationMinutes = body.lineStopDurationMinutes !== undefined
    ? Number(body.lineStopDurationMinutes)
    : (isLineStop ? 0 : undefined)

  const newTicket: MaintenanceTicket = {
    id: `tkt-${Date.now()}-${randomSuffix}`,
    ticketNumber,
    stationId: body.stationId || 'GENERAL-FACTORY',
    stationName: body.stationName || body.stationId || 'General Factory Station',
    machineType: body.machineType,
    groupId: body.groupId,
    controllerId: body.controllerId,
    title: body.title,
    description: body.description,
    status: (body.status || 'Open') as any,
    priority,
    category: body.category || 'Error',
    issueType: body.issueType || 'Maintenance',
    originatorType: body.originatorType || 'ManualUser',
    isLineStop,
    lineStopDurationMinutes,
    responsibleDepartment: body.responsibleDepartment || 'Assy',
    externalOperatorId: body.externalOperatorId || null,
    externalOperatorName: body.externalOperatorName || null,
    startedAt: body.status === 'InProgress' ? now.toISOString() : undefined,
    qrScannedAt: body.qrScannedAt || undefined,
    reactionTimeMinutes: body.reactionTimeMinutes || undefined,
    reservedBy: body.reservedBy || undefined,
    errorGroup: body.errorGroup,
    errorCode: body.errorCode,
    tags: Array.isArray(body.tags) ? body.tags : [],
    sfc: body.sfc,
    pendingReason: body.pendingReason || 'None',
    pendingDetails: body.pendingDetails || null,
    isEscalated,
    escalationReason,
    escalationTarget,
    escalationHandoverState,
    escalatedAt: isEscalated ? (body.escalatedAt || now.toISOString()) : null,
    escalatedBy: isEscalated ? (body.escalatedBy || 'System:AutoCollisionRule') : null,
    telemetrySnapshot: body.telemetrySnapshot || {
      timestamp: now.toISOString(),
      metrics: {
        Plc_State: 'RUN',
        Cycle_Time_ms: 1420.5,
        Spindle_Temp_C: 48.2,
        Vibration_Envelope_mm_s: 1.34,
        Hydraulic_Pressure_bar: 152.0
      }
    },
    reportedByUserId,
    reportedByUserName,
    assignedTechnicianId: body.assignedTechnicianId,
    assignedTechnicianName: body.assignedTechnicianName,
    createdAt: now.toISOString(),
    updatedAt: now.toISOString(),
    slaDueAt,
    comments: [],
    attachments: body.attachments || [],
    changeHistory: []
  }

  addTicketToStore(newTicket)

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5001'
  $fetch(`${backendBase}/api/v1/tickets`, {
    method: 'POST',
    body: {
      ...newTicket,
      machineId: newTicket.stationId,
      clientPcId: newTicket.controllerId,
      createdBy: newTicket.reportedByUserName,
      assignedTo: newTicket.assignedTechnicianName
    }
  }).catch(() => {})

  return {
    success: true,
    ticket: newTicket
  }
})
