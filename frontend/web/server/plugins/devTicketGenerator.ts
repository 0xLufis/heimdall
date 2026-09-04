import { defineNitroPlugin } from 'nitropack/runtime'
import { getTicketsStore, addTicketToStore, getTicketSettings, MaintenanceTicket } from '../utils/ticketsStore'

// ---------------------------------------------------------------------------
// Inline template pool – mirrors the error-template-engine structure.
// Cannot import from app/utils in a server plugin.
// ---------------------------------------------------------------------------
interface FbState {
  blockName: string
  state: string
  errorCode: string
}

interface DevTemplate {
  code: string
  category: 'Error' | 'Prevention' | 'Improvement' | 'ETC'
  group: string
  title: string
  targetStatus: string
  tags: string[]
  fbState: FbState | null
  /** Telemetry metric keys that will be populated with realistic numeric values */
  telemetryKeys: string[]
}

const TEMPLATE_POOL: DevTemplate[] = [
  {
    code: 'E-MOT-01',
    category: 'Error',
    group: 'Motion & Drive',
    title: 'Axis Position Divergence',
    targetStatus: 'In_Progress',
    tags: ['#Motion', '#Axis'],
    fbState: { blockName: 'FB_AxisControl', state: 'ERROR_STOP', errorCode: '16#4330' },
    telemetryKeys: ['axisPosition_mm', 'setpointPosition_mm', 'divergence_mm', 'torqueCurrent_A']
  },
  {
    code: 'E-SAFE-01',
    category: 'Error',
    group: 'Safety System',
    title: 'Light Curtain Muting Fault',
    targetStatus: 'Escalated',
    tags: ['#Safety', '#SIL2'],
    fbState: { blockName: 'FB_SafetyDoor', state: 'SAFE_STOP_2', errorCode: '16#F001' },
    telemetryKeys: ['safetyCategory', 'mutingSignal_ms', 'responseTime_ms', 'plcDiag']
  },
  {
    code: 'E-VIS-01',
    category: 'Error',
    group: 'Vision & Optical',
    title: 'AOI Blob Detection Reject Limit',
    targetStatus: 'Open',
    tags: ['#AOI', '#Vision'],
    fbState: { blockName: 'FB_CameraControl', state: 'RUNNING', errorCode: '16#3001' },
    telemetryKeys: ['rejectRate_pct', 'inspectedParts', 'failedParts', 'cameraTemp_C', 'lightIntensity_lux']
  },
  {
    code: 'E-DISP-01',
    category: 'Error',
    group: 'Dispensing',
    title: 'Gap Filler Nozzle Pressure Sag',
    targetStatus: 'Pending_Parts',
    tags: ['#GapFiller', '#Parts'],
    fbState: { blockName: 'FB_DispensingControl', state: 'MATERIAL_LOW', errorCode: '16#5001' },
    telemetryKeys: ['nozzlePressure_bar', 'setpointPressure_bar', 'materialVolume_ml', 'pumpRpm']
  },
  {
    code: 'P-CAL-01',
    category: 'Prevention',
    group: 'Calibration',
    title: 'Load Cell Calibration Due',
    targetStatus: 'Closure_Pending',
    tags: ['#PM', '#Calibration'],
    fbState: null,
    telemetryKeys: ['loadCellOffset_N', 'lastCalibration_days', 'driftFactor_pct']
  },
  {
    code: 'E-SCRW-01',
    category: 'Error',
    group: 'Screwing',
    title: 'Final Torque Angle Window Exceeded',
    targetStatus: 'Open',
    tags: ['#Screwing', '#NOK'],
    fbState: { blockName: 'FB_NutRunner', state: 'NOK', errorCode: '16#6001' },
    telemetryKeys: ['finalTorque_Nm', 'torqueLimitHigh_Nm', 'torqueLimitLow_Nm', 'angleWindow_deg', 'screwCycles']
  }
]

// ---------------------------------------------------------------------------
// Machine pool for realistic station assignment
// ---------------------------------------------------------------------------
const MACHINE_POOL = [
  { stationId: 'L06-OP150',        stationName: 'Line 06 – Battery Module Line',             controllerId: 'CPC-081' },
  { stationId: 'ROBOT-CELL-01',    stationName: 'Robotic Welding Cell 01',                   controllerId: 'CPC-001' },
  { stationId: 'L09-OP270',        stationName: 'Line 09 – Optical Quality Inspection',      controllerId: 'CPC-159' },
  { stationId: 'L05-OP80',         stationName: 'Line 05 – Powertrain Sub-Assembly',         controllerId: 'CPC-470' },
  { stationId: 'L08-OP50',         stationName: 'Line 08 – Surface Coating & Paint Shop',    controllerId: 'CPC-203' },
  { stationId: 'STATION-OP10-01',  stationName: 'OP10 Machining Cell',                       controllerId: 'CPC-101' },
  { stationId: 'ASSEMBLY-ST-02',   stationName: 'SIMATIC S7 Conveyor Station 02',            controllerId: 'CPC-038' },
  { stationId: 'ROBOT-CELL-04',    stationName: 'Fanuc Palletizing Cell 04',                 controllerId: 'CPC-095' }
]

