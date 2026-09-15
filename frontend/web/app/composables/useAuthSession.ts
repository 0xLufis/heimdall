import { computed, ref } from 'vue'
import { authClient } from '~/utils/auth-client'

export interface DemoPersona {
  id: string
  name: string
  email: string
  role: 'system_admin' | 'heimdall_admin' | 'plant_director' | 'plant_engineering_manager' | 'senior_engineering_manager' | 'it_site_admin' | 'it_admin' | 'engineering_admin' | 'operative_planner' | 'manager' | 'group_leader' | 'shift_leader' | 'engineer' | 'technician' | 'admin'
  description?: string
  title?: string
  department?: string
}

export const DEMO_PERSONAS: DemoPersona[] = [
  {
    id: 'usr-sysadmin-1',
    name: 'Root System Administrator',
    email: 'sysadmin@heimdall.dev',
    role: 'system_admin',
    title: 'Root System Administrator',
    department: 'Platform Operations',
    description: 'Superuser: Complete System & God-Level Administration'
  },
  {
    id: 'usr-director-1',
    name: 'Dr. Henrik Weber (Plant Director)',
    email: 'henrik.weber@factory.corp',
    role: 'plant_director',
    title: 'Plant Director',
    department: 'Plant Management',
    description: 'Executive: Final Approval, Line Stop Authorization & Plant-Wide Read Access'
  },
  {
    id: 'usr-plant-eng-1',
    name: 'Gábor Varga (Plant Engineering Manager)',
    email: 'gabor.varga@factory.corp',
    role: 'plant_engineering_manager',
    title: 'Plant Engineering Manager',
    department: 'Plant Engineering',
    description: 'Head of Site Engineering: Oversees Controls, Robotics, SMT & Assembly'
  },
  {
    id: 'usr-senior-eng-1',
    name: 'Elena Rostova (Senior Engineering Manager)',
    email: 'elena.rostova@factory.corp',
    role: 'senior_engineering_manager',
    title: 'Senior Engineering Manager',
    department: 'Engineering Operations',
    description: 'Senior Engineering Manager: Battery Module & Pack Operations'
  },
  {
    id: 'usr-heimdall-admin-1',
    name: 'Heimdall Administrator',
    email: 'admin@heimdall.dev',
    role: 'heimdall_admin',
    title: 'Heimdall Platform Administrator',
    department: 'Platform Administration',
    description: 'Platform Admin: Studio & Tenant Governance (Pseudo-IT Admin if enabled)'
  },
  {
    id: 'usr-itadmin-1',
    name: 'IT Infrastructure Administrator',
    email: 'it.admin@heimdall.dev',
    role: 'it_admin',
    title: 'IT Infrastructure Administrator',
    department: 'Enterprise IT',
    description: 'IT Admin: Active Directory, Entra ID OU Approvals & PKI'
  },
  {
    id: 'usr-itsiteadmin-1',
    name: 'Marcus Vance (IT Site Administrator)',
    email: 'marcus.vance@factory.corp',
    role: 'it_site_admin',
    title: 'IT Site Administrator',
    department: 'Industrial IT',
    description: 'Outside Approver: AD / Entra ID OU Ingress Approvals, PKI & Plant Networks'
  },
  {
    id: 'usr-engadmin-1',
    name: 'Engineering Administrator',
    email: 'eng.admin@heimdall.dev',
    role: 'engineering_admin',
    title: 'Engineering Administrator',
    department: 'Functional Engineering Systems',
    description: 'Engineering Admin: Functional Settings, OT Telemetry & App Users'
  },
  {
    id: 'usr-manager-andras',
    name: 'András Molnár (Plant Manager)',
    email: 'andras.manager@heimdall.dev',
    role: 'manager',
    title: 'Plant Operations Manager',
    department: 'Plant Operations',
    description: 'Line Management: Plant Manager (All Teams & Technologies)'
  },
  {
    id: 'usr-op-planner-1',
    name: 'András Molnár (Operative Planner)',
    email: 'andras.planner@factory.corp',
    role: 'operative_planner',
    title: 'Operative Line Planner',
    department: 'Line Operations Planning',
    description: 'Outside Approver: Line Stops, Production Windows & Shift Quotas'
  },
  {
    id: 'usr-gl-assy-1',
    name: 'Sally Vance (Group Leader – Fastening & Assembly)',
    email: 'sally.vance@factory.corp',
    role: 'group_leader',
    title: 'Engineering Group Leader',
    department: 'Fastening & Assembly',
    description: 'Engineering Group Leader: Supervises Screwing & Dispensing Stations'
  },
  {
    id: 'usr-ferenc',
    name: 'Shift Leader Ferenc',
    email: 'ferenc.leader@heimdall.dev',
    role: 'shift_leader',
    title: 'Shift Operations Leader',
    department: 'Shift Operations',
    description: 'Tier 2: Technician Shift Leader (Shift Technicians & Absences)'
  },
  {
    id: 'usr-eng-robotics-1',
    name: 'Alex Novak (Robotics & SMT Controls Engineer)',
    email: 'alex.novak@factory.corp',
    role: 'engineer',
    title: 'Controls & Robotics Engineer',
    department: 'Automation & Controls',
    description: 'Discipline Specialist: KUKA / Fanuc Manipulators & AOI Inspection'
  },
  {
    id: 'usr-tech-welding-1',
    name: 'István Kovács (Laser Welding & Mechanical Tech)',
    email: 'istvan.kovacs@factory.corp',
    role: 'technician',
    title: 'Maintenance Technician',
    department: 'Laser Welding Maintenance',
    description: 'Shop-Floor Specialist: Laser Welding Maintenance & Calibration'
  }
]

