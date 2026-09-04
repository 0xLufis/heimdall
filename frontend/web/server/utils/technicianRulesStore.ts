import { randomUUID } from 'node:crypto'

export type ScopeType = 'technology' | 'group' | 'machine'
export type UserRoleType = 'technician' | 'engineer' | 'shift_leader' | 'group_leader' | 'manager' | 'admin'

export interface CandidateUser {
  id: string
  name: string
  email: string
  role: UserRoleType
  department: string
  specialization?: string
  isOutOfOffice?: boolean
  assignedRulesCount: number
}

export const SEEDED_CANDIDATES: CandidateUser[] = [
  {
    id: 'usr-sally',
    name: 'Engineer Sally',
    email: 'sally.milling@heimdall.dev',
    role: 'engineer',
    department: 'Milling & Machining',
    specialization: 'Milling Technology & CNC Tooling',
    isOutOfOffice: false,
    assignedRulesCount: 1
  },
  {
    id: 'usr-orwell',
    name: 'Engineer Orwell',
    email: 'orwell.audi@heimdall.dev',
    role: 'group_leader',
    department: 'Battery Assembly (AUDI)',
    specialization: 'Mechanical Engineering & Robotics',
    isOutOfOffice: true,
    assignedRulesCount: 1
  },
  {
    id: 'usr-katalin',
    name: 'Katalin Nagy',
    email: 'katalin.aoi@heimdall.dev',
    role: 'group_leader',
    department: 'Quality & Vision AI',
    specialization: 'Automatic Optical Inspection & Cognex',
    isOutOfOffice: false,
    assignedRulesCount: 1
  },
  {
    id: 'usr-ferenc',
    name: 'Shift Leader Ferenc',
    email: 'ferenc.leader@heimdall.dev',
    role: 'shift_leader',
    department: 'Production Shift A',
    specialization: 'Shift Operations & Absence Management',
    isOutOfOffice: false,
    assignedRulesCount: 0
  },
  {
    id: 'usr-kovacs',
    name: 'István Kovács',
    email: 'istvan.kovacs@heimdall.dev',
    role: 'technician',
    department: 'Line 06 Mechanical',
    specialization: 'Bearing Replacement & Spindles',
    isOutOfOffice: false,
    assignedRulesCount: 0
  },
  {
    id: 'usr-varga',
    name: 'Gábor Varga',
    email: 'gabor.varga@heimdall.dev',
    role: 'technician',
    department: 'General Maintenance',
    specialization: 'Hydraulics & Dispensing Systems',
    isOutOfOffice: false,
    assignedRulesCount: 1
  },
  {
    id: 'usr-nemeth',
    name: 'Zoltán Németh',
    email: 'zoltan.nemeth@heimdall.dev',
    role: 'technician',
    department: 'Robotics & Fastening',
    specialization: 'KUKA Servo Drives & Fasteners',
    isOutOfOffice: false,
    assignedRulesCount: 0
  },
  {
    id: 'usr-horvath',
    name: 'Bence Horváth',
    email: 'bence.horvath@heimdall.dev',
    role: 'technician',
    department: 'Pressing & Fitting',
    specialization: 'Hydraulic Seals & Force Sensors',
    isOutOfOffice: false,
    assignedRulesCount: 0
  },
  {
    id: 'usr-pap',
    name: 'Orsolya Pap',
    email: 'orsolya.pap@heimdall.dev',
    role: 'engineer',
    department: 'Testing & Diagnostics',
    specialization: 'EOL Testing & Firmware Diagnostics',
    isOutOfOffice: false,
    assignedRulesCount: 0
  },
  {
    id: 'usr-manager-andras',
    name: 'András Molnár (Plant Manager)',
    email: 'andras.manager@heimdall.dev',
    role: 'manager',
    department: 'Plant Operations & Governance',
    specialization: 'Plant-wide Cluster Allocation',
    isOutOfOffice: false,
    assignedRulesCount: 0
  }
]

export interface TechnicianRuleEntry {
  id: string
  name?: string
  technicianId?: string
  technicianName: string
  technicianEmail?: string
  scopeType: 'technology' | 'group' | 'machine'
  targetId: string // e.g. 'Milling', 'grp-line06', 'STATION-OP10-01'
  categoryFilter?: string // e.g. 'Mechanical', 'Controls'
  backupTechnicianId?: string
  backupTechnicianName?: string
  assignedByRole?: string
  assignedByUserId?: string
  assignedByUserName?: string
}

