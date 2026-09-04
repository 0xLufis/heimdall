import { randomUUID } from 'node:crypto'

export interface StateTransitionMeta {
  fromStatus: string
  toStatus: string
  reason?: string
  actor?: string
}

export interface TicketComment {
  id: string
  ticketId: string
  authorUserId: string
  authorName: string
  content: string
  createdAt: string
  transition?: StateTransitionMeta
  attachments?: TicketAttachment[]
}

export interface TicketAttachment {
  id: string
  ticketId: string
  commentId?: string
  fileName: string
  contentType: string
  fileSize: number
  uploadedAt: string
  url?: string
}

export type TicketStatus =
  | 'Open'
  | 'In_Progress'
  | 'Pending_Parts'
  | 'Escalated'
  | 'Escalated_External'
  | 'Closure_Pending'
  | 'Resolved'
  | 'Closed_Unresolved'
  | 'Closed'
  | 'Draft'

export interface FunctionBlockState {
  blockName: string
  state: string
  subState?: string
  errorCode?: string
}

export interface TelemetrySnapshot {
  timestamp: string
  metrics: Record<string, string | number>
}

export interface MaintenanceTicket {
  id: string
  ticketNumber: string
  stationId: string
  stationName: string
  machineType?: string
  controllerId?: string
  title: string
  description: string
  status: TicketStatus
  priority: 'Low' | 'Medium' | 'High' | 'Critical'
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC'
  errorGroup?: string
  errorCode?: string
  tags?: string[]
  fbState?: FunctionBlockState
  sfc?: string
  telemetrySnapshot?: TelemetrySnapshot
  externalEscalationTarget?: string
  reportedByUserId: string
  reportedByUserName: string
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  createdAt: string
  updatedAt: string
  slaDueAt: string
  resolvedAt?: string
  comments: TicketComment[]
  attachments: TicketAttachment[]
  metadata?: Record<string, any>
}

function hoursAgo(h: number): string {
  return new Date(Date.now() - h * 3600 * 1000).toISOString()
}

function hoursFromNow(h: number): string {
  return new Date(Date.now() + h * 3600 * 1000).toISOString()
}

function daysAgo(d: number): string {
  return new Date(Date.now() - d * 86400 * 1000).toISOString()
}

function daysFromNow(d: number): string {
  return new Date(Date.now() + d * 86400 * 1000).toISOString()
}

function sfcSerial(dateStr: string, seq: string): string {
  return `SFC-BAT-${dateStr}-${seq}`
}

