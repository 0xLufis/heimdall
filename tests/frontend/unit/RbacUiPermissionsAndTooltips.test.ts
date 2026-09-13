import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref, computed } from 'vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '../../../frontend/web/app/composables/useRbacPermission'
import RbacButton from '../../../frontend/web/app/components/common/RbacButton.vue'
import RbacTooltip from '../../../frontend/web/app/components/common/RbacTooltip.vue'
import { navMenu, navMenuBottom } from '../../../frontend/web/app/constants/menus'

// Mock useAuthSession with reactive computed capability matrix
const mockCurrentRole = ref('engineer')

vi.mock('../../../frontend/web/app/composables/useAuthSession', () => {
  return {
    useAuthSession: () => {
      const userRole = computed(() => mockCurrentRole.value)
      const isSystemAdmin = computed(() => userRole.value === 'system_admin')
      const isItAdmin = computed(() => ['system_admin', 'it_admin', 'it_site_admin'].includes(userRole.value))
      const isItSiteAdmin = computed(() => isItAdmin.value)
      const isEngineeringAdmin = computed(() => ['system_admin', 'engineering_admin'].includes(userRole.value))
      const isOperativePlanner = computed(() => ['system_admin', 'operative_planner', 'manager'].includes(userRole.value))
      const isAdmin = computed(() => ['system_admin', 'heimdall_admin', 'admin', 'it_admin', 'engineering_admin'].includes(userRole.value))
      const isTechnician = computed(() => ['technician', 'engineer', 'lead_engineer', 'system_admin'].includes(userRole.value))
      
      const canManageUsers = computed(() => ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin'].includes(userRole.value))
      const canManageActiveDirectory = computed(() => isItAdmin.value)
      const canManageEndpoints = computed(() => ['system_admin', 'engineering_admin', 'lead_engineer', 'engineer', 'controls_engineer'].includes(userRole.value))
      const canExecuteRemote = computed(() => ['system_admin', 'engineering_admin', 'lead_engineer', 'engineer'].includes(userRole.value))
      const canAdministerSystem = computed(() => ['system_admin', 'heimdall_admin', 'admin'].includes(userRole.value))
      const canApproveLineStops = computed(() => ['system_admin', 'operative_planner', 'manager'].includes(userRole.value))
      const canApproveOus = computed(() => isItAdmin.value)

      return {
        userRole,
        isSystemAdmin,
        isItAdmin,
        isItSiteAdmin,
        isEngineeringAdmin,
        isOperativePlanner,
        isAdmin,
        isTechnician,
        canManageUsers,
        canManageActiveDirectory,
        canManageEndpoints,
        canExecuteRemote,
        canAdministerSystem,
        canApproveLineStops,
        canApproveOus,
      }
    }
  }
})

describe('RBAC UI Permissions & Tooltip Hardening Test Suite', () => {
  beforeEach(() => {
    mockCurrentRole.value = 'engineer'
  })

  describe('useRbacPermission Capabilities Matrix', () => {
    it('evaluates engineer role capabilities accurately', () => {
      mockCurrentRole.value = 'engineer'
      const rbac = useRbacPermission()

      expect(rbac.canExecuteRemote.value).toBe(true)
      expect(rbac.canManageEndpoints.value).toBe(true)
      expect(rbac.canManageUsers.value).toBe(false)
      expect(rbac.canManageActiveDirectory.value).toBe(false)
      expect(rbac.isSystemAdmin.value).toBe(false)
      expect(rbac.canAdministerSystem.value).toBe(false)
    })

    it('evaluates system_admin (superuser) role capabilities accurately', () => {
      mockCurrentRole.value = 'system_admin'
      const rbac = useRbacPermission()

      expect(rbac.canExecuteRemote.value).toBe(true)
      expect(rbac.canManageEndpoints.value).toBe(true)
      expect(rbac.canManageUsers.value).toBe(true)
      expect(rbac.canManageActiveDirectory.value).toBe(true)
      expect(rbac.isSystemAdmin.value).toBe(true)
      expect(rbac.canAdministerSystem.value).toBe(true)
      expect(rbac.canApproveLineStops.value).toBe(true)
      expect(rbac.canApproveOus.value).toBe(true)
    })

    it('evaluates it_admin role capabilities accurately', () => {
      mockCurrentRole.value = 'it_admin'
      const rbac = useRbacPermission()

      expect(rbac.canManageActiveDirectory.value).toBe(true)
      expect(rbac.canApproveOus.value).toBe(true)
      expect(rbac.canManageUsers.value).toBe(false)
      expect(rbac.canExecuteRemote.value).toBe(false)
    })

    it('evaluates operative_planner role capabilities accurately', () => {
      mockCurrentRole.value = 'operative_planner'
      const rbac = useRbacPermission()

      expect(rbac.canApproveLineStops.value).toBe(true)
      expect(rbac.canManageEndpoints.value).toBe(false)
      expect(rbac.canExecuteRemote.value).toBe(false)
    })

    it('evaluates operator/viewer role restrictions accurately', () => {
      mockCurrentRole.value = 'operator'
      const rbac = useRbacPermission()

      expect(rbac.canExecuteRemote.value).toBe(false)
      expect(rbac.canManageEndpoints.value).toBe(false)
      expect(rbac.canManageUsers.value).toBe(false)
      expect(rbac.canAdministerSystem.value).toBe(false)
      expect(rbac.canApproveLineStops.value).toBe(false)
    })
  })

  describe('RBAC_TOOLTIPS Canonical Messages', () => {
    it('contains clear, descriptive role requirement messages for each key action', () => {
      expect(RBAC_TOOLTIPS.ENDPOINT_MANAGEMENT).toContain('Engineering Administrator')
      expect(RBAC_TOOLTIPS.REMOTE_EXECUTION).toContain('Engineering Administrator')
      expect(RBAC_TOOLTIPS.USER_MANAGEMENT).toContain('Engineering Administrator')
      expect(RBAC_TOOLTIPS.SYSTEM_ADMIN).toContain('System Administrator')
      expect(RBAC_TOOLTIPS.SYSTEM_GOVERNANCE).toContain('System Administrator')
      expect(RBAC_TOOLTIPS.IT_ADMIN).toContain('IT Infrastructure Administrator')
      expect(RBAC_TOOLTIPS.LINE_STOP).toContain('Operative Planner')
    })
  })

  describe('RbacButton Component Rendering & Tooltip Binding', () => {
    it('renders enabled button when user possesses the required capability', () => {
      mockCurrentRole.value = 'engineer'
      const wrapper = mount(RbacButton, {
        props: {
          capability: 'canExecuteRemote'
        },
        slots: {
          default: 'Execute Command'
        },
        global: {
          components: {
            RbacTooltip
          },
          stubs: {
            Lock: { template: '<span class="lock-icon" />' }
          }
        }
      })

      const button = wrapper.find('button')
      expect(button.exists()).toBe(true)
      expect(button.attributes('disabled')).toBeUndefined()
      expect(wrapper.find('.lock-icon').exists()).toBe(false)
      expect(wrapper.text()).toContain('Execute Command')
    })

    it('renders disabled button with lock icon when user lacks capability', () => {
      mockCurrentRole.value = 'operator' // operator cannot execute remote
      const wrapper = mount(RbacButton, {
        props: {
          capability: 'canExecuteRemote'
        },
        slots: {
          default: 'Execute Command'
        },
        global: {
          stubs: {
            RbacTooltip: false,
            TooltipProvider: { template: '<div><slot /></div>' },
            Tooltip: { template: '<div><slot /></div>' },
            TooltipTrigger: { template: '<div><slot /></div>' },
            TooltipContent: { template: '<div class="tooltip-content-stub"><slot /></div>' }
          }
        }
      })

      expect(wrapper.find('button').exists()).toBe(true)
      expect(wrapper.find('button').attributes('disabled')).toBeDefined()
      expect(wrapper.find('svg').exists()).toBe(true) // Lock icon svg from lucide-vue-next
      expect(wrapper.text()).toContain('Execute Command')
    })
  })

  describe('Navigation Menus RBAC Configuration', () => {
    it('hides Identity Studio from unauthorized users', () => {
      const allItems = navMenu.flatMap(group => group.items)
      const identityStudio = allItems.find(item => item.title === 'Identity Studio')

      expect(identityStudio).toBeDefined()
      expect(identityStudio?.hideWhenUnauthorized).toBe(true)
      expect(identityStudio?.requiredCapability).toBe('isSystemAdmin')
    })

    it('gates System Governance and Security Groups behind required capabilities', () => {
      const allItems = navMenu.flatMap(group => group.items)
      const governance = allItems.find(item => item.title === 'System Governance')
      const securityGroups = allItems.find(item => item.title === 'Security Groups')

      expect(governance?.requiredCapability).toBe('canAdministerSystem')
      expect(securityGroups?.requiredCapability).toBe('canManageActiveDirectory')
    })

    it('includes Live Telemetry route in navMenu', () => {
      const allItems = navMenu.flatMap(group => group.items)
      const liveTelemetry = allItems.find(item => item.link === '/dashboard/telemetry')

      expect(liveTelemetry).toBeDefined()
      expect(liveTelemetry?.title).toBe('Live Telemetry')
    })
  })
})