const fallbackSimulatedPersona = ref<DemoPersona | null>(null)
const fallbackAdminRoleDelegation = ref({
  heimdallAdminIsPseudoItAdmin: true,
  allowEngineeringAdminUserCreation: true
})

export const useAuthSession = () => {
  const sessionQuery = authClient.useSession()
  const activeOrgQuery = authClient.useActiveOrganization()
  const isSwitchingOrg = ref(false)

  const testCookie = typeof useCookie !== 'undefined'
    ? useCookie('heimdall_test_session')
    : ref<string | null>(null)
  const defaultTestUser = DEMO_PERSONAS[0]

  const getStoredPersona = (): DemoPersona | null => {
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        const raw = localStorage.getItem('heimdall_simulated_persona')
        if (raw) return JSON.parse(raw)
      } catch {}
    }
    return null
  }

  // Simulated persona state for testing / role switching
  const simulatedPersona = typeof useState !== 'undefined'
    ? useState<DemoPersona | null>('auth_simulated_persona', () => getStoredPersona())
    : fallbackSimulatedPersona

  // Re-hydrate on client mount if needed
  if (typeof window !== 'undefined') {
    const stored = getStoredPersona()
    if (stored && !simulatedPersona.value) {
      simulatedPersona.value = stored
    }
  }

  const session = computed(() => {
    if (simulatedPersona.value) {
      return { user: simulatedPersona.value }
    }
    return sessionQuery.data?.value || (testCookie.value === 'true' ? { user: defaultTestUser } : null)
  })

  const user = computed(() => {
    if (simulatedPersona.value) return simulatedPersona.value
    return session.value?.user || (testCookie.value === 'true' ? defaultTestUser : null)
  })

  const isAuthenticated = computed(() => !!user.value)
  const userRole = computed<string>(() => (user.value as any)?.role || 'admin')
  const activeOrg = computed(() => activeOrgQuery.data?.value || { id: 'org-platform', name: 'Platform Operations (Synthetic AI Guild)' })

  // Dynamic Admin Role Delegation state (SysAdmin controlled)
  const adminRoleDelegation = typeof useState !== 'undefined'
    ? useState<{ heimdallAdminIsPseudoItAdmin: boolean; allowEngineeringAdminUserCreation: boolean }>('admin_role_delegation', () => ({
        heimdallAdminIsPseudoItAdmin: true,
        allowEngineeringAdminUserCreation: true
      }))
    : fallbackAdminRoleDelegation

  // Lean & mean regex pattern matching helper
  const match = (...patterns: (RegExp | string)[]) => {
    const r = (userRole.value || '').toLowerCase()
    return patterns.some(p => typeof p === 'string' ? r === p.toLowerCase() : p.test(r))
  }

  // Specific role checks (hierarchical & pattern-matched)
  const isSystemAdmin = computed(() => match(/^system_admin$/, /^sysadmin$/))
  const isHeimdallAdmin = computed(() => isSystemAdmin.value || match(/heimdall_admin/, /^admin$/))
  const isItAdmin = computed(() => {
    if (isSystemAdmin.value || match(/it(_site)?_admin/)) return true
    return adminRoleDelegation.value.heimdallAdminIsPseudoItAdmin && isHeimdallAdmin.value
  })
  const isItSiteAdmin = computed(() => isItAdmin.value)
  const isEngineeringAdmin = computed(() => isSystemAdmin.value || match(/engineering_admin/, /plant_engineering_manager/, /senior_engineering_manager/))
  const isPlantDirector = computed(() => isSystemAdmin.value || match(/plant_director/))
  const isPlantEngineeringManager = computed(() => isPlantDirector.value || match(/plant_engineering_manager/))
  const isSeniorEngineeringManager = computed(() => isPlantEngineeringManager.value || match(/senior_engineering_manager/))
  const isOperativePlanner = computed(() => isSystemAdmin.value || match(/operative_planner/, /manager/, /plant_director/))

  // Broad role groups (pattern-driven)
  const isAdmin = computed(() => isSystemAdmin.value || match(/admin/, /plant_director/))
  const isEngineeringManager = computed(() => isAdmin.value || match(/manager/, /operative_planner/))
  const isGroupLeader = computed(() => isEngineeringManager.value || match(/group_leader/, /lead_engineer/, /team_lead/))
  const isShiftLeader = computed(() => isEngineeringManager.value || match(/shift_leader/, /group_leader/))
  const isEngineer = computed(() => isAdmin.value || isEngineeringManager.value || match(/engineer/, /team_lead/))
  const isTechnician = computed(() => isEngineer.value || match(/technician/, /shift_leader/))

  // Plant Line Management Dedication Tier
  const dedicationTier = computed<'self' | 'shift' | 'group' | 'manager'>(() => {
    if (match(/manager|operative_planner|admin/)) return 'manager'
    if (match(/group_leader|lead_engineer|team_lead/)) return 'group'
    if (match(/shift_leader/)) return 'shift'
    return 'self'
  })

  // Capability policies: Admin users and IT admins allowed to create user roles & manage users
  const canManageUsers = computed(() => {
    if (isSystemAdmin.value || isAdmin.value || isItAdmin.value) return true
    if (match(/engineering_admin/)) return adminRoleDelegation.value.allowEngineeringAdminUserCreation
    return match(/plant_engineering_manager/)
  })
  const canCreateUserRoles = computed(() => isSystemAdmin.value || isAdmin.value || isItAdmin.value || canManageUsers.value)
  const canCreateRoles = computed(() => canCreateUserRoles.value)
  const canManageActiveDirectory = computed(() => isItAdmin.value)
  const canManageFunctionalSettings = computed(() => isSystemAdmin.value || isEngineer.value)
  const canManageEndpoints = computed(() => isSystemAdmin.value || isEngineer.value)
  const canExecuteRemote = computed(() => isSystemAdmin.value || isEngineer.value)
  const canAdministerSystem = computed(() => isSystemAdmin.value || isHeimdallAdmin.value || match(/plant_director/))
  const canApproveLineStops = computed(() => isSystemAdmin.value || match(/plant_director/, /plant_engineering_manager/, /operative_planner/, /group_leader/))

  const setSimulatedPersona = (persona: DemoPersona | null) => {
    simulatedPersona.value = persona
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        if (persona) {
          localStorage.setItem('heimdall_simulated_persona', JSON.stringify(persona))
        } else {
          localStorage.removeItem('heimdall_simulated_persona')
        }
      } catch {}
    }
  }

  const clearSimulatedPersona = () => {
    setSimulatedPersona(null)
  }

  const setAdminRoleDelegation = (delegation: { heimdallAdminIsPseudoItAdmin?: boolean; allowEngineeringAdminUserCreation?: boolean }) => {
    adminRoleDelegation.value = { ...adminRoleDelegation.value, ...delegation }
  }

  const switchOrganization = async (organizationId: string) => {
    isSwitchingOrg.value = true
    try {
      await authClient.organization.setActive({ organizationId })
    } finally {
      isSwitchingOrg.value = false
    }
  }

  const signOut = async () => {
    testCookie.value = null
    setSimulatedPersona(null)
    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        localStorage.removeItem('heimdall_simulated_persona')
        localStorage.removeItem('auth_simulated_persona')
      } catch {}
    }
    if (typeof useState !== 'undefined') {
      const authSession = useState<{ authenticated: boolean; user?: any } | null>('auth_user_session', () => null)
      authSession.value = null
    }
    try {
      await authClient.signOut()
    } catch (e) {
      console.warn('[useAuthSession] signOut error:', e)
    }
    if (typeof window !== 'undefined') {
      window.location.href = '/auth/login'
    } else if (typeof navigateTo !== 'undefined') {
      await navigateTo('/auth/login')
    }
  }

  return {
    session,
    user,
    isAuthenticated,
    userRole,
    activeOrg,
    isAdmin,
    isSystemAdmin,
    isPlantDirector,
    isPlantEngineeringManager,
    isSeniorEngineeringManager,
    isHeimdallAdmin,
    isItAdmin,
    isItSiteAdmin,
    isEngineeringAdmin,
    isOperativePlanner,
    isEngineeringManager,
    isGroupLeader,
    isShiftLeader,
    isEngineer,
    isTechnician,
    dedicationTier,
    simulatedPersona,
    setSimulatedPersona,
    clearSimulatedPersona,
    adminRoleDelegation,
    setAdminRoleDelegation,
    canManageUsers,
    canCreateUserRoles,
    canCreateRoles,
    canManageActiveDirectory,
    canManageFunctionalSettings,
    canManageEndpoints,
    canExecuteRemote,
    canAdministerSystem,
    canApproveLineStops,
    isSwitchingOrg,
    switchOrganization,
    signOut,
    DEMO_PERSONAS
  }
}
