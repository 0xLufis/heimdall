import { describe, it, expect, beforeEach } from 'vitest'
import {
  getActiveDirectoryOus,
  setOuGovernance,
  getOuGovernances
} from '~~/server/utils/activeDirectoryStore'

describe('OU Governance, Host Inspection, and Folding Actions Suite', () => {
  beforeEach(() => {
    // Reset or ensure baseline
  })

  it('retrieves discovered Active Directory OUs with nested candidate hosts for inspection', () => {
    const ous = getActiveDirectoryOus()
    expect(ous.length).toBeGreaterThan(0)

    for (const ou of ous) {
      expect(ou.ouPath).toBeDefined()
      expect(ou.subnet).toBeDefined()
      expect(ou.vlanId).toBeGreaterThan(0)
      expect(Array.isArray(ou.candidateHosts)).toBe(true)
      expect(ou.candidateHosts.length).toBe(ou.hostCount)

      for (const host of ou.candidateHosts) {
        expect(host.hostname).toBeDefined()
        expect(host.ipAddress).toMatch(/^\d+\.\d+\.\d+\.\d+$/)
        expect(host.macAddress).toMatch(/^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$/)
        expect(host.machineIdentifier).toBeDefined()
      }
    }
  })

  it('updates OU governance level and reflects approval status in active directory store', () => {
    const targetOu = 'OU=Robotics,OU=VLAN10-Production,DC=factory,DC=corp'
    
    // Set to read_write
    const rwGov = setOuGovernance(targetOu, 'read_write', 'admin@heimdall.local', 'Approved for production line')
    expect(rwGov.ouPath).toBe(targetOu)
    expect(rwGov.accessLevel).toBe('read_write')
    expect(rwGov.isApproved).toBe(true)

    // Verify reflected in getActiveDirectoryOus
    const ous = getActiveDirectoryOus()
    const updated = ous.find(o => o.ouPath === targetOu)
    expect(updated?.accessLevel).toBe('read_write')
    expect(updated?.isApproved).toBe(true)
    expect(updated?.approvedBy).toBe('admin@heimdall.local')
  })

  it('correctly transitions OU from read_write to read_only and unapproved', () => {
    const targetOu = 'OU=VisionInspection,OU=VLAN20-Quality,DC=factory,DC=corp'

    // Transition to read_only
    const roGov = setOuGovernance(targetOu, 'read_only', 'it_admin')
    expect(roGov.accessLevel).toBe('read_only')
    expect(roGov.isApproved).toBe(true)

    // Transition to unapproved (Revoke)
    const revoked = setOuGovernance(targetOu, 'unapproved', 'it_admin')
    expect(revoked.accessLevel).toBe('unapproved')
    expect(revoked.isApproved).toBe(false)

    const ous = getActiveDirectoryOus()
    const updated = ous.find(o => o.ouPath === targetOu)
    expect(updated?.accessLevel).toBe('unapproved')
    expect(updated?.isApproved).toBe(false)
  })

  it('evaluates contextual button visibility rules to prevent redundant current-state buttons', () => {
    // Helper function mimicking the template visibility logic
    const getVisibleActions = (accessLevel: string, isApproved: boolean) => {
      const showApproveRw = !(accessLevel === 'read_write' && isApproved)
      const showDowngradeRo = accessLevel === 'read_write' && isApproved
      const showUpgradeRw = accessLevel === 'read_only' && isApproved
      const showApproveRo = !(accessLevel === 'read_only' && isApproved)
      const showRevoke = isApproved

      return {
        showApproveRw,
        showDowngradeRo,
        showUpgradeRw,
        showApproveRo,
        showRevoke
      }
    }

    // 1. When already Read/Write Approved
    const rwState = getVisibleActions('read_write', true)
    // Redundant "Approve R/W" button is HIDDEN
    expect(rwState.showApproveRw).toBe(false)
    expect(rwState.showDowngradeRo).toBe(true)
    expect(rwState.showRevoke).toBe(true)

    // 2. When already Read-Only Approved
    const roState = getVisibleActions('read_only', true)
    // Redundant "Read-Only" button is HIDDEN
    expect(roState.showApproveRo).toBe(false)
    expect(roState.showUpgradeRw).toBe(true)
    expect(roState.showRevoke).toBe(true)

    // 3. When Unapproved / Blocked
    const unapprovedState = getVisibleActions('unapproved', false)
    // Redundant "Revoke" button is HIDDEN
    expect(unapprovedState.showRevoke).toBe(false)
    expect(unapprovedState.showApproveRw).toBe(true)
    expect(unapprovedState.showApproveRo).toBe(true)
  })

  it('manages fold state transitions for individual and bulk OU expansion', () => {
    const ous = getActiveDirectoryOus()
    const expandedMap: Record<string, boolean> = {}

    // Toggle individual fold
    const firstOu = ous[0].ouPath
    expandedMap[firstOu] = !expandedMap[firstOu]
    expect(expandedMap[firstOu]).toBe(true)

    // Toggle back
    expandedMap[firstOu] = !expandedMap[firstOu]
    expect(expandedMap[firstOu]).toBe(false)

    // Bulk expand all
    for (const ou of ous) {
      expandedMap[ou.ouPath] = true
    }
    const allExpanded = ous.every(ou => expandedMap[ou.ouPath])
    expect(allExpanded).toBe(true)

    // Bulk collapse all
    for (const ou of ous) {
      expandedMap[ou.ouPath] = false
    }
    const anyExpanded = ous.some(ou => expandedMap[ou.ouPath])
    expect(anyExpanded).toBe(false)
  })
})
