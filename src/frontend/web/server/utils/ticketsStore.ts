import { randomUUID } from 'node:crypto'
import { featureFlags } from './featureFlags'
import { ensureBetterAuthProfile } from './authProfiles'

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

export type TicketStatus = 'Open' | 'InProgress' | 'Pending' | 'Resolved' | 'Closed'

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

export interface TicketChangeRecord {
  id: string
  timestamp: string
  changedBy: string
  field: string
  oldValue?: any
  newValue?: any
  note?: string
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
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC' | string
  issueType?: 'Transient' | 'Maintenance' | 'Improvement' | 'Other'
  originatorType?: 'MachineAutomatic' | 'ScheduledMaintenance' | 'ManualUser'
  isLineStop?: boolean
  lineStopDurationMinutes?: number
  responsibleDepartment?: string
  externalOperatorId?: string
  externalOperatorName?: string
  startedAt?: string
  qrScannedAt?: string
  reactionTimeMinutes?: number
  reservedBy?: string
  errorGroup?: string
  errorCode?: string
  tags?: string[]
  fbState?: FunctionBlockState
  sfc?: string
  telemetrySnapshot?: TelemetrySnapshot | Record<string, any>
  targetComponentId?: string
  targetComponentBmk?: string
  alarmCode?: string
  controllerSource?: string
  telemetryProbes?: any[]
  externalEscalationTarget?: string
  pendingReason?: string
  pendingReasons?: string[]
  pendingDetails?: string | null
  pendingAuthority?: string
  isEscalated?: boolean
  escalationReason?: string | null
  escalationTarget?: 'DedicatedEngineer' | 'Management' | string | null
  escalationHandoverState?: 'None' | 'Notification' | 'HandOff' | 'ParallelWork' | 'HandedOffToField' | null
  escalatedAt?: string | null
  escalatedBy?: string | null
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
  changeHistory?: TicketChangeRecord[]
  metadata?: Record<string, any>
}

// ─── Initial Seed Data ────────────────────────────────────────────────────────