const initialTickets: MaintenanceTicket[] = [
  {
    id: 'tkt-001',
    ticketNumber: 'TKT-20260830-0001',
    stationId: 'STATION-OP10-01',
    stationName: 'OP10 Machining Cell',
    machineType: 'Milling',
    controllerId: 'ctrl-101',
    title: '[E-MILL-01] Spindle Vibration Harmonic Peak',
    description: 'Spindle vibration harmonic peak exceeded 4.5 mm/s RMS at 3,200 Hz frequency band. Likely toolholder imbalance or tool wear. Spindle balance check and tool replacement required.',
    status: 'In_Progress',
    priority: 'Critical',
    category: 'Error',
    errorGroup: 'Milling & Machining',
    errorCode: 'E-MILL-01',
    groupId: 'grp-line06',
    tags: ['#Milling', '#Vibration', '#Spindle', '#ToolWear'],
    fbState: { blockName: 'FB_SpindleControl', state: 'WARNING', subState: 'VibrationHigh', errorCode: '16#9001' },
    sfc: sfcSerial('20260903', '0042'),
    telemetrySnapshot: {
      timestamp: hoursAgo(3.5),
      metrics: {
        spindle_temp_c: 85,
        vibration_mm_s: 12.4,
        coolant_level_pct: 97,
        feed_rate_mm_min: 4200,
        alarm_code: 'GF-THM-0041'
      }
    },
    reportedByUserId: 'usr-op-01',
    reportedByUserName: 'István Kovács (Operator)',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga (Lead Tech)',
    createdAt: hoursAgo(3),
    updatedAt: hoursAgo(1),
    slaDueAt: hoursFromNow(1),
    comments: [
      {
        id: 'c-001-01',
        ticketId: 'tkt-001',
        authorUserId: 'usr-op-01',
        authorName: 'István Kovács',
        content: 'Reported during 2nd shift execution. Coolant level is normal.',
        createdAt: hoursAgo(3),
        attachments: []
      },
      {
        id: 'c-001-02',
        ticketId: 'tkt-001',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Inspected with thermal camera. Bearing housing replacement scheduled for tonight.',
        createdAt: hoursAgo(1),
        attachments: []
      }
    ],
    attachments: [
      {
        id: 'att-001-01',
        ticketId: 'tkt-001',
        fileName: 'thermal_scan_gf_l06.jpg',
        contentType: 'image/jpeg',
        fileSize: 412000,
        uploadedAt: hoursAgo(2),
        url: '/uploads/thermal_scan_gf_l06.jpg'
      }
    ]
  },

  // 2 — Pending_Parts / High
  {
    id: 'tkt-002',
    ticketNumber: 'TKT-20260903-0002',
    stationId: 'STATION-SC-L06-03',
    stationName: 'Line 06 — Screwing Station #3',
    controllerId: 'ctrl-201',
    title: 'KUKA Robot Axis 3 Servo Alarm E-409',
    description: 'Robot controller halted execution with position feedback divergence error. Encoder cable suspected damaged.',
    status: 'Pending_Parts',
    priority: 'High',
    category: 'Electrical',
    errorGroup: 'Servo Drive',
    errorCode: 'E-409',
    machineType: 'Screwing Station',
    groupId: 'grp-cell-a',
    tags: ['robot', 'servo', 'kuka', 'encoder'],
    fbState: 'E_STOP',
    sfc: sfcSerial('20260903', '0038'),
    telemetrySnapshot: {
      capturedAt: hoursAgo(6.2),
      values: {
        axis3_pos_error_mm: 3.7,
        drive_current_a: 0,
        alarm_code: 'E-409',
        last_good_cycle_count: 14820
      }
    },
    reportedByUserId: 'usr-op-02',
    reportedByUserName: 'Péter Tóth',
    assignedTechnicianId: 'usr-tech-02',
    assignedTechnicianName: 'Zoltán Németh',
    createdAt: hoursAgo(6),
    updatedAt: hoursAgo(2),
    slaDueAt: hoursFromNow(2),
    comments: [
      {
        id: 'c-002-01',
        ticketId: 'tkt-002',
        authorUserId: 'usr-tech-02',
        authorName: 'Zoltán Németh',
        content: 'Encoder cable damaged at drag-chain exit. Replacement part ordered from spare store (ETA: 4 h).',
        createdAt: hoursAgo(2),
        attachments: []
      }
    ],
    attachments: []
  },

  // 3 — Open / Medium
  {
    id: 'tkt-003',
    ticketNumber: 'TKT-20260903-0003',
    stationId: 'STATION-AOI-L06-02',
    stationName: 'Line 06 — Automatic Optical Inspection #2',
    title: 'Cognex Camera Lens Cleaning & Calibration Required',
    description: 'Routine maintenance: dirty lens optics triggering false reject rate of 1.4%. Quality hold placed on last 200 assemblies.',
    status: 'Open',
    priority: 'Medium',
    category: 'Process',
    errorGroup: 'Optics Contamination',
    errorCode: 'AOI-CAM-ERR-07',
    machineType: 'Automatic Optical Inspection',
    groupId: 'grp-line06',
    tags: ['aoi', 'camera', 'calibration', 'quality'],
    fbState: 'QUALITY_HOLD',
    sfc: sfcSerial('20260903', '0040'),
    telemetrySnapshot: {
      capturedAt: hoursAgo(12),
      values: {
        false_reject_rate_pct: 1.4,
        illumination_intensity_lx: 3800,
        last_calibration_days_ago: 32
      }
    },
    reportedByUserId: 'usr-op-03',
    reportedByUserName: 'Katalin Nagy',
    createdAt: hoursAgo(12),
    updatedAt: hoursAgo(12),
    slaDueAt: hoursFromNow(12),
    comments: [],
    attachments: []
  },

  // 4 — Resolved / Low
  {
    id: 'tkt-004',
    ticketNumber: 'TKT-20260902-0004',
    stationId: 'IPC-L09-01',
    stationName: 'IPC Line 09 Master Controller',
    title: 'Win10 IoT System Update & OPC-UA Driver Patch',
    description: 'Apply security rollup KB5040442 and update OPC-UA client protocol bindings to v1.05.03.',
    status: 'Resolved',
    priority: 'Low',
    category: 'Software',
    errorGroup: 'OS Update',
    machineType: 'Tester Cell',
    groupId: 'grp-line09',
    tags: ['windows', 'opc-ua', 'patch', 'software'],
    fbState: 'READY',
    durationMinutes: 95,
    aokSignOff: {
      signedOffBy: 'Anna Szabó (QA Lead)',
      signedOffAt: hoursAgo(3),
      remarks: 'System restarted and verified. All OPC tags nominal.'
    },
    reportedByUserId: 'usr-admin',
    reportedByUserName: 'System Admin',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga (Lead Tech)',
    createdAt: daysAgo(1),
    updatedAt: hoursAgo(4),
    slaDueAt: hoursFromNow(24),
    resolvedAt: hoursAgo(4),
    comments: [
      {
        id: 'c-004-01',
        ticketId: 'tkt-004',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Patch applied successfully. Gateway ping tests verified.',
        createdAt: hoursAgo(4),
        attachments: []
      }
    ],
    attachments: []
  },

  // 5 — Escalated / Critical
  {
    id: 'tkt-005',
    ticketNumber: 'TKT-20260903-0005',
    stationId: 'STATION-PRESS-L09-01',
    stationName: 'Line 09 — Pressing Station #1',
    controllerId: 'ctrl-301',
    title: 'Hydraulic Press Force Deviation >5 kN',
    description: 'Press force sensor reporting consistent 5.3 kN deviation from set-point of 48 kN. Suspected cylinder seal leak. Batch on quality hold.',
    status: 'Escalated',
    priority: 'Critical',
    category: 'Mechanical',
    errorGroup: 'Hydraulic Fault',
    errorCode: 'PRESS-HYD-015',
    machineType: 'Pressing',
    groupId: 'grp-line09',
    tags: ['hydraulic', 'press', 'seal', 'line-09', 'quality-hold'],
    fbState: 'FAULT_STOP',
    sfc: sfcSerial('20260903', '0051'),
    telemetrySnapshot: {
      capturedAt: hoursAgo(1.5),
      values: {
        press_force_kn: 42.7,
        setpoint_kn: 48.0,
        deviation_kn: 5.3,
        hydraulic_pressure_bar: 142,
        oil_temp_c: 61,
        alarm_code: 'PRESS-HYD-015'
      }
    },
    reportedByUserId: 'usr-op-04',
    reportedByUserName: 'Mária Fekete',
    assignedTechnicianId: 'usr-tech-03',
    assignedTechnicianName: 'Bence Horváth',
    createdAt: hoursAgo(2),
    updatedAt: hoursAgo(0.5),
    slaDueAt: hoursFromNow(0.5),
    comments: [
      {
        id: 'c-005-01',
        ticketId: 'tkt-005',
        authorUserId: 'usr-tech-03',
        authorName: 'Bence Horváth',
        content: 'Initial inspection complete. Cylinder seal leaking at end-cap. Escalating to shift leader — senior authorization needed.',
        createdAt: hoursAgo(1),
        attachments: []
      },
      {
        id: 'c-005-sys-01',
        ticketId: 'tkt-005',
        authorName: 'System',
        content: '',
        transition: { fromStatus: 'In_Progress', toStatus: 'Escalated', actor: 'Bence Horváth' },
        createdAt: hoursAgo(0.5),
        attachments: []
      }
    ],
    attachments: []
  },

  // 6 — Escalated_External / High
  {
    id: 'tkt-006',
    ticketNumber: 'TKT-20260902-0006',
    stationId: 'STATION-TC-L09-02',
    stationName: 'Line 09 — Tester Cell #2',
    controllerId: 'ctrl-401',
    title: 'EOL Tester Firmware Bug — False Leakage Fail',
    description: 'End-of-line tester firmware v2.4.1 flagging false leakage failures on valid assemblies. Pattern confirmed after 6 sequential failures.',
    status: 'Escalated_External',
    priority: 'High',
    category: 'Software',
    errorGroup: 'Firmware Bug',
    errorCode: 'TC-FW-224',
    machineType: 'Tester Cell',
    groupId: 'grp-line09',
    tags: ['firmware', 'eol-tester', 'false-fail', 'vendor'],
    fbState: 'INSPECTION_FAIL',
    sfc: sfcSerial('20260902', '0088'),
    externalEscalationTarget: 'Hioki Instruments — Support Ticket #HK-20260902-7741',
    telemetrySnapshot: {
      capturedAt: daysAgo(1),
      values: {
        leakage_measured_ma: 0.02,
        leakage_limit_ma: 0.01,
        fw_version: '2.4.1',
        consecutive_false_fails: 6
      }
    },
    reportedByUserId: 'usr-tech-04',
    reportedByUserName: 'Orsolya Pap',
    assignedTechnicianId: 'usr-tech-04',
    assignedTechnicianName: 'Orsolya Pap',
    createdAt: daysAgo(1),
    updatedAt: hoursAgo(5),
    slaDueAt: hoursFromNow(6),
    comments: [
      {
        id: 'c-006-01',
        ticketId: 'tkt-006',
        authorUserId: 'usr-tech-04',
        authorName: 'Orsolya Pap',
        content: 'Firmware rolled back to v2.3.9 as temporary workaround. Hioki contacted for root-cause patch.',
        createdAt: hoursAgo(5),
        attachments: []
      },
      {
        id: 'c-006-sys-01',
        ticketId: 'tkt-006',
        authorName: 'System',
        content: '',
        transition: { fromStatus: 'In_Progress', toStatus: 'Escalated_External', actor: 'Orsolya Pap' },
        createdAt: hoursAgo(5),
        attachments: []
      }
    ],
    attachments: [
      {
        id: 'att-006-01',
        ticketId: 'tkt-006',
        fileName: 'hioki_support_email.pdf',
        contentType: 'application/pdf',
        fileSize: 87000,
        uploadedAt: hoursAgo(4.5),
        url: '/uploads/hioki_support_email.pdf'
      }
    ]
  },

  // 7 — Closure_Pending / Medium
  {
    id: 'tkt-007',
    ticketNumber: 'TKT-20260901-0007',
    stationId: 'STATION-GF-L06-02',
    stationName: 'Line 06 — Gap Filler Station #2',
    controllerId: 'ctrl-102',
    title: 'Dispensing Needle Clog — Adhesive Viscosity Fault',
    description: 'Gap filler reported material flow below threshold. Needle partially clogged. Needle replaced and production resumed.',
    status: 'Closure_Pending',
    priority: 'Medium',
    category: 'Process',
    errorGroup: 'Material Flow',
    errorCode: 'GF-FLOW-008',
    machineType: 'Gap Filler',
    groupId: 'grp-cell-a',
    tags: ['dispensing', 'needle', 'adhesive', 'gap-filler'],
    fbState: 'IDLE',
    sfc: sfcSerial('20260901', '0014'),
    durationMinutes: 47,
    telemetrySnapshot: {
      capturedAt: daysAgo(2),
      values: {
        flow_rate_g_min: 3.2,
        flow_setpoint_g_min: 8.0,
        adhesive_temp_c: 22,
        adhesive_viscosity_mpa: 51000,
        alarm_code: 'GF-FLOW-008'
      }
    },
    reportedByUserId: 'usr-op-01',
    reportedByUserName: 'István Kovács',
    assignedTechnicianId: 'usr-tech-02',
    assignedTechnicianName: 'Zoltán Németh',
    createdAt: daysAgo(2),
    updatedAt: hoursAgo(8),
    slaDueAt: hoursAgo(16),
    resolvedAt: hoursAgo(8),
    comments: [
      {
        id: 'c-007-01',
        ticketId: 'tkt-007',
        authorUserId: 'usr-tech-02',
        authorName: 'Zoltán Németh',
        content: 'Needle replaced. Machine back in production. Awaiting QA sign-off before closing.',
        createdAt: hoursAgo(8),
        attachments: []
      },
      {
        id: 'c-007-sys-01',
        ticketId: 'tkt-007',
        authorName: 'System',
        content: '',
        transition: { fromStatus: 'In_Progress', toStatus: 'Closure_Pending', actor: 'Zoltán Németh' },
        createdAt: hoursAgo(8),
        attachments: []
      }
    ],
    attachments: []
  },

  // 8 — Closed_Unresolved / High
  {
    id: 'tkt-008',
    ticketNumber: 'TKT-20260831-0008',
    stationId: 'STATION-SC-L06-01',
    stationName: 'Line 06 — Screwing Station #1',
    controllerId: 'ctrl-202',
    title: 'Torque Wrench Calibration Discrepancy — Intermittent',
    description: 'Intermittent torque under-spec measured by inline quality checker. Issue vanished after shift change. Station monitored for recurrence.',
    status: 'Closed_Unresolved',
    priority: 'High',
    category: 'Mechanical',
    errorGroup: 'Torque Fault',
    errorCode: 'SC-TRQ-019',
    machineType: 'Screwing Station',
    groupId: 'grp-cell-a',
    tags: ['torque', 'intermittent', 'calibration', 'monitor'],
    fbState: 'READY',
    sfc: sfcSerial('20260831', '0071'),
    closeReason: 'Intermittent fault — no reproduction in 4 h monitoring. Re-open if recurs.',
    durationMinutes: 240,
    telemetrySnapshot: {
      capturedAt: daysAgo(3),
      values: {
        torque_nm: 14.2,
        torque_setpoint_nm: 18.0,
        deviation_pct: 21.1,
        occurrence_count: 3,
        alarm_code: 'SC-TRQ-019'
      }
    },
    reportedByUserId: 'usr-op-05',
    reportedByUserName: 'Lajos Bíró',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga (Lead Tech)',
    createdAt: daysAgo(3),
    updatedAt: daysAgo(2),
    slaDueAt: daysAgo(2),
    comments: [
      {
        id: 'c-008-01',
        ticketId: 'tkt-008',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Monitored 4 h — no reproduction. Closing as unresolved with monitor note.',
        createdAt: daysAgo(2),
        attachments: []
      },
      {
        id: 'c-008-sys-01',
        ticketId: 'tkt-008',
        authorName: 'System',
        content: '',
        transition: { fromStatus: 'In_Progress', toStatus: 'Closed_Unresolved', actor: 'Gábor Varga' },
        createdAt: daysAgo(2),
        attachments: []
      }
    ],
    attachments: []
  },

  // 9 — Closed / Low
  {
    id: 'tkt-009',
    ticketNumber: 'TKT-20260830-0009',
    stationId: 'STATION-PRESS-L09-02',
    stationName: 'Line 09 — Pressing Station #2',
    title: 'Preventive Maintenance — Cylinder Seal Kit Replacement',
    description: 'Scheduled PM at 250 000-cycle interval. Seal kit replaced, re-test cycle passed.',
    status: 'Closed',
    priority: 'Low',
    category: 'Mechanical',
    errorGroup: 'Preventive Maintenance',
    machineType: 'Pressing',
    groupId: 'grp-line09',
    tags: ['pm', 'preventive', 'cylinder', 'seal'],
    fbState: 'READY',
    sfc: sfcSerial('20260830', '0099'),
    durationMinutes: 130,
    aokSignOff: {
      signedOffBy: 'Anna Szabó (QA Lead)',
      signedOffAt: daysAgo(4),
      remarks: 'PM completed per checklist. First 10 cycles verified OK.'
    },
    reportedByUserId: 'usr-admin',
    reportedByUserName: 'CMMS Scheduler',
    assignedTechnicianId: 'usr-tech-03',
    assignedTechnicianName: 'Bence Horváth',
    createdAt: daysAgo(5),
    updatedAt: daysAgo(4),
    slaDueAt: daysAgo(4),
    resolvedAt: daysAgo(4),
    comments: [
      {
        id: 'c-009-01',
        ticketId: 'tkt-009',
        authorUserId: 'usr-tech-03',
        authorName: 'Bence Horváth',
        content: 'Seal kit replaced per PM checklist. 10-cycle test passed all force spec targets.',
        createdAt: daysAgo(4),
        attachments: []
      }
    ],
    attachments: [
      {
        id: 'att-009-01',
        ticketId: 'tkt-009',
        fileName: 'pm_checklist_press_l09.pdf',
        contentType: 'application/pdf',
        fileSize: 124000,
        uploadedAt: daysAgo(4),
        url: '/uploads/pm_checklist_press_l09.pdf'
      }
    ]
  },

  // 10 — Open / Critical (brand-new)
  {
    id: 'tkt-010',
    ticketNumber: 'TKT-20260903-0010',
    stationId: 'STATION-AOI-L09-01',
    stationName: 'Line 09 — Automatic Optical Inspection #1',
    controllerId: 'ctrl-501',
    title: 'AOI Vision System — Lighting Array Partial Failure',
    description: 'Four of 12 LED segments in Ring Light A failed. Inspection accuracy degraded. All inspected parts flagged for manual re-check.',
    status: 'Open',
    priority: 'Critical',
    category: 'Electrical',
    errorGroup: 'Lighting Fault',
    errorCode: 'AOI-LIGHT-003',
    machineType: 'Automatic Optical Inspection',
    groupId: 'grp-line09',
    tags: ['aoi', 'led', 'lighting', 'vision', 'line-09'],
    fbState: 'DEGRADED',
    sfc: sfcSerial('20260903', '0060'),
    telemetrySnapshot: {
      capturedAt: hoursAgo(0.25),
      values: {
        ring_light_segments_ok: 8,
        ring_light_segments_total: 12,
        illumination_lx: 2100,
        required_illumination_lx: 3500,
        alarm_code: 'AOI-LIGHT-003'
      }
    },
    reportedByUserId: 'usr-op-06',
    reportedByUserName: 'Réka Molnár',
    createdAt: hoursAgo(0.25),
    updatedAt: hoursAgo(0.25),
    slaDueAt: hoursFromNow(2),
    comments: [],
    attachments: []
  }
]

