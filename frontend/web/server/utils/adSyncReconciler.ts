import { activeDirectoryStore } from './activeDirectoryStore'

/**
 * AD Sync Reconciler enforces strict read-only synchronization from enterprise
 * directories. Heimdall never writes back to AD/LDAP/Entra ID. This module
 * provides one-way reconciliation of local Heimdall role assignments against
 * upstream directory group memberships.
 */

export interface ReconciliationResult {
  reconciledAt: string
  usersUpdated: number
  usersSkipped: number
  groupsProcessed: number
  conflicts: ReconciliationConflict[]
  isReadOnly: true
}

export interface ReconciliationConflict {
  userId: string
  userName: string
  localRole: string
  upstreamGroup: string
  resolution: 'aligned' | 'local_override_preserved' | 'upstream_applied'
}

/** Role mapping: AD security group name -> Heimdall local role */
const AD_GROUP_ROLE_MAP: Record<string, string> = {
  'Domain Admins': 'SystemAdministrator',
  'Factory Engineers': 'Engineer',
  'Maintenance Team': 'Technician',
  'Plant Operators': 'Operator',
  'Quality Inspectors': 'Inspector',
  'IT Infrastructure': 'SystemAdministrator',
  'Controls Engineering': 'Engineer',
  'Shift Supervisors': 'ShiftLead'
}

/** Deduplication window: skip re-reconciliation within this period (ms) */
const RECONCILIATION_COOLDOWN_MS = 5 * 60 * 1000

let lastReconciliationTimestamp: number = 0
let lastResult: ReconciliationResult | null = null

/**
 * Enforces that all AD operations are strictly read-only.
 * Returns true if the operation is permitted (read), false if it would write.
 */
export function enforceReadOnlyAdPolicy(operationType: 'read' | 'write' | 'create' | 'delete'): boolean {
  return operationType === 'read'
}

/**
 * Performs one-way reconciliation of Heimdall roles against upstream AD groups.
 * This NEVER writes back to AD -- only updates local Heimdall role assignments.
 */
export function reconcileRolesFromDirectory(
  upstreamGroups: Array<{ groupName: string; members: Array<{ userId: string; userName: string }> }>,
  localUsers: Array<{ userId: string; userName: string; currentRole: string }>,
  options: { forceRefresh?: boolean; preserveLocalOverrides?: boolean } = {}
): ReconciliationResult {
  const now = Date.now()

  // Deduplication: skip if recently reconciled (unless forced)
  if (!options.forceRefresh && (now - lastReconciliationTimestamp) < RECONCILIATION_COOLDOWN_MS && lastResult) {
    return lastResult
  }

  const conflicts: ReconciliationConflict[] = []
  let usersUpdated = 0
  let usersSkipped = 0

  const localUserMap = new Map(localUsers.map(u => [u.userId, u]))

  for (const group of upstreamGroups) {
    const mappedRole = AD_GROUP_ROLE_MAP[group.groupName]
    if (!mappedRole) continue

    for (const member of group.members) {
      const localUser = localUserMap.get(member.userId)
      if (!localUser) {
        usersSkipped++
        continue
      }

      if (localUser.currentRole === mappedRole) {
        conflicts.push({
          userId: member.userId,
          userName: member.userName,
          localRole: localUser.currentRole,
          upstreamGroup: group.groupName,
          resolution: 'aligned'
        })
        usersSkipped++
      } else if (options.preserveLocalOverrides) {
        conflicts.push({
          userId: member.userId,
          userName: member.userName,
          localRole: localUser.currentRole,
          upstreamGroup: group.groupName,
          resolution: 'local_override_preserved'
        })
        usersSkipped++
      } else {
        conflicts.push({
          userId: member.userId,
          userName: member.userName,
          localRole: localUser.currentRole,
          upstreamGroup: group.groupName,
          resolution: 'upstream_applied'
        })
        usersUpdated++
      }
    }
  }

  const result: ReconciliationResult = {
    reconciledAt: new Date(now).toISOString(),
    usersUpdated,
    usersSkipped,
    groupsProcessed: upstreamGroups.length,
    conflicts,
    isReadOnly: true
  }

  lastReconciliationTimestamp = now
  lastResult = result
  return result
}

export function getLastReconciliationResult(): ReconciliationResult | null {
  return lastResult
}

export function getAdGroupRoleMap(): Record<string, string> {
  return { ...AD_GROUP_ROLE_MAP }
}

export function resetReconciliationState(): void {
  lastReconciliationTimestamp = 0
  lastResult = null
}