const INITIAL_DEMO_TICKETS: MaintenanceTicket[] = [
  {
    id: 'tkt-001',
    ticketNumber: 'TKT-20260928-0001',
    stationId: 'STATION-OP10-01',
    stationName: 'OP10 Machining Cell',
    machineType: 'Milling',
    controllerId: 'ctrl-101',
    title: '[E-MILL-01] Spindle Vibration Harmonic Peak',
    description: 'Spindle vibration harmonic peak exceeded 4.5 mm/s RMS at 3,200 Hz frequency band. Toolholder imbalance suspected. Spindle balance check required.',
    status: 'InProgress',
    priority: 'Critical',
    category: 'Error',
    errorGroup: 'Motion & Drive',
    errorCode: 'E-MILL-01',
    tags: ['#Motion', '#Spindle', '#Vibration'],
    sfc: 'SFC-BAT-20260928-0012',
    isLineStop: true,
    lineStopDurationMinutes: 120,
    responsibleDepartment: 'Mechanical',
    originatorType: 'MachineAutomatic',
    issueType: 'Transient',
    startedAt: new Date(Date.now() - 3.5 * 3600 * 1000).toISOString(),
    qrScannedAt: new Date(Date.now() - 3.5 * 3600 * 1000).toISOString(),
    reactionTimeMinutes: 30,
    isEscalated: true,
    escalationReason: 'Spindle harmonic resonance critical risk — OEM specialist notified',
    escalationTarget: 'DedicatedEngineer',
    escalationHandoverState: 'Notification',
    escalatedAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    escalatedBy: 'Gábor Varga (Lead Tech)',
    reportedByUserId: 'usr-op-01',
    reportedByUserName: 'Péter Kovács (Operator)',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga',
    createdAt: new Date(Date.now() - 4 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 1 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 4 * 3600 * 1000).toISOString(),
    comments: [
      {
        id: 'c-001-01',
        ticketId: 'tkt-001',
        authorUserId: 'usr-op-01',
        authorName: 'Péter Kovács',
        content: 'Alarm raised on axis 2 after high speed cutting cycle.',
        createdAt: new Date(Date.now() - 3.5 * 3600 * 1000).toISOString()
      },
      {
        id: 'c-001-02',
        ticketId: 'tkt-001',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Commencing accelerometer diagnostic run on main bearing assembly.',
        createdAt: new Date(Date.now() - 2.5 * 3600 * 1000).toISOString()
      }
    ],
    attachments: []
  },
  {
    id: 'tkt-002',
    ticketNumber: 'TKT-20260928-0002',
    stationId: 'ROBOT-CELL-01',
    stationName: 'Robotic Welding Cell 01',
    machineType: 'Welding',
    controllerId: 'ctrl-201',
    title: '[E-ROB-03] KUKA Robot Axis 3 Servo Alarm E-409',
    description: 'Axis 3 motor current feedback saturation during weld seam trajectory. Drive package replacement needed.',
    status: 'Pending',
    priority: 'High',
    category: 'Error',
    errorGroup: 'Motion & Drive',
    errorCode: 'E-ROB-03',
    tags: ['#Robotics', '#KUKA', '#Servo'],
    sfc: 'SFC-BAT-20260928-0044',
    isLineStop: true,
    lineStopDurationMinutes: 240,
    responsibleDepartment: 'Robotics',
    originatorType: 'MachineAutomatic',
    issueType: 'Maintenance',
    pendingReason: 'Parts',
    pendingDetails: 'Requisition PO #88219 ordered for replacement servo amplifier. Delivery expected tomorrow morning.',
    isEscalated: false,
    reportedByUserId: 'usr-op-02',
    reportedByUserName: 'Anna Szabó',
    assignedTechnicianId: 'usr-tech-02',
    assignedTechnicianName: 'Zoltán Németh',
    createdAt: new Date(Date.now() - 8 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 3 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 8 * 3600 * 1000).toISOString(),
    comments: [
      {
        id: 'c-002-01',
        ticketId: 'tkt-002',
        authorUserId: 'usr-tech-02',
        authorName: 'Zoltán Németh',
        content: 'Servo amplifier failed insulation resistance test. Spare part requisition submitted to central stock.',
        createdAt: new Date(Date.now() - 5 * 3600 * 1000).toISOString()
      }
    ],
    attachments: []
  },
  {
    id: 'tkt-003',
    ticketNumber: 'TKT-20260928-0003',
    stationId: 'L09-OP270',
    stationName: 'Line 09 – Optical Quality Inspection',
    machineType: 'Vision',
    controllerId: 'ctrl-301',
    title: '[E-VIS-01] Cognex Camera Lens Cleaning & Calibration Required',
    description: 'Image contrast ratio dropped by 18% on surface defect station. Periodic cleaning and calibration target test needed.',
    status: 'Open',
    priority: 'Medium',
    category: 'Prevention',
    errorGroup: 'Vision & Optical',
    errorCode: 'E-VIS-01',
    tags: ['#Vision', '#Quality', '#AOI'],
    sfc: 'SFC-BAT-20260928-0089',
    isLineStop: false,
    responsibleDepartment: 'Vision',
    originatorType: 'ScheduledMaintenance',
    issueType: 'Maintenance',
    isEscalated: false,
    reportedByUserId: 'usr-op-03',
    reportedByUserName: 'László Tóth',
    createdAt: new Date(Date.now() - 1 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 1 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 16 * 3600 * 1000).toISOString(),
    comments: [],
    attachments: []
  },
  {
    id: 'tkt-004',
    ticketNumber: 'TKT-20260928-0004',
    stationId: 'ASSEMBLY-ST-02',
    stationName: 'SIMATIC S7 Conveyor Station 02',
    machineType: 'Conveyor',
    controllerId: 'ctrl-401',
    title: '[P-NET-01] Win10 IoT System Update & OPC-UA Driver Patch',
    description: 'Apply KB5029351 cumulative security roll-up and TwinCAT OPC-UA server runtime hotfix 3.4.20.',
    status: 'Resolved',
    priority: 'Low',
    category: 'Improvement',
    errorGroup: 'Network & Comms',
    errorCode: 'P-NET-01',
    tags: ['#IPC', '#Patch', '#TwinCAT'],
    isLineStop: false,
    responsibleDepartment: 'IT',
    originatorType: 'ManualUser',
    issueType: 'Improvement',
    isEscalated: false,
    reportedByUserId: 'usr-admin-01',
    reportedByUserName: 'System Administrator',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga',
    createdAt: new Date(Date.now() - 24 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    resolvedAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 24 * 3600 * 1000).toISOString(),
    comments: [
      {
        id: 'c-004-01',
        ticketId: 'tkt-004',
        authorUserId: 'usr-tech-01',
        authorName: 'Gábor Varga',
        content: 'Patch successfully applied during shift changeover. Verified live PLC tag communications.',
        createdAt: new Date(Date.now() - 2 * 3600 * 1000).toISOString()
      }
    ],
    attachments: []
  },
  {
    id: 'tkt-005',
    ticketNumber: 'TKT-20260928-0005',
    stationId: 'L05-OP80',
    stationName: 'Line 05 – Powertrain Sub-Assembly',
    machineType: 'Press',
    controllerId: 'ctrl-501',
    title: '[E-PRS-02] Hydraulic Press Force Deviation >5 kN',
    description: 'Bushing press-fit cell registered force curve overshoot past envelope safety margin.',
    status: 'Pending',
    priority: 'Critical',
    category: 'Error',
    errorGroup: 'Motion & Drive',
    errorCode: 'E-PRS-02',
    tags: ['#Hydraulics', '#Force', '#Calibration'],
    isLineStop: true,
    lineStopDurationMinutes: 360,
    responsibleDepartment: 'Assy',
    originatorType: 'MachineAutomatic',
    issueType: 'Maintenance',
    pendingReason: 'ExternalOk',
    pendingDetails: 'Awaiting OEM press calibration engineer on-site certification.',
    isEscalated: true,
    escalationReason: 'Force envelope violation exceeds structural tolerance — line halt',
    escalationTarget: 'Management',
    escalationHandoverState: 'ParallelWork',
    escalatedAt: new Date(Date.now() - 6 * 3600 * 1000).toISOString(),
    escalatedBy: 'Shift Supervisor',
    reportedByUserId: 'usr-op-04',
    reportedByUserName: 'Tamás Horváth',
    assignedTechnicianId: 'usr-tech-02',
    assignedTechnicianName: 'Zoltán Németh',
    createdAt: new Date(Date.now() - 7 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 4 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() + 2 * 3600 * 1000).toISOString(),
    comments: [],
    attachments: []
  },
  {
    id: 'tkt-006',
    ticketNumber: 'TKT-20260927-0006',
    stationId: 'L08-OP50',
    stationName: 'Line 08 – Surface Coating & Paint Shop',
    machineType: 'Coating',
    controllerId: 'ctrl-601',
    title: '[P-COAT-01] Air Filter HEPA Cartridge Periodic Replacement',
    description: 'Differential pressure across HEPA air filter bank reached 250 Pa. Routine replacement executed.',
    status: 'Closed',
    priority: 'Low',
    category: 'Prevention',
    errorGroup: 'Safety System',
    errorCode: 'P-COAT-01',
    tags: ['#PM', '#HVAC', '#Filter'],
    isLineStop: false,
    responsibleDepartment: 'ProcessEngineering',
    originatorType: 'ScheduledMaintenance',
    issueType: 'Other',
    isEscalated: false,
    reportedByUserId: 'usr-admin-01',
    reportedByUserName: 'Maintenance Planner',
    assignedTechnicianId: 'usr-tech-01',
    assignedTechnicianName: 'Gábor Varga',
    createdAt: new Date(Date.now() - 48 * 3600 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 20 * 3600 * 1000).toISOString(),
    resolvedAt: new Date(Date.now() - 22 * 3600 * 1000).toISOString(),
    slaDueAt: new Date(Date.now() - 10 * 3600 * 1000).toISOString(),
    comments: [],
    attachments: []
  }
]

