/**
 * Canonical ticket status — exactly 5 values, matching the backend enum.
 */
export type TicketStatus = 'Open' | 'InProgress' | 'Pending' | 'Resolved' | 'Closed'

export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical'

/**
 * Describes why a ticket is in the Pending state.
 * Maps to the backend PendingReason enum.
 */
export type PendingReason = 'None' | 'Parts' | 'ExternalOk' | 'Action' | 'SignOff' | 'External' | 'Custom'

export type EscalationHandoverState = 'None' | 'Notification' | 'HandOff' | 'ParallelWork'

export type TicketOriginatorType = 'MachineAutomatic' | 'ScheduledMaintenance' | 'ManualUser'

export type TicketIssueType = 'Transient' | 'Maintenance' | 'Improvement' | 'Other'

export type ResponsibleDepartment =
  | 'Assy'
  | 'SMT'
  | 'Test'
  | 'IT'
  | 'MES'
  | 'SAP'
  | 'ProcessEngineering'
  | 'ProductOwner'
  | 'Mechanical'
  | 'Robotics'
  | 'Vision'
  | string

export interface TicketChangeRecord {
  id: string
  timestamp: string
  changedBy: string
  field: string
  oldValue?: any
  newValue?: any
  note?: string
}

export interface StoppageDepartmentSummary {
  department: string
  stoppageMinutes: number
  incidentCount: number
}

export interface StoppageStats {
  totalStoppageMinutesThisWeek: number
  totalLineStopIncidents: number
  departmentBreakdown: StoppageDepartmentSummary[]
  topWorstMachines: Array<{ machineName: string; totalStoppageMinutes: number; incidentCount: number }>
}

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

export interface ErrorTemplate {
  id: string
  category: 'Prevention' | 'Error' | 'Improvement' | 'ETC'
  errorGroup: string
  errorCode: string
  shortDescription: string
  detailedDescription: string
  targetKanbanState: TicketStatus
  defaultTags: string[]
  sampleFbState?: FunctionBlockState
  sampleTelemetryKeys?: string[]
  affectedMachineTypes?: string[]
}

export type MachineGroupType =
  | 'BusinessUnit'
  | 'Project'
  | 'Line'
  | 'SubLine'
  | 'Machine'
  | 'Technological'
  | 'Customer'
  | 'Manual'

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
  groupType?: MachineGroupType
  interfaceContract?: string
  technologyKey?: string
}

/**
 * Wire values of App.Contracts.Enums.DelegationDiscipline. The backend has no global
 * JsonStringEnumConverter, so enums are serialized as integers.
 */
export const DelegationDisciplineValue = {
  Mechanical: 1,
  Controls: 2,
  Electrical: 3,
  Safety: 4,
  Quality: 5,
  Process: 6,
  Tooling: 7,
  General: 99
} as const

/** Wire values of the App.Contracts.Enums.WorkerScheduleType [Flags] enum (bitwise OR of these). */
export const WorkerScheduleFlag = {
  Shift: 1,
  OfficeTime: 2,
  OnCall: 4
} as const

export interface DelegationRecipe {
  id: string
  name: string
  description?: string
  /** Integer DelegationDiscipline value (see DelegationDisciplineValue). */
  discipline: number
  triggerEventCategory?: string
  /** Bitmask of WorkerScheduleFlag values (e.g. 7 = Shift | OfficeTime | OnCall). */
  allowedSchedules: number
  requiredSkillIds: string[]
  requiredSkillCodes: string[]
  primaryPersonnelGroupId?: string
  primaryPersonnelGroupName?: string
  primaryUserId?: string
  primaryUserName?: string
  onCallFallbackUserId?: string
  onCallFallbackUserName?: string
  escalationGroupId?: string
  escalationGroupName?: string
  escalationTimeoutMinutes: number
  isActive: boolean
  technology?: string
  isTechnologyPrimary?: boolean
  isCompleteMachineDedication?: boolean
  maintenanceGroupIds?: string[]
  maintenanceGroupNames?: string[]
  machineGroupIds?: string[]
  machineGroupNames?: string[]
  createdAt?: string
  updatedAt?: string
}

export interface DedicatedPerson {
  userId: string
  userName: string
  email?: string
  source: string
  technology?: string
  isGroupMemberOverride?: boolean
  overriddenGroupName?: string
}

export interface DedicatedGroup {
  groupId: string
  groupName: string
  discipline: string
  technology?: string
  memberCount: number
  memberNames: string[]
}

export interface MachineDirectDedication {
  id: string
  targetType: 'person' | 'group'
  targetId: string
  targetName: string
  targetEmail?: string
  technology: string
  isTechnologySpecific: boolean
  isGroupMemberOverride: boolean
  overriddenGroupName?: string
  assignedAt: string
}

export interface InheritedDedication {
  machineGroupId: string
  machineGroupName: string
  groupType: string
  dedicatedPerson?: DedicatedPerson
  dedicatedGroup?: DedicatedGroup
}

export interface TechnologicalGroupDedication {
  groupId: string
  groupName: string
  technology: string
  isAutomaticallyDedicated: boolean
  isSuperseded: boolean
  supersededByUserId?: string
  supersededByUserName?: string
  memberUserIds: string[]
  memberNames: string[]
}