// ---------------------------------------------------------------------------
// Telemetry value ranges [min, max, decimals]
// ---------------------------------------------------------------------------
const TELEMETRY_RANGES: Record<string, [number, number, number]> = {
  axisPosition_mm:       [0,      1200,  2],
  setpointPosition_mm:   [0,      1200,  2],
  divergence_mm:         [0.5,    12,    3],
  torqueCurrent_A:       [1.2,    8.5,   2],
  mutingSignal_ms:       [0,      500,   0],
  responseTime_ms:       [5,      120,   0],
  safetyCategory:        [1,      4,     0],
  plcDiag:               [0,      255,   0],
  rejectRate_pct:        [5,      30,    1],
  inspectedParts:        [100,    5000,  0],
  failedParts:           [5,      300,   0],
  cameraTemp_C:          [35,     65,    1],
  lightIntensity_lux:    [800,    3000,  0],
  nozzlePressure_bar:    [1.5,    4.2,   2],
  setpointPressure_bar:  [4.0,    5.5,   2],
  materialVolume_ml:     [0,      500,   0],
  pumpRpm:               [200,    1800,  0],
  loadCellOffset_N:      [-2.5,   2.5,   3],
  lastCalibration_days:  [90,     730,   0],
  driftFactor_pct:       [0.1,    3.5,   2],
  finalTorque_Nm:        [5,      50,    1],
  torqueLimitHigh_Nm:    [45,     55,    1],
  torqueLimitLow_Nm:     [4,      8,     1],
  angleWindow_deg:       [50,     420,   0],
  screwCycles:           [500,    50000, 0]
}

function randomBetween(min: number, max: number, decimals = 1): number {
  const val = min + Math.random() * (max - min)
  return parseFloat(val.toFixed(decimals))
}

function buildTelemetry(keys: string[]): { timestamp: string; metrics: Record<string, string | number> } {
  const metrics: Record<string, string | number> = {}
  for (const key of keys) {
    const range = TELEMETRY_RANGES[key]
    metrics[key] = range ? randomBetween(...range) : 0
  }
  return { timestamp: new Date().toISOString(), metrics }
}

// ---------------------------------------------------------------------------
// Priority mapping per template code
// ---------------------------------------------------------------------------
const TEMPLATE_PRIORITY: Record<string, MaintenanceTicket['priority']> = {
  'E-MOT-01':  'High',
  'E-SAFE-01': 'Critical',
  'E-VIS-01':  'Medium',
  'E-DISP-01': 'Medium',
  'P-CAL-01':  'Low',
  'E-SCRW-01': 'High'
}

// ---------------------------------------------------------------------------
// Per-template human-readable description generator
// ---------------------------------------------------------------------------
function buildDescription(template: DevTemplate, machineName: string): string {
  switch (template.code) {
    case 'E-MOT-01':
      return (
        `Servo axis reported position divergence exceeding threshold at ${machineName}. ` +
        `FB "${template.fbState?.blockName}" halted in ${template.fbState?.state} ` +
        `with diagnostic code ${template.fbState?.errorCode}.`
      )
    case 'E-SAFE-01':
      return (
        `Safety PLC detected muting fault on light curtain barrier at ${machineName}. ` +
        `SIL-2 interlock triggered – zone locked out. ` +
        `Manual reset required after root-cause verification.`
      )
    case 'E-VIS-01':
      return (
        `AOI blob-detection reject rate exceeded the 15 % threshold at ${machineName}. ` +
        `Possible lens contamination or illumination drift. ` +
        `Awaiting vision engineer inspection.`
      )
    case 'E-DISP-01':
      return (
        `Gap-filler nozzle pressure sag detected at ${machineName}. ` +
        `Material reservoir may be low or pump wear suspected. ` +
        `Parts order likely required.`
      )
    case 'P-CAL-01':
      return (
        `Scheduled preventive maintenance: load cell calibration interval reached at ${machineName}. ` +
        `Calibration certificate renewal required before the next production window.`
      )
    case 'E-SCRW-01':
      return (
        `Nut-runner reported NOK result – final torque angle outside acceptance window at ${machineName}. ` +
        `Part quarantined for re-work. ` +
        `FB "${template.fbState?.blockName}" in state ${template.fbState?.state} ` +
        `(code ${template.fbState?.errorCode}).`
      )
    default:
      return `Automated anomaly detected at ${machineName} – template code ${template.code}.`
  }
}

