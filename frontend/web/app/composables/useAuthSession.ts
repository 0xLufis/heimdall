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

  // Simulated persona state for testing / role switching
  const simulatedPersona = typeof useState !== 'undefined'
    ? useState<DemoPersona | null>('auth_simulated_persona', () => null)
    : fallbackSimulatedPersona

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
  const activeOrg = computed(() => activeOrgQuery.data?.value || { id: 'org-1', name: 'Heimdall Engineering' })

  // Dynamic Admin Role Delegation state (SysAdmin controlled)
  const adminRoleDelegation = typeof useState !== 'undefined'
    ? useState<{ heimdallAdminIsPseudoItAdmin: boolean; allowEngineeringAdminUserCreation: boolean }>('admin_role_delegation', () => ({
        heimdallAdminIsPseudoItAdmin: true,
        allowEngineeringAdminUserCreation: true
      }))
    : fallbackAdminRoleDelegation

  // Specific role checks
  const isSystemAdmin = computed(() => userRole.value === 'system_admin')
  const isPlantDirector = computed(() => ['system_admin', 'plant_director'].includes(userRole.value))
  const isPlantEngineeringManager = computed(() => ['system_admin', 'plant_director', 'plant_engineering_manager'].includes(userRole.value))
  const isSeniorEngineeringManager = computed(() => ['system_admin', 'plant_director', 'plant_engineering_manager', 'senior_engineering_manager'].includes(userRole.value))
  const isHeimdallAdmin = computed(() => ['system_admin', 'heimdall_admin', 'admin'].includes(userRole.value))
  
  // IT Admin check with dynamic pseudo-IT admin role inheritance
  const isItAdmin = computed(() => {
    if (['system_admin', 'it_admin', 'it_site_admin'].includes(userRole.value)) return true
    if (adminRoleDelegation.value.heimdallAdminIsPseudoItAdmin && ['heimdall_admin', 'admin'].includes(userRole.value)) return true
    return false
  })
  const isItSiteAdmin = computed(() => isItAdmin.value)
  const isEngineeringAdmin = computed(() => ['system_admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager'].includes(userRole.value))
  const isOperativePlanner = computed(() => ['system_admin', 'operative_planner', 'manager', 'plant_director'].includes(userRole.value))

  // Broad role groups
  const isAdmin = computed(() => ['system_admin', 'heimdall_admin', 'admin', 'it_admin', 'it_site_admin', 'engineering_admin', 'plant_director'].includes(userRole.value))
  const isEngineeringManager = computed(() => ['manager', 'operative_planner', 'plant_director', 'plant_engineering_manager', 'senior_engineering_manager', 'admin', 'heimdall_admin', 'system_admin'].includes(userRole.value))
  const isGroupLeader = computed(() => ['group_leader', 'lead_engineer', 'team_lead', 'manager', 'operative_planner', 'plant_engineering_manager', 'senior_engineering_manager', 'plant_director', 'admin', 'heimdall_admin', 'system_admin'].includes(userRole.value))
  const isShiftLeader = computed(() => ['shift_leader', 'group_leader', 'manager', 'operative_planner', 'admin', 'heimdall_admin', 'system_admin'].includes(userRole.value))
  const isEngineer = computed(() => ['engineer', 'controls_engineer', 'lead_engineer', 'team_lead', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'manager', 'admin', 'heimdall_admin', 'system_admin'].includes(userRole.value))
  const isTechnician = computed(() => ['technician', 'engineer', 'controls_engineer', 'lead_engineer', 'team_lead', 'shift_leader', 'engineering_admin', 'admin', 'heimdall_admin', 'system_admin'].includes(userRole.value))

  // Plant Line Management Dedication Tier (engineering_admin is NOT in line management)
  const dedicationTier = computed<'self' | 'shift' | 'group' | 'manager'>(() => {
    const r = userRole.value.toLowerCase()
    if (r === 'manager' || r === 'operative_planner' || r === 'admin' || r === 'heimdall_admin' || r === 'system_admin') return 'manager'
    if (r === 'group_leader' || r === 'lead_engineer' || r === 'team_lead') return 'group'
    if (r === 'shift_leader') return 'shift'
    return 'self'
  })

  // Capability policies
  const canManageUsers = computed(() => ['system_admin', 'heimdall_admin', 'admin', 'engineering_admin', 'plant_engineering_manager'].includes(userRole.value))
  const canManageActiveDirectory = computed(() => isItAdmin.value)
  const canManageFunctionalSettings = computed(() => ['system_admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'heimdall_admin', 'admin', 'lead_engineer', 'engineer'].includes(userRole.value))
  const canManageEndpoints = computed(() => ['system_admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'heimdall_admin', 'admin', 'lead_engineer', 'engineer', 'controls_engineer'].includes(userRole.value))
  const canExecuteRemote = computed(() => ['system_admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager', 'heimdall_admin', 'admin', 'lead_engineer', 'engineer'].includes(userRole.value))
  const canAdministerSystem = computed(() => ['system_admin', 'heimdall_admin', 'admin', 'plant_director'].includes(userRole.value))
  const canApproveLineStops = computed(() => ['system_admin', 'plant_director', 'plant_engineering_manager', 'operative_planner', 'group_leader'].includes(userRole.value))

  const setSimulatedPersona = (persona: DemoPersona | null) => {
    simulatedPersona.value = persona
  }

  const clearSimulatedPersona = () => {
    simulatedPersona.value = null
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
    simulatedPersona.value = null
    if (typeof useState !== 'undefined') {
      const authSession = useState<{ authenticated: boolean; user?: any } | null>('auth_user_session', () => null)
      authSession.value = null
    }
    await authClient.signOut({
      fetchOptions: {
        onSuccess: () => {
          if (typeof navigateTo !== 'undefined') {
            navigateTo('/auth/login')
          }
        }
      }
    })
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