// ─── Store ────────────────────────────────────────────────────────────────────

let ticketsInMemoryList: MaintenanceTicket[] = [...INITIAL_DEMO_TICKETS]


// ─── Settings ─────────────────────────────────────────────────────────────────

export interface TicketSettings {
  autoAssignTickets: boolean
  devTicketGenEnabled: boolean
}

let ticketSettings: TicketSettings = {
  autoAssignTickets: false,
  devTicketGenEnabled: featureFlags.enableDevFeatures
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
  return ticketsInMemoryList
}

export const getTickets = getTicketsStore

export function addTicketToStore(ticket: MaintenanceTicket): MaintenanceTicket {
  // Guarantee reporter and assigned technician have Better-Auth user profiles
  if (ticket.reportedByUserId) {
    ensureBetterAuthProfile(ticket.reportedByUserId, {
      name: ticket.reportedByUserName,
      role: 'operator'
    }).catch(() => {})
  }
  if (ticket.assignedTechnicianId) {
    ensureBetterAuthProfile(ticket.assignedTechnicianId, {
      name: ticket.assignedTechnicianName,
      role: 'technician'
    }).catch(() => {})
  }
  ticketsInMemoryList.unshift(ticket)
  return ticket
}

export function mapBackendTicketToStore(t: any): MaintenanceTicket {
  return {
    id: t.id || t.Id,
    ticketNumber: t.ticketNumber || t.TicketNumber || `TKT-${(t.id || t.Id || '').substring(0, 8).toUpperCase()}`,
    stationId: t.machineId || t.stationId || t.MachineId || '',
    stationName: t.machine?.name || t.machine?.customIdentifier || t.stationName || '',
    machineType: t.machineType || t.MachineType,
    groupId: t.groupId || t.GroupId,
    controllerId: t.clientPcId || t.controllerId || t.ClientPcId,
    controllerName: t.clientPc?.hostname || t.controllerName,
    title: t.title || t.Title || '',
    description: t.description || t.Description || '',
    status: t.status || t.Status || 'Open',
    priority: t.priority || t.Priority || 'Medium',
    category: t.category || t.Category,
    issueType: t.issueType || t.IssueType || 'Maintenance',
    originatorType: t.originatorType || t.OriginatorType || 'ManualUser',
    isLineStop: Boolean(t.isLineStop ?? t.IsLineStop ?? false),
    lineStopDurationMinutes: t.lineStopDurationMinutes ?? t.LineStopDurationMinutes ?? (Boolean(t.isLineStop ?? t.IsLineStop) ? 0 : null),
    responsibleDepartment: t.responsibleDepartment || t.ResponsibleDepartment || 'Assy',
    externalOperatorId: t.externalOperatorId || t.ExternalOperatorId || null,
    externalOperatorName: t.externalOperatorName || t.ExternalOperatorName || null,
    startedAt: t.startedAt || t.StartedAt || null,
    qrScannedAt: t.qrScannedAt || t.QrScannedAt || null,
    reactionTimeMinutes: t.reactionTimeMinutes ?? t.ReactionTimeMinutes ?? null,
    reservedBy: t.reservedBy || t.ReservedBy || null,
    errorGroup: t.errorGroup || t.ErrorGroup,
    errorCode: t.errorCode || t.ErrorCode,
    tags: Array.isArray(t.tags) ? t.tags : (typeof t.tags === 'string' ? t.tags.split(',').map((s: string) => s.trim()).filter(Boolean) : []),
    sfc: t.sfc || t.Sfc,
    pendingReason: t.pendingReason || t.PendingReason || 'None',
    pendingDetails: t.pendingDetails || t.PendingDetails || null,
    isEscalated: Boolean(t.isEscalated ?? t.IsEscalated ?? false),
    escalationReason: t.escalationReason || t.EscalationReason || null,
    escalationTarget: t.escalationTarget || t.EscalationTarget || null,
    escalationHandoverState: t.escalationHandoverState || t.EscalationHandoverState || null,
    escalatedAt: t.escalatedAt || t.EscalatedAt || null,
    escalatedBy: t.escalatedBy || t.EscalatedBy || null,
    telemetrySnapshot: t.telemetrySnapshot || t.TelemetrySnapshot || null,
    reportedByUserId: t.reportedByUserId || t.createdBy || t.CreatedBy || '',
    reportedByUserName: t.reportedByUserName || t.createdBy || t.CreatedBy || 'Operator',
    assignedTechnicianId: t.assignedTechnicianId || t.assignedTo || t.AssignedTo || '',
    assignedTechnicianName: t.assignedTechnicianName || t.assignedTo || t.AssignedTo || 'Unassigned',
    createdAt: t.createdAt || t.CreatedAt || new Date().toISOString(),
    updatedAt: t.updatedAt || t.UpdatedAt || new Date().toISOString(),
    slaDueAt: t.slaDueAt || t.SlaDueAt || null,
    resolvedAt: t.resolvedAt || t.ResolvedAt || null,
    comments: t.comments ?? [],
    attachments: t.attachments ?? [],
    changeHistory: t.changeHistory ?? []
  }
}

