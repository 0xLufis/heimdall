import { computed } from 'vue'
import { useAuthSession } from '~/composables/useAuthSession'

export const RBAC_TOOLTIPS = {
  SYSTEM_ADMIN: 'Requires Root System Administrator privilege.',
  SYSTEM_GOVERNANCE: 'Requires System Administrator, Platform Administrator, or Plant Director privilege.',
  IT_ADMIN: 'Requires IT Infrastructure Administrator or System Administrator privilege.',
  USER_MANAGEMENT: 'Requires Engineering Administrator or System Administrator privilege.',
  ENDPOINT_MANAGEMENT: 'Requires Engineering Administrator, Controls Engineer, or System Administrator privilege.',
  REMOTE_EXECUTION: 'Requires Engineering Administrator, Controls Engineer, or System Administrator to dispatch edge commands.',
  LINE_STOP: 'Requires Operative Planner, Plant Director, or Plant Engineering Manager privilege to initiate line stops.',
  ORGANIZATION_MANAGEMENT: 'Requires System Administrator or Platform Administrator privilege to manage organizations.',
  DEDICATION_TIER_SELF: 'Technicians can only manage self-dedication rules.',
  DEDICATION_TIER_SHIFT: 'Shift Leaders can only assign shift-level technician dedications.',
  DEDICATION_TIER_GROUP: 'Group Leaders can assign group and shift technician dedications.',
  DEDICATION_TIER_MANAGER: 'Requires Plant Manager or System Administrator privilege.',
} as const

export function useRbacPermission() {
  const auth = useAuthSession()

  function checkCapability(capability: string): boolean {
    if (!capability) return true
    const val = (auth as any)[capability]
    if (val === undefined) return false
    return typeof val === 'object' && val !== null && 'value' in val ? Boolean(val.value) : Boolean(val)
  }

  function getCapabilityTooltip(capability: string): string {
    switch (capability) {
      case 'isSystemAdmin':
        return RBAC_TOOLTIPS.SYSTEM_ADMIN
      case 'canAdministerSystem':
        return RBAC_TOOLTIPS.SYSTEM_GOVERNANCE
      case 'canManageActiveDirectory':
      case 'isItAdmin':
      case 'isItSiteAdmin':
        return RBAC_TOOLTIPS.IT_ADMIN
      case 'canManageUsers':
        return RBAC_TOOLTIPS.USER_MANAGEMENT
      case 'canManageEndpoints':
      case 'canManageFunctionalSettings':
        return RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT
      case 'canExecuteRemote':
        return RBAC_TOOLTIPS.REMOTE_EXECUTION
      case 'canApproveLineStops':
      case 'isOperativePlanner':
        return RBAC_TOOLTIPS.LINE_STOP
      case 'isAdmin':
        return RBAC_TOOLTIPS.ORGANIZATION_MANAGEMENT
      default:
        return 'You do not have the required permissions for this action.'
    }
  }

  return {
    ...auth,
    RBAC_TOOLTIPS,
    checkCapability,
    getCapabilityTooltip,
  }
}