export interface ShiftAbsenceEntry {
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

export interface TeamsOooEntry {
  id: string
  userId: string
  displayName: string
  email: string
  availability: 'Available' | 'Busy' | 'OutOfOffice' | 'Away'
  isOutOfOffice: boolean
  oooMessage?: string
  returnDate?: string
  simulated?: boolean
}

let rules: TechnicianRuleEntry[] = [
  {
    id: 'rule-sally-milling',
    name: 'Milling Technology Specialist',
    technicianId: 'usr-sally',
    technicianName: 'Engineer Sally',
    technicianEmail: 'sally.milling@heimdall.dev',
    scopeType: 'technology',
    targetId: 'Milling',
    backupTechnicianId: 'usr-varga',
    backupTechnicianName: 'Gábor Varga',
    assignedByRole: 'group_leader',
    assignedByUserName: 'Engineer Orwell'
  },
  {
    id: 'rule-orwell-audi',
    name: 'AUDI Line 06 Mechanical Lead',
    technicianId: 'usr-orwell',
    technicianName: 'Engineer Orwell',
    technicianEmail: 'orwell.audi@heimdall.dev',
    scopeType: 'group',
    targetId: 'grp-line06',
    categoryFilter: 'Mechanical',
    backupTechnicianId: 'usr-nemeth',
    backupTechnicianName: 'Zoltán Németh',
    assignedByRole: 'group_leader',
    assignedByUserName: 'Engineer Orwell'
  },
  {
    id: 'rule-katalin-aoi',
    name: 'Vision & AOI Technology Lead',
    technicianId: 'usr-katalin',
    technicianName: 'Katalin Nagy',
    technicianEmail: 'katalin.aoi@heimdall.dev',
    scopeType: 'technology',
    targetId: 'Automatic Optical Inspection',
    backupTechnicianId: 'usr-varga',
    backupTechnicianName: 'Gábor Varga',
    assignedByRole: 'manager',
    assignedByUserName: 'András Molnár (Plant Manager)'
  }
]

let absences: ShiftAbsenceEntry[] = [
  {
    id: 'abs-001',
    technicianId: 'usr-kovacs',
    technicianName: 'István Kovács',
    reason: 'Vacation',
    startDate: new Date(Date.now() - 24 * 3600 * 1000).toISOString(),
    endDate: new Date(Date.now() + 48 * 3600 * 1000).toISOString(),
    markedBy: 'Shift Leader Ferenc',
    backupTechnicianId: 'usr-varga',
    backupTechnicianName: 'Gábor Varga',
    active: true
  }
]

let teamsOoo: TeamsOooEntry[] = [
  {
    id: 'usr-sally',
    userId: 'usr-sally',
    displayName: 'Engineer Sally',
    email: 'sally.milling@heimdall.dev',
    availability: 'Available',
    isOutOfOffice: false
  },
  {
    id: 'usr-orwell',
    userId: 'usr-orwell',
    displayName: 'Engineer Orwell',
    email: 'orwell.audi@heimdall.dev',
    availability: 'OutOfOffice',
    isOutOfOffice: true,
    oooMessage: 'Annual Leave — returning Monday 08:00',
    returnDate: '2026-09-08T08:00:00Z',
    simulated: true
  },
  {
    id: 'usr-varga',
    userId: 'usr-varga',
    displayName: 'Gábor Varga',
    email: 'gabor.varga@heimdall.dev',
    availability: 'Available',
    isOutOfOffice: false
  },
  {
    id: 'usr-nemeth',
    userId: 'usr-nemeth',
    displayName: 'Zoltán Németh',
    email: 'zoltan.nemeth@heimdall.dev',
    availability: 'Available',
    isOutOfOffice: false
  },
  {
    id: 'usr-katalin',
    userId: 'usr-katalin',
    displayName: 'Katalin Nagy',
    email: 'katalin.aoi@heimdall.dev',
    availability: 'Available',
    isOutOfOffice: false
  }
]

export function normalizeScopeType(raw: string): ScopeType {
  const lower = (raw || '').toLowerCase().trim()
  if (lower.includes('tech')) return 'technology'
  if (lower.includes('group') || lower.includes('line')) return 'group'
  return 'machine'
}

export function findCandidateByNameOrId(nameOrId: string): CandidateUser | undefined {
  if (!nameOrId) return undefined
  const query = nameOrId.toLowerCase().trim()
  return SEEDED_CANDIDATES.find(c =>
    c.id.toLowerCase() === query ||
    c.name.toLowerCase() === query ||
    c.name.toLowerCase().includes(query)
  )
}

/**
 * Validates dedication authorization according to plant governance rules:
 * - Engineers & Technicians: Can only dedicate themselves.
 * - Shift Leaders: Can dedicate their shift technicians to machines / stations & lines.
 * - Group Leaders: Can dedicate their engineers & technicians to stations, lines AND technologies.
 * - Managers & Admins: Full authority to dedicate any team to any technology, line, or machine group.
 */
export function validateDedicationGovernance(params: {
  callerRole?: string
  callerUserName?: string
  callerUserId?: string
  technicianName: string
  technicianId?: string
  scopeType: ScopeType
}): { authorized: boolean; error?: string } {
  const role = (params.callerRole || '').toLowerCase().replace(/[\s_-]+/g, '_')
  const techName = params.technicianName.trim().toLowerCase()
  const techId = (params.technicianId || '').trim().toLowerCase()
  const callerName = (params.callerUserName || '').trim().toLowerCase()
  const callerId = (params.callerUserId || '').trim().toLowerCase()

  // 1. Tier 1: Engineers & Technicians
  if (role === 'engineer' || role === 'technician') {
    const isSelfName = callerName && (techName === callerName || techName.includes(callerName) || callerName.includes(techName))
    const isSelfId = callerId && (techId === callerId)
    if (!isSelfName && !isSelfId) {
      return {
        authorized: false,
        error: 'Governance violation: Engineers and technicians can only dedicate themselves.'
      }
    }
    return { authorized: true }
  }

  // 2. Tier 2: Shift Leaders
  if (role === 'shift_leader' || role === 'shiftleader') {
    // Cannot assign technology-wide engineering scope
    if (params.scopeType === 'technology') {
      return {
        authorized: false,
        error: 'Governance violation: Shift leaders can only dedicate technicians to stations and lines, not technology-wide engineering domains.'
      }
    }

    // Check if target is a known candidate
    const targetUser = findCandidateByNameOrId(params.technicianName) || (params.technicianId ? findCandidateByNameOrId(params.technicianId) : undefined)
    if (targetUser && targetUser.role !== 'technician') {
      return {
        authorized: false,
        error: `Governance violation: Shift leaders can only dedicate shift technicians, not ${targetUser.role.replace('_', ' ')}s.`
      }
    }

    return { authorized: true }
  }

  // 3. Tier 3: Group Leaders
  if (role === 'group_leader' || role === 'groupleader' || role === 'lead_engineer' || role === 'team_lead') {
    // Can dedicate technicians & engineers to stations, lines, AND technologies
    const targetUser = findCandidateByNameOrId(params.technicianName) || (params.technicianId ? findCandidateByNameOrId(params.technicianId) : undefined)
    if (targetUser && (targetUser.role === 'manager' || targetUser.role === 'admin')) {
      return {
        authorized: false,
        error: 'Governance violation: Group leaders cannot assign plant management or administrators.'
      }
    }
    return { authorized: true }
  }

  // 4. Tier 4: Managers & Admins
  if (role === 'manager' || role === 'admin' || role === 'system_admin' || !role) {
    // Full cross-cutting authority
    return { authorized: true }
  }

  return { authorized: true }
}

export function getAllRules(): TechnicianRuleEntry[] {
  return rules
}

export function createRule(data: {
  name?: string
  technicianId?: string
  technicianName: string
  technicianEmail?: string
  scopeType: string
  targetId?: string
  target?: string
  categoryFilter?: string
  backupTechnicianId?: string
  backupTechnicianName?: string
  backupTechnician?: string
  role?: string
  assignedByRole?: string
  assignedByUserId?: string
  assignedByUserName?: string
  callerRole?: string
  callerUserName?: string
  callerUserId?: string
}): TechnicianRuleEntry {
  const normScope = normalizeScopeType(data.scopeType)
  const targetId = data.targetId || data.target || ''

  const callerRole = data.callerRole || data.assignedByRole || data.role
  const callerUserName = data.callerUserName || data.assignedByUserName
  const callerUserId = data.callerUserId || data.assignedByUserId

  // Run governance verification
  const check = validateDedicationGovernance({
    callerRole,
    callerUserName,
    callerUserId,
    technicianName: data.technicianName,
    technicianId: data.technicianId,
    scopeType: normScope
  })

  if (!check.authorized) {
    const err: any = new Error(check.error || 'Unauthorized dedication')
    err.statusCode = 403
    throw err
  }

  const matchedTech = findCandidateByNameOrId(data.technicianName)
  const matchedBackup = data.backupTechnician || data.backupTechnicianName
    ? findCandidateByNameOrId(data.backupTechnician || data.backupTechnicianName || '')
    : undefined

  const newRule: TechnicianRuleEntry = {
    id: `rule-${randomUUID().slice(0, 8)}`,
    name: data.name || `${data.technicianName} [${normScope.toUpperCase()}: ${targetId}]`,
    technicianId: data.technicianId || matchedTech?.id || `tech-${randomUUID().slice(0, 6)}`,
    technicianName: data.technicianName,
    technicianEmail: data.technicianEmail || matchedTech?.email,
    scopeType: normScope,
    targetId,
    categoryFilter: data.categoryFilter || undefined,
    backupTechnicianId: data.backupTechnicianId || matchedBackup?.id,
    backupTechnicianName: data.backupTechnicianName || data.backupTechnician,
    assignedByRole: callerRole,
    assignedByUserId: callerUserId,
    assignedByUserName: callerUserName
  }

  rules.push(newRule)
  return newRule
}

export function deleteRule(id: string): boolean {
  const idx = rules.findIndex(r => r.id === id)
  if (idx === -1) return false
  rules.splice(idx, 1)
  return true
}

export function getAllAbsences(): ShiftAbsenceEntry[] {
  return absences
}

export function getActiveAbsences(): ShiftAbsenceEntry[] {
  return absences.filter(a => a.active)
}

export function createAbsence(data: Omit<ShiftAbsenceEntry, 'id' | 'active'>): ShiftAbsenceEntry {
  const newAbsence: ShiftAbsenceEntry = {
    id: `abs-${randomUUID().slice(0, 8)}`,
    ...data,
    active: true
  }
  absences.unshift(newAbsence)
  return newAbsence
}

export function resolveAbsence(id: string): boolean {
  const abs = absences.find(a => a.id === id)
  if (!abs) return false
  abs.active = false
  return true
}

export function getTeamsOooStatuses(): TeamsOooEntry[] {
  return teamsOoo
}

export function toggleSimulatedOoo(userIdOrName: string, isOoo: boolean): TeamsOooEntry | undefined {
  const query = userIdOrName.toLowerCase().trim()
  const entry = teamsOoo.find(e =>
    e.userId.toLowerCase() === query ||
    e.id.toLowerCase() === query ||
    e.displayName.toLowerCase() === query ||
    e.displayName.toLowerCase().includes(query)
  )
  if (entry) {
    entry.isOutOfOffice = isOoo
    entry.availability = isOoo ? 'OutOfOffice' : 'Available'
    entry.simulated = true
    if (isOoo && !entry.oooMessage) {
      entry.oooMessage = 'Simulated Out of Office (Teams sync)'
    }
  }
  return entry
}

export function isPersonAvailable(technicianIdOrName: string): { available: boolean; reason?: string; backupName?: string } {
  const query = technicianIdOrName.toLowerCase().trim()

  // Check shift absence
  const absence = absences.find(a =>
    a.active && (
      a.technicianId.toLowerCase() === query ||
      a.technicianName.toLowerCase() === query ||
      a.technicianName.toLowerCase().includes(query)
    )
  )
  if (absence) {
    return {
      available: false,
      reason: `Shift absence: ${absence.reason} (Marked by ${absence.markedBy})`,
      backupName: absence.backupTechnicianName
    }
  }

  // Check Teams OOO
  const ooo = teamsOoo.find(t =>
    t.isOutOfOffice && (
      t.userId.toLowerCase() === query ||
      t.displayName.toLowerCase() === query ||
      t.displayName.toLowerCase().includes(query)
    )
  )
  if (ooo) {
    return {
      available: false,
      reason: `Teams Out of Office: ${ooo.oooMessage || 'Away'}`,
      backupName: 'On-duty shift supervisor'
    }
  }

  return { available: true }
}