// ─── Store ────────────────────────────────────────────────────────────────────

let ticketsStore: MaintenanceTicket[] = [...initialTickets]

// ─── Settings ─────────────────────────────────────────────────────────────────

export interface TicketSettings {
  autoAssignTickets: boolean
  devTicketGenEnabled: boolean
}

let ticketSettings: TicketSettings = {
  autoAssignTickets: false,
  devTicketGenEnabled: true
}

export function getTicketSettings(): TicketSettings {
  return ticketSettings
}

export function updateTicketSettings(updates: Partial<TicketSettings>): TicketSettings {
  ticketSettings = { ...ticketSettings, ...updates }
  return ticketSettings
}

// ─── Store Functions ──────────────────────────────────────────────────────────

export function getTicketsStore(): MaintenanceTicket[] {
  return ticketsStore
}

export function addTicketToStore(ticket: MaintenanceTicket): MaintenanceTicket {
  ticketsStore.unshift(ticket)
  return ticket
}

export function findTicketById(id: string): MaintenanceTicket | undefined {
  return ticketsStore.find(t => t.id === id || t.ticketNumber === id)
}

export function updateTicketInStore(
  id: string,
  updates: Partial<MaintenanceTicket>
): MaintenanceTicket | undefined {
  const ticket = findTicketById(id)
  if (!ticket) return undefined

  const oldStatus = ticket.status
  Object.assign(ticket, updates, { updatedAt: new Date().toISOString() })

  if (updates.status === 'Resolved' && !ticket.resolvedAt) {
    ticket.resolvedAt = new Date().toISOString()
  }

  // Auto-append system comment on status change
  if (updates.status && updates.status !== oldStatus) {
    const sysComment: TicketComment = {
      id: randomUUID(),
      ticketId: ticket.id,
      authorName: 'System',
      content: '',
      transition: {
        fromStatus: oldStatus,
        toStatus: updates.status,
        actor: 'System'
      },
      createdAt: new Date().toISOString(),
      attachments: []
    }
    ticket.comments.push(sysComment)
  }

  return ticket
}

