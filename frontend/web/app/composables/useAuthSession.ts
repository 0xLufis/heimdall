import { computed, ref } from 'vue'
import { authClient } from '~/utils/auth-client'

export interface DemoPersona {
  id: string
  name: string
  email: string
  role: 'technician' | 'engineer' | 'shift_leader' | 'group_leader' | 'manager' | 'admin'
  description?: string
}

export const DEMO_PERSONAS: DemoPersona[] = [
  {
    id: 'usr-admin-1',
    name: 'System Administrator',
    email: 'admin@heimdall.dev',
    role: 'admin',
    description: 'Tier 4: Full Cross-Cutting Plant Governance'
  },
  {
    id: 'usr-manager-andras',
    name: 'András Molnár (Plant Manager)',
    email: 'andras.manager@heimdall.dev',
    role: 'manager',
    description: 'Tier 4: Engineering Manager (All Teams & Technologies)'
  },
  {
    id: 'usr-orwell',
    name: 'Engineer Orwell',
    email: 'orwell.audi@heimdall.dev',
    role: 'group_leader',
    description: 'Tier 3: Engineering Group Leader (Stations, Lines & Technologies)'
  },
  {
    id: 'usr-ferenc',
    name: 'Shift Leader Ferenc',
    email: 'ferenc.leader@heimdall.dev',
    role: 'shift_leader',
    description: 'Tier 2: Technician Shift Leader (Shift Technicians & Absences)'
  },
  {
    id: 'usr-sally',
    name: 'Engineer Sally',
    email: 'sally.milling@heimdall.dev',
    role: 'engineer',
    description: 'Tier 1: Engineer (Self-Dedication Only)'
  },
  {
    id: 'usr-kovacs',
    name: 'István Kovács',
    email: 'istvan.kovacs@heimdall.dev',
    role: 'technician',
    description: 'Tier 1: Technician (Self-Dedication Only)'
  }
]

export const useAuthSession = () => {
  const sessionQuery = authClient.useSession()
  const activeOrgQuery = authClient.useActiveOrganization()
  const isSwitchingOrg = ref(false)

  const testCookie = useCookie('heimdall_test_session')
  const defaultTestUser = DEMO_PERSONAS[0]

  // Simulated persona state for testing / role switching
  const simulatedPersona = useState<DemoPersona | null>('auth_simulated_persona', () => null)

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

  // Role checks
  const isAdmin = computed(() => ['admin', 'system_admin'].includes(userRole.value))
  const isEngineeringManager = computed(() => ['manager', 'admin', 'system_admin'].includes(userRole.value))
  const isGroupLeader = computed(() => ['group_leader', 'lead_engineer', 'team_lead', 'manager', 'admin', 'system_admin'].includes(userRole.value))
  const isShiftLeader = computed(() => ['shift_leader', 'group_leader', 'manager', 'admin', 'system_admin'].includes(userRole.value))
  const isEngineer = computed(() => ['engineer', 'controls_engineer', 'lead_engineer', 'team_lead', 'manager', 'admin', 'system_admin'].includes(userRole.value))
  const isTechnician = computed(() => ['technician', 'engineer', 'controls_engineer', 'lead_engineer', 'team_lead', 'shift_leader', 'admin', 'system_admin'].includes(userRole.value))

  // Dedication Tier:
  // 'self': Only dedicate themselves (Engineer & Technician)
  // 'shift': Can dedicate shift technicians (Shift Leader)
  // 'group': Can dedicate engineers and technicians to stations, lines AND technologies (Group Leader)
  // 'manager': Can dedicate any team to any technology or group of machines (Manager & Admin)
  const dedicationTier = computed<'self' | 'shift' | 'group' | 'manager'>(() => {
    const r = userRole.value.toLowerCase()
    if (r === 'manager' || r === 'admin' || r === 'system_admin') return 'manager'
    if (r === 'group_leader' || r === 'lead_engineer' || r === 'team_lead') return 'group'
    if (r === 'shift_leader') return 'shift'
    return 'self'
  })

  // Capability policies
  const canManageEndpoints = computed(() => ['admin', 'system_admin', 'lead_engineer', 'engineer', 'controls_engineer'].includes(userRole.value))
  const canExecuteRemote = computed(() => ['admin', 'system_admin', 'lead_engineer', 'engineer'].includes(userRole.value))
  const canAdministerSystem = computed(() => ['admin', 'system_admin'].includes(userRole.value))

  const setSimulatedPersona = (persona: DemoPersona | null) => {
    simulatedPersona.value = persona
  }

  const clearSimulatedPersona = () => {
    simulatedPersona.value = null
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
    const authSession = useState<{ authenticated: boolean; user?: any } | null>('auth_user_session', () => null)
    authSession.value = null
    await authClient.signOut({
      fetchOptions: {
        onSuccess: () => {
          navigateTo('/auth/login')
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
    isEngineeringManager,
    isGroupLeader,
    isShiftLeader,
    isEngineer,
    isTechnician,
    dedicationTier,
    simulatedPersona,
    setSimulatedPersona,
    clearSimulatedPersona,
    canManageEndpoints,
    canExecuteRemote,
    canAdministerSystem,
    isSwitchingOrg,
    switchOrganization,
    signOut
  }
}
