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
  | 'Waiting_On_Feedback'
  | 'Pending_Validation'
  | 'Archived'
  | 'Cancelled'

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'

export interface StateTransitionMeta {
  fromStatus: TicketStatus
  toStatus: TicketStatus
  reason?: string
  actor?: string
}

export interface TicketComment {
  id: string
  ticketId: string
  authorUserId?: string
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
  url: string
}

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

export interface CameraInspectionData {
  capturedAt: string
  imageUrl: string
  defectType?: string
  confidence?: number
}

export interface AokSignOff {
  required: boolean
  granted: boolean
  grantedBy?: string
  grantedAt?: string
  comments?: string
}

export interface ErrorTemplate {
  id: string
  category: 'Prevention' | 'Error' | 'Improvement' | 'ETC'
  errorGroup: string
  errorCode: string
  shortDescription: string
  detailedDescription: string
  targetKanbanState: TicketStatus
  externalEscalationTarget?: string
  defaultTags: string[]
  sampleFbState?: FunctionBlockState
  sampleTelemetryKeys?: string[]
  affectedMachineTypes?: string[]
}

export interface MachineGroup {
  id: string
  name: string
  description?: string
  parentId?: string | null
  machineIds: string[]
  machineTypes?: string[]
  color?: string
  icon?: string
  leadEngineerId?: string
  leadEngineerName?: string
}

export interface TechnicianRule {
  id: string
  name: string
  technicianId: string
  technicianName: string
  technicianEmail?: string
  scopeType: 'technology' | 'group' | 'machine'
  targetId: string
  categoryFilter?: string
  backupTechnicianId?: string
  backupTechnicianName?: string
  assignedByRole?: 'shift_leader' | 'group_leader' | 'manager'
}

export interface MachineGroupClusterRule {
  groupId: string
  groupName: string
  machineTypes: string[]
  leadEngineerId?: string
  leadEngineerName?: string
}

export interface ShiftAbsenceRecord {
  id: string
  technicianId: string
  technicianName: string
  reason: 'Sick' | 'Emergency' | 'Vacation' | 'Training' | 'Unplanned'
  startDate: string
  endDate: string
  markedBy: string
  backupTechnicianId?: string
  backupTechnicianName?: string
  active: boolean
}

export interface TeamsPresenceInfo {
  userId: string
  displayName: string
  email: string
  availability: 'Available' | 'Busy' | 'OutOfOffice' | 'Away'
  isOutOfOffice: boolean
  oooMessage?: string
  returnDate?: string
}

export interface MaintenanceTicket {
  id: string
  ticketNumber: string
  stationId?: string
  stationName?: string
  machineType?: string
  groupId?: string
  controllerId?: string
  controllerName?: string
  title: string
  description?: string
  status: TicketStatus
  priority: TicketPriority
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC'
  errorGroup?: string
  errorCode?: string
  tags?: string[]
  fbState?: FunctionBlockState
  sfc?: string
  cameraInspection?: CameraInspectionData
  telemetrySnapshot?: TelemetrySnapshot
  aokSignOff?: AokSignOff
  externalEscalationTarget?: string
  closeReason?: string
  durationMinutes?: number
  reportedByUserId?: string
  reportedByUserName?: string
  assignedTechnicianId?: string
  assignedTechnicianName?: string
  createdAt: string
  updatedAt: string
  slaDueAt?: string
  resolvedAt?: string
  comments: TicketComment[]
  attachments: TicketAttachment[]
  metadata?: Record<string, any>
  isOfflinePending?: boolean
}

export interface CreateTicketInput {
  title: string
  description?: string
  stationId?: string
  stationName?: string
  machineType?: string
  groupId?: string
  controllerId?: string
  controllerName?: string
  priority: TicketPriority
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC'
  errorGroup?: string
  errorCode?: string
  tags?: string[]
  reportedByUserName?: string
  assignedTechnicianName?: string
  metadata?: Record<string, any>
}

export interface TicketFilter {
  status?: TicketStatus | 'all'
  priority?: TicketPriority | 'all'
  query?: string
  stationId?: string
  machineType?: string
  groupId?: string
  tag?: string
  category?: string
  controllerId?: string
  assignedTo?: string
  sortBy?: string
  sortOrder?: 'asc' | 'desc'
  page?: number
  pageSize?: number
}

export interface TicketMetrics {
  totalTickets: number
  openCount: number
  criticalCount: number
  pendingPartsCount: number
  escalatedCount: number
  escalatedExternalCount: number
  closurePendingCount: number
  resolvedCount: number
  closedUnresolvedCount: number
  overdueCount: number
  slaCompliancePercent: number
  // legacy aliases
  openTickets?: number
  criticalTickets?: number
}

export interface MaintenanceEvent {
  type: 'TicketCreated' | 'TicketUpdated' | 'StatusChanged' | 'NewComment' | 'CriticalAlert'
  ticketId?: string
  ticket?: MaintenanceTicket
  status?: TicketStatus
  comment?: TicketComment
  stationName?: string
  message?: string
  timestamp: string
}

export interface IMaintenanceService {
  getTickets(filter?: TicketFilter): Promise<MaintenanceTicket[]>
  getTicketById(id: string): Promise<MaintenanceTicket | null>
  createTicket(ticket: CreateTicketInput): Promise<MaintenanceTicket>
  updateTicket(id: string, updates: Partial<MaintenanceTicket>): Promise<MaintenanceTicket>
  updateTicketStatus(id: string, status: TicketStatus, technicianName?: string): Promise<MaintenanceTicket>
  addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment>
  uploadAttachment?(ticketId: string, file: File): Promise<TicketAttachment>
  getMetrics(): Promise<TicketMetrics>
  subscribeToEvents(listener: (event: MaintenanceEvent) => void): () => void
}