export function upsertTicketInStore(ticket: MaintenanceTicket): MaintenanceTicket {
  const idx = ticketsInMemoryList.findIndex(t => t.id === ticket.id || (ticket.ticketNumber && t.ticketNumber === ticket.ticketNumber))
  if (idx !== -1) {
    ticketsInMemoryList[idx] = { ...ticketsInMemoryList[idx], ...ticket }
    return ticketsInMemoryList[idx]
  } else {
    ticketsInMemoryList.unshift(ticket)
    return ticket
  }
}

export function upsertTicketsInStore(tickets: MaintenanceTicket[]): void {
  for (const t of tickets) {
    upsertTicketInStore(t)
  }
}

export async function fetchAndCacheBackendTicket(id: string): Promise<MaintenanceTicket | undefined> {
  const existing = findTicketById(id)
  if (existing) return existing

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const raw = await $fetch<any>(`${backendBase}/api/v1/tickets/${id}`)
    if (raw && (raw.id || raw.Id)) {
      const mapped = mapBackendTicketToStore(raw)
      return upsertTicketInStore(mapped)
    }
  } catch {
    // If 5099 failed, try 5001 fallback
    try {
      const raw = await $fetch<any>(`http://localhost:5001/api/v1/tickets/${id}`)
      if (raw && (raw.id || raw.Id)) {
        const mapped = mapBackendTicketToStore(raw)
        return upsertTicketInStore(mapped)
      }
    } catch {}
  }
  return undefined
}

