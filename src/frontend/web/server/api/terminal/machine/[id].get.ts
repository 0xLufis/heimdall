import { defineEventHandler, getQuery, getRouterParam, getHeader, createError } from 'h3'
import { requireTerminalAuth, assertTerminalStationAccess } from '../../../utils/terminalAuth'
import { machinesStore } from '../../../utils/machinesStore'
import { ticketsStore } from '../../../utils/ticketsStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Machine identifier is required.'
    })
  }

  // Workstation authentication (operator session, workstation certificate via the trusted proxy, or workstation token)
  const terminalAuth = await requireTerminalAuth(event)

  // Retrieve machine from store (or backend fallback)
  let machine = machinesStore.getMachineById(id)

  if (!machine) {
    const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
    try {
      const backendMachine = await $fetch<any>(`${backendBase}/api/v1/machine/${id}`, {
        headers: event.headers as any
      })
      if (backendMachine) {
        machine = backendMachine
      }
    } catch {
      // Backend unavailable or machine not in database
    }
  }

  if (!machine) {
    throw createError({
      statusCode: 404,
      statusMessage: `Machine station with ID or Identifier "${id}" was not found.`
    })
  }

  // A certificate-authenticated workstation only opens stations bound to its controller.
  assertTerminalStationAccess(terminalAuth, id, machine.id, machine.customIdentifier)

  // Generate live PLC telemetry for this terminal
  const isOperational = machine.status === 'active'
  const isMaintenance = machine.status === 'maintenance'
  const plcState = isOperational ? 'RUN' : (isMaintenance ? 'MAINT' : 'STOP')

  const now = new Date()
  const liveTelemetry = {
    plcState,
    heartbeatTimestamp: now.toISOString(),
    cycleTimeMs: isOperational ? Number((1200 + (Math.sin(now.getTime() / 10000) * 150)).toFixed(1)) : 0,
    spindleSpeedRpm: isOperational ? Math.round(11800 + Math.random() * 400) : 0,
    spindleTemperatureC: isOperational ? Number((42.5 + Math.random() * 5).toFixed(1)) : 22.0,
    vibrationEnvelopeMmS: isOperational ? Number((1.12 + Math.random() * 0.4).toFixed(2)) : 0.05,
    hydraulicPressureBar: isOperational ? Number((148.0 + Math.random() * 6).toFixed(1)) : 0.0,
    productionCount: 4890,
    partDefectCount: 3,
    feedRatePercentage: isOperational ? 100 : 0
  }

  // Strictly read-only recent tickets for this station
  const allTickets = ticketsStore.getAllTickets()
  const recentTickets = allTickets
    .filter(t => t.stationId === machine.id || t.stationName === machine.name || t.stationId === machine.customIdentifier)
    .slice(0, 5)
    .map(t => ({
      id: t.id,
      ticketNumber: t.ticketNumber,
      title: t.title,
      status: t.status,
      priority: t.priority,
      category: t.category,
      createdAt: t.createdAt
    }))

  return {
    machine: {
      id: machine.id,
      name: machine.name,
      displayName: machine.displayName || machine.name,
      customIdentifier: machine.customIdentifier,
      machineType: machine.machineType || 'General Industrial',
      groupId: machine.groupId || 'Line 1',
      status: machine.status,
      controllers: machine.controllers || [],
      responsibleTeams: machine.responsibleTeams || []
    },
    telemetry: liveTelemetry,
    recentTickets,
    workstation: {
      terminalMode: 'TouchscreenKiosk',
      authenticatedVia: terminalAuth.method === 'client-certificate'
        ? 'X509Certificate'
        : terminalAuth.method === 'session'
          ? 'OperatorSession'
          : terminalAuth.method === 'dev-bypass' ? 'DevBypass' : 'WorkstationToken',
      permissions: ['read:machine_status', 'read:telemetry', 'write:terminal_ticket'],
      readOnly: true
    }
  }
})