// ---------------------------------------------------------------------------
// Plugin entry point
// ---------------------------------------------------------------------------
export default defineNitroPlugin(() => {
  // Run exclusively in development mode
  if (process.env.NODE_ENV !== 'development') {
    return
  }

  const MAX_TICKETS = 50
  const INTERVAL_MS = 40 * 1000 // 40 seconds

  // Sequential counter for SFC serial numbers within one server session
  let sfcSeqNum = 1

  console.log('[DevTicketGen] Initialized dev ticket generator (interval: 40s, cap: 50)')

  setInterval(() => {
    try {
      const settings = getTicketSettings()
      if (!settings.devTicketGenEnabled) {
        return
      }

      const currentTickets = getTicketsStore()
      if (currentTickets.length >= MAX_TICKETS) {
        return
      }

      // Pick a random error template and a random machine
      const template = TEMPLATE_POOL[Math.floor(Math.random() * TEMPLATE_POOL.length)]
      const machine  = MACHINE_POOL[Math.floor(Math.random() * MACHINE_POOL.length)]

      const now          = new Date()
      const dateStr      = now.toISOString().slice(0, 10).replace(/-/g, '')
      const randomSuffix = Math.floor(1000 + Math.random() * 9000)

      // SFC serial: SFC-BAT-YYYYMMDD-0001
      const sfcSerial    = `SFC-BAT-${dateStr}-${String(sfcSeqNum++).padStart(4, '0')}`
      const ticketNumber = `TKT-${dateStr}-${randomSuffix}`

      const priority = TEMPLATE_PRIORITY[template.code] ?? 'Medium'
      let slaHours = 24
      if (priority === 'Critical') slaHours = 4
      else if (priority === 'High') slaHours = 8
      else if (priority === 'Medium') slaHours = 24
      else if (priority === 'Low') slaHours = 48

      const slaDueAt = new Date(now.getTime() + slaHours * 3600 * 1000).toISOString()

      // If auto-assign is enabled, assign the lead tech; otherwise leave unassigned
      const assignedTech = settings.autoAssignTickets ? 'Gábor Varga (Lead Tech)' : undefined

      // Ticket title carries the error code prefix for quick identification
      const ticketTitle = `[${template.code}] ${template.title} @ ${machine.stationName}`

      const newTicket = {
        id:           `tkt-dev-${Date.now()}-${randomSuffix}`,
        ticketNumber,
        stationId:    machine.stationId,
        stationName:  machine.stationName,
        controllerId: machine.controllerId,
        title:        ticketTitle,
        description:  buildDescription(template, machine.stationName),
        // targetStatus may exceed the narrow server-store union (e.g. 'Escalated');
        // cast to satisfy the type while preserving the richer value for the frontend.
        status:       template.targetStatus as MaintenanceTicket['status'],
        priority,
        // Extended fields recognised by the frontend MaintenanceTicket type
        category:          template.category,
        errorGroup:        template.group,
        errorCode:         template.code,
        tags:              template.tags,
        fbState:           template.fbState ?? undefined,
        sfc:               sfcSerial,
        telemetrySnapshot: buildTelemetry(template.telemetryKeys),
        reportedByUserId:   'usr-autonomous-agent',
        reportedByUserName: 'Autonomous Fleet Telemetry (Dev)',
        assignedTechnicianName: assignedTech,
        createdAt:   now.toISOString(),
        updatedAt:   now.toISOString(),
        slaDueAt,
        comments:    [],
        attachments: []
      }

      addTicketToStore(newTicket as unknown as MaintenanceTicket)
      console.log(
        `[DevTicketGen] Generated ticket ${newTicket.ticketNumber} ` +
        `[${template.code}] for ${machine.stationName} ` +
        `(SFC: ${sfcSerial}, Status: ${template.targetStatus}, ` +
        `Count: ${getTicketsStore().length}/${MAX_TICKETS})`
      )
    } catch (err) {
      console.error('[DevTicketGen] Error generating dev ticket:', err)
    }
  }, INTERVAL_MS)
})