export function ensureTicketInStore(id: string, initialDefaults?: Partial<MaintenanceTicket>): MaintenanceTicket {
  let existing = findTicketById(id)
  if (!existing) {
    existing = {
      id,
      ticketNumber: `TKT-${id.substring(0, 8).toUpperCase()}`,
      stationId: initialDefaults?.stationId || 'STATION-01',
      stationName: initialDefaults?.stationName || 'Assembly Station',
      title: initialDefaults?.title || `Incident ${id.substring(0, 8)}`,
      description: initialDefaults?.description || '',
      status: initialDefaults?.status || 'Open',
      priority: initialDefaults?.priority || 'Medium',
      responsibleDepartment: initialDefaults?.responsibleDepartment || 'Assy',
      originatorType: initialDefaults?.originatorType || 'ManualUser',
      issueType: initialDefaults?.issueType || 'Maintenance',
      reportedByUserId: initialDefaults?.reportedByUserId || 'usr-op-01',
      reportedByUserName: initialDefaults?.reportedByUserName || 'Operator',
      createdAt: initialDefaults?.createdAt || new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      slaDueAt: initialDefaults?.slaDueAt || new Date(Date.now() + 8 * 3600 * 1000).toISOString(),
      comments: [],
      attachments: [],
      ...initialDefaults
    }
    ticketsInMemoryList.unshift(existing)
  }
  return existing
}

