import type { TechnicianRule, ShiftAbsenceRecord, MachineGroup } from '~/types/maintenance'

export interface ResolvedTechnician {
  technicianId: string
  technicianName: string
  technicianEmail?: string
  source: 'machine_override' | 'group_rule' | 'technology_rule' | 'default'
  sourceLabel: string
  isAbsent?: boolean
  absenceReason?: string
  backupTechnicianName?: string
}

export function resolvePreferredTechnician(
  machineId: string | undefined,
  machineType: string | undefined,
  groupId: string | undefined,
  rules: TechnicianRule[],
  absences: ShiftAbsenceRecord[],
): ResolvedTechnician | null {
  let resolved: TechnicianRule | undefined
  let source: ResolvedTechnician['source'] = 'default'
  let sourceLabel = 'No preferred technician'

  // 1. Machine-level override (most specific)
  if (machineId) {
    resolved = rules.find(r => r.scopeType === 'machine' && r.targetId === machineId)
    if (resolved) {
      source = 'machine_override'
      sourceLabel = `Machine override`
    }
  }

  // 2. Group / Line rule
  if (!resolved && groupId) {
    resolved = rules.find(r => r.scopeType === 'group' && r.targetId === groupId)
    if (resolved) {
      source = 'group_rule'
      sourceLabel = `Line / Group rule`
    }
  }

  // 3. Technology / MachineType rule
  if (!resolved && machineType) {
    resolved = rules.find(r => r.scopeType === 'technology' && r.targetId === machineType)
    if (resolved) {
      source = 'technology_rule'
      sourceLabel = `Technology rule (${machineType})`
    }
  }

  if (!resolved) return null

  // Check shift absences
  const absence = absences.find(
    a => a.active && (a.technicianId === resolved!.technicianId || a.technicianName === resolved!.technicianName)
  )

  return {
    technicianId: resolved.technicianId,
    technicianName: resolved.technicianName,
    technicianEmail: resolved.technicianEmail,
    source,
    sourceLabel,
    isAbsent: !!absence,
    absenceReason: absence?.reason,
    backupTechnicianName: absence?.backupTechnicianName ?? resolved.backupTechnicianName,
  }
}
