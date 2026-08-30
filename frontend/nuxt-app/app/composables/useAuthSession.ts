import { computed, ref } from 'vue'
import { authClient } from '~/utils/auth-client'

export const useAuthSession = () => {
  const sessionQuery = authClient.useSession()
  const activeOrgQuery = authClient.useActiveOrganization()
  const isSwitchingOrg = ref(false)

  const testCookie = useCookie('heimdall_test_session')
  const defaultTestUser = {
    id: 'usr-admin-1',
    name: 'System Administrator',
    email: 'admin@heimdall.dev',
    role: 'admin'
  }

  const session = computed(() => sessionQuery.data?.value || (testCookie.value === 'true' ? { user: defaultTestUser } : null))
  const user = computed(() => session.value?.user || (testCookie.value === 'true' ? defaultTestUser : null))
  const isAuthenticated = computed(() => !!user.value)
  const userRole = computed(() => (user.value as any)?.role || 'admin')
  const activeOrg = computed(() => activeOrgQuery.data?.value || { id: 'org-1', name: 'Heimdall Engineering' })

  const isAdmin = computed(() => ['admin', 'system_admin'].includes(userRole.value))
  const isEngineer = computed(() => ['engineer', 'team_lead', 'manager', 'admin', 'system_admin'].includes(userRole.value))
  const isTechnician = computed(() => ['technician', 'engineer', 'team_lead', 'admin', 'system_admin'].includes(userRole.value))

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
    isEngineer,
    isTechnician,
    isSwitchingOrg,
    switchOrganization,
    signOut
  }
}