export function findTicketById(id: string): MaintenanceTicket | undefined {
  return ticketsInMemoryList.find(t => t.id === id || t.ticketNumber === id)
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
  if (comment.authorUserId) {
    ensureBetterAuthProfile(comment.authorUserId, {
      name: comment.authorName,
      role: 'technician'
    }).catch(() => {})
  }
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

export function reserveTicketInStore(
  ticketId: string,
  technicianName: string
): MaintenanceTicket | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  ticket.reservedBy = technicianName
  ticket.assignedTechnicianName = technicianName
  ticket.updatedAt = new Date().toISOString()
  return ticket
}

export function qrPickupTicketInStore(
  ticketId: string,
  technicianName?: string
): MaintenanceTicket | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  const now = new Date()
  ticket.qrScannedAt = now.toISOString()
  ticket.startedAt = now.toISOString()
  ticket.status = 'InProgress'
  if (technicianName) {
    ticket.assignedTechnicianName = technicianName
    ticket.reservedBy = technicianName
  }
  const createdMs = new Date(ticket.createdAt).getTime()
  if (!isNaN(createdMs) && createdMs > 0) {
    ticket.reactionTimeMinutes = Math.max(0, Math.round(((now.getTime() - createdMs) / 60000) * 10) / 10)
  }
  ticket.updatedAt = now.toISOString()
  return ticket
}

export function editTicketWithHistoryInStore(
  ticketId: string,
  updates: Partial<MaintenanceTicket>,
  editorName: string,
  reason?: string
): MaintenanceTicket | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  if (!ticket.changeHistory) ticket.changeHistory = []

  const now = new Date().toISOString()
  const trackedFields = [
    'title', 'description', 'responsibleDepartment', 'priority', 'status',
    'isLineStop', 'lineStopDurationMinutes', 'createdAt', 'tags', 'pendingReason', 'pendingDetails'
  ] as const

  for (const field of trackedFields) {
    if (field in updates && (updates as any)[field] !== (ticket as any)[field]) {
      ticket.changeHistory.push({
        id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `chg-${Date.now()}-${Math.random()}`,
        timestamp: now,
        changedBy: editorName,
        field,
        oldValue: (ticket as any)[field],
        newValue: (updates as any)[field],
        note: reason
      })
    }
  }

  Object.assign(ticket, updates, { updatedAt: now })
  return ticket
}

export function autoCloseTicketInStore(
  ticketId: string,
  details?: { machineId?: string; controllerId?: string; clearedReason?: string; resolutionNotes?: string }
): MaintenanceTicket | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  const now = new Date().toISOString()
  const prevStatus = ticket.status
  ticket.status = 'Closed'
  ticket.resolvedAt = now
  ticket.updatedAt = now
  if (ticket.isLineStop) {
    ticket.isLineStop = false
  }

  const reason = details?.clearedReason || 'Machine alarm / PLC condition cleared automatically'
  const actor = details?.controllerId || details?.machineId || 'Machine PLC'

  if (!ticket.changeHistory) ticket.changeHistory = []
  ticket.changeHistory.push({
    id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `chg-${Date.now()}-${Math.random()}`,
    timestamp: now,
    changedBy: actor,
    field: 'status',
    oldValue: prevStatus,
    newValue: 'Closed',
    note: `Auto-closed by machine: ${reason}`
  })

  addCommentToTicket(ticketId, {
    authorUserId: 'system-plc-autoclose',
    authorName: actor,
    content: details?.resolutionNotes || `Ticket automatically closed by controller. Fault condition cleared: ${reason}`,
    transition: {
      fromStatus: prevStatus,
      toStatus: 'Closed',
      reason,
      actor
    }
  })

  return ticket
}