/** Update ticket status with an explicit actor label, auto-appending a system comment. */
export function updateTicketStatus(
  id: string,
  newStatus: TicketStatus,
  actor: string = 'System'
): MaintenanceTicket | undefined {
  const ticket = findTicketById(id)
  if (!ticket) return undefined

  const oldStatus = ticket.status
  if (oldStatus === newStatus) return ticket

  ticket.status = newStatus
  ticket.updatedAt = new Date().toISOString()

  if (newStatus === 'Resolved' && !ticket.resolvedAt) {
    ticket.resolvedAt = new Date().toISOString()
  }

  const sysComment: TicketComment = {
    id: crypto.randomUUID(),
    ticketId: ticket.id,
    authorName: 'System',
    content: '',
    transition: { fromStatus: oldStatus, toStatus: newStatus, actor },
    createdAt: new Date().toISOString(),
    attachments: []
  }
  ticket.comments.push(sysComment)

  return ticket
}

export function addCommentToTicket(
  ticketId: string,
  comment: TicketComment
): TicketComment | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined
  ticket.comments.push(comment)
  ticket.updatedAt = new Date().toISOString()
  return comment
}

export function addAttachmentToTicket(
  ticketId: string,
  attachment: Omit<TicketAttachment, 'id' | 'ticketId' | 'uploadedAt'> & { id?: string },
  commentId?: string
): TicketAttachment | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  const fullAttachment: TicketAttachment = {
    id: attachment.id || (typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `att-${Date.now()}`),
    ticketId,
    commentId,
    fileName: attachment.fileName,
    contentType: attachment.contentType,
    fileSize: attachment.fileSize,
    uploadedAt: new Date().toISOString(),
    url: attachment.url || ''
  }

  ticket.attachments.push(fullAttachment)

  if (commentId) {
    const comment = ticket.comments.find(c => c.id === commentId)
    if (comment) {
      if (!comment.attachments) comment.attachments = []
      comment.attachments.push(fullAttachment)
    }
  }

  ticket.updatedAt = new Date().toISOString()
  return fullAttachment
}