export interface MachineDedications {
  machineId: string
  machineName: string
  customIdentifier: string
  technologies: string[]
  directDedications?: MachineDirectDedication[]
  dedicatedPerson?: DedicatedPerson | null
  dedicatedGroup?: DedicatedGroup | null
  inheritedDedications: InheritedDedication[]
  technologicalGroupDedications: TechnologicalGroupDedication[]
}

export interface DedicatedMachineItem {
  machineId: string
  machineName: string
  customIdentifier: string
  machineType?: string
  groupName?: string
  technology?: string
  supersedesTechnologicalGroup?: boolean
  supersededGroupName?: string
}

export interface DedicatedMachineGroupItem {
  groupId: string
  groupName: string
  groupType: string
  machineCount: number
}

export interface UserTechnologicalCoverage {
  groupId: string
  groupName: string
  technology: string
  coveredMachines: DedicatedMachineItem[]
}

export interface UserDedications {
  userId: string
  userName: string
  directlyDedicatedMachines: DedicatedMachineItem[]
  dedicatedMachineGroups: DedicatedMachineGroupItem[]
  technologicalCoverage: UserTechnologicalCoverage[]
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

export interface EscalationInfo {
  isEscalated: boolean
  escalationReason?: string | null
  escalatedAt?: string | null
  escalatedBy?: string | null
  escalationClosedAt?: string | null
  escalationClosedBy?: string | null
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
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC' | string
  issueType?: TicketIssueType
  originatorType?: TicketOriginatorType
  isLineStop?: boolean
  lineStopDurationMinutes?: number
  responsibleDepartment?: ResponsibleDepartment
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
  cameraInspection?: CameraInspectionData
  telemetrySnapshot?: TelemetrySnapshot | Record<string, any>
  // Pending details
  pendingReason?: PendingReason | null
  pendingDetails?: string | null
  // Escalation (orthogonal to status)
  isEscalated?: boolean
  escalationReason?: string | null
  escalationTarget?: 'DedicatedEngineer' | 'Management' | string | null
  escalationHandoverState?: EscalationHandoverState | null
  escalatedAt?: string | null
  escalatedBy?: string | null
  escalationClosedAt?: string | null
  escalationClosedBy?: string | null
  // Assignment & tracking
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
  changeHistory?: TicketChangeRecord[]
  metadata?: Record<string, any>
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
  category?: 'Prevention' | 'Error' | 'Improvement' | 'ETC' | string
  issueType?: TicketIssueType
  originatorType?: TicketOriginatorType
  isLineStop?: boolean
  responsibleDepartment?: ResponsibleDepartment
  externalOperatorId?: string
  externalOperatorName?: string
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
  filteredCount?: number
  openCount: number
  inProgressCount: number
  pendingCount: number
  resolvedCount: number
  closedCount: number
  escalatedCount: number
  criticalCount: number
  overdueCount: number
  slaCompliancePercent: number
}

export interface MaintenanceEvent {
  type:
    | 'TicketCreated'
    | 'TicketUpdated'
    | 'TicketDeleted'
    | 'StatusChanged'
    | 'NewComment'
    | 'CriticalAlert'
    | 'ReceiveNotification'
    | 'InventoryUpdated'
    | 'TelemetryReceived'
    | 'StationTelemetryReceived'
    | 'StationOscillationReceived'
  ticketId?: string
  ticket?: MaintenanceTicket
  status?: TicketStatus
  comment?: TicketComment
  stationName?: string
  message?: string
  hostname?: string
  mac?: string
  source?: string
  summary?: any
  timestamp: string
}

export interface IMaintenanceService {
  getTickets(filter?: TicketFilter): Promise<MaintenanceTicket[]>
  getTicketById(id: string): Promise<MaintenanceTicket | null>
  createTicket(ticket: CreateTicketInput): Promise<MaintenanceTicket>
  updateTicket(id: string, updates: Partial<MaintenanceTicket>): Promise<MaintenanceTicket>
  updateTicketStatus(id: string, status: TicketStatus, technicianName?: string, extra?: Record<string, any>): Promise<MaintenanceTicket>
  setPending(id: string, reason: PendingReason, details?: string): Promise<MaintenanceTicket>
  escalate(id: string, reason: string, escalatedBy: string, handoverState?: EscalationHandoverState, target?: string): Promise<MaintenanceTicket>
  resolveEscalation(id: string, resolvedBy: string): Promise<MaintenanceTicket>
  reserveTicket(ticketId: string, technicianName: string): Promise<MaintenanceTicket>
  qrPickup(ticketId: string, technicianName?: string): Promise<MaintenanceTicket>
  editWithHistory(ticketId: string, updates: Partial<MaintenanceTicket>, editorName: string, reason?: string): Promise<MaintenanceTicket>
  getStoppageStats?(): Promise<StoppageStats>
  addComment(ticketId: string, authorName: string, content: string): Promise<TicketComment>
  uploadAttachment?(ticketId: string, file: File): Promise<TicketAttachment>
  getMetrics(): Promise<TicketMetrics>
  subscribeToEvents(listener: (event: MaintenanceEvent) => void): () => void
}