export function retractTicketInStore(
  ticketId: string,
  retractedBy: string,
  reason?: string
): MaintenanceTicket | undefined {
  const ticket = findTicketById(ticketId)
  if (!ticket) return undefined

  const now = new Date().toISOString()
  const prevStatus = ticket.status
  ticket.status = 'Closed'
  ticket.pendingReason = 'Retracted'
  ticket.pendingReasons = ['Retracted']
  ticket.updatedAt = now

  if (!ticket.changeHistory) ticket.changeHistory = []
  ticket.changeHistory.push({
    id: typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `chg-${Date.now()}-${Math.random()}`,
    timestamp: now,
    changedBy: retractedBy,
    field: 'status',
    oldValue: prevStatus,
    newValue: 'Closed',
    note: `Ticket retracted: ${reason || 'Retracted from workstation kiosk'}`
  })

  addCommentToTicket(ticketId, {
    authorUserId: 'terminal-retract',
    authorName: retractedBy,
    content: `Ticket retracted by ${retractedBy}. ${reason ? `Reason: ${reason}` : ''}`,
    transition: {
      fromStatus: prevStatus,
      toStatus: 'Closed',
      reason: reason || 'Retracted from workstation kiosk',
      actor: retractedBy
    }
  })

  return ticket
}

export function getStoppageStatsFromStore() {
  const allTickets = getTicketsStore()
  const oneWeekAgo = Date.now() - 7 * 24 * 3600 * 1000
  const weeklyLineStopTickets = allTickets.filter(
    t => t.isLineStop && new Date(t.createdAt).getTime() >= oneWeekAgo
  )

  const totalStoppageMinutesThisWeek = weeklyLineStopTickets.reduce(
    (acc, t) => acc + (t.lineStopDurationMinutes || 0), 0
  )

  const depMap: Record<string, { stoppageMinutes: number; incidentCount: number }> = {}
  for (const t of weeklyLineStopTickets) {
    const dep = t.responsibleDepartment || 'Unassigned'
    if (!depMap[dep]) depMap[dep] = { stoppageMinutes: 0, incidentCount: 0 }
    depMap[dep].stoppageMinutes += t.lineStopDurationMinutes || 0
    depMap[dep].incidentCount += 1
  }

  const departmentBreakdown = Object.entries(depMap)
    .map(([department, data]) => ({
      department,
      stoppageMinutes: data.stoppageMinutes,
      incidentCount: data.incidentCount
    }))
    .sort((a, b) => b.stoppageMinutes - a.stoppageMinutes)

  const machineMap: Record<string, { totalStoppageMinutes: number; incidentCount: number }> = {}
  for (const t of weeklyLineStopTickets) {
    const m = t.stationName || t.stationId || 'Unknown Machine'
    if (!machineMap[m]) machineMap[m] = { totalStoppageMinutes: 0, incidentCount: 0 }
    machineMap[m].totalStoppageMinutes += t.lineStopDurationMinutes || 0
    machineMap[m].incidentCount += 1
  }

  const topWorstMachines = Object.entries(machineMap)
    .map(([machineName, data]) => ({
      machineName,
      totalStoppageMinutes: data.totalStoppageMinutes,
      incidentCount: data.incidentCount
    }))
    .sort((a, b) => b.totalStoppageMinutes - a.totalStoppageMinutes)
    .slice(0, 5)

  return {
    totalStoppageMinutesThisWeek,
    totalLineStopIncidents: weeklyLineStopTickets.length,
    departmentBreakdown,
    topWorstMachines
  }
}

export const ticketsStore = {
  getTickets: getTicketsStore,
  getAllTickets: getTicketsStore,
  getTicketsStore,
  addTicketToStore,
  upsertTicketInStore,
  upsertTicketsInStore,
  fetchAndCacheBackendTicket,
  ensureTicketInStore,
  findTicketById,
  updateTicketInStore,
  updateTicketStatus,
  addCommentToTicket,
  addAttachmentToTicket,
  reserveTicketInStore,
  qrPickupTicketInStore,
  editTicketWithHistoryInStore,
  autoCloseTicketInStore,
  retractTicketInStore,
  getStoppageStatsFromStore
}


