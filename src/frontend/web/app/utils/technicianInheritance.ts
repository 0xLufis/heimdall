import type { TechnicianRule, ShiftAbsenceRecord, MachineGroup } from '~/types/maintenance'

export interface ResolvedTechnician {
  technicianId: string
  technicianName: string
  technicianEmail?: string
  source: 'technology_rule' | 'machine_override' | 'complete_machine_dedication' | 'group_rule' | 'default'
  sourceLabel: string
  isAbsent?: boolean
  absenceReason?: string
  backupTechnicianName?: string
}

export interface PreferredTechnicianOptions {
  prioritizeTechnology?: boolean
  technology?: string
}

export function resolvePreferredTechnician(
  machineId: string | undefined,
  machineType: string | undefined,
  groupId: string | undefined,
  rules: TechnicianRule[],
  absences: ShiftAbsenceRecord[],
  options?: PreferredTechnicianOptions | boolean
): ResolvedTechnician | null {
  const prioritizeTech = typeof options === 'boolean' ? options : !!options?.prioritizeTechnology
  const effectiveTech = (typeof options === 'object' && options?.technology) ? options.technology : machineType

  let resolved: TechnicianRule | undefined
  let source: ResolvedTechnician['source'] = 'default'
  let sourceLabel = 'No preferred technician'

  if (prioritizeTech) {
    // 1. Technology dedication primarily
    if (effectiveTech) {
      resolved = rules.find(r => r.scopeType === 'technology' && r.targetId.toLowerCase() === effectiveTech.toLowerCase())
      if (resolved) {
        source = 'technology_rule'
        sourceLabel = `Technology dedication (${effectiveTech})`
      }
    }

    // 2. Complete machine dedication / override
    if (!resolved && machineId) {
      resolved = rules.find(r => r.scopeType === 'machine' && r.targetId === machineId)
      if (resolved) {
        source = 'machine_override'
        sourceLabel = `Complete machine dedication`
      }
    }

    // 3. Group / Line rule
    if (!resolved && groupId) {
      resolved = rules.find(r => r.scopeType === 'group' && r.targetId === groupId)
      if (resolved) {
        source = 'group_rule'
        sourceLabel = `Line / Group rule`
      }
    }
  } else {
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
    if (!resolved && effectiveTech) {
      resolved = rules.find(r => r.scopeType === 'technology' && r.targetId.toLowerCase() === effectiveTech.toLowerCase())
      if (resolved) {
        source = 'technology_rule'
        sourceLabel = `Technology rule (${effectiveTech})`
      }
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

/**
 * Resolves preferred technician prioritizing technology dedication primarily,
 * while still supporting complete machine dedications and group hierarchy fallback.
 */
export function resolveTechnologyPrimaryTechnician(
  machineId: string | undefined,
  machineType: string | undefined,
  groupId: string | undefined,
  rules: TechnicianRule[],
  absences: ShiftAbsenceRecord[],
  technology?: string
): ResolvedTechnician | null {
  return resolvePreferredTechnician(machineId, machineType, groupId, rules, absences, {
    prioritizeTechnology: true,
    technology
  })
}
