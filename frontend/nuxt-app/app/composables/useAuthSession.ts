import { computed, ref } from 'vue'
import { authClient } from '~/utils/auth-client'

export const useAuthSession = () => {
  const sessionQuery = authClient.useSession()
  const activeOrgQuery = authClient.useActiveOrganization()
  const isSwitchingOrg = ref(false)

  const session = computed(() => sessionQuery.data?.value)
  const user = computed(() => session.value?.user)
  const isAuthenticated = computed(() => !!session.value?.user)
  const userRole = computed(() => (user.value as any)?.role || 'user')
  const activeOrg = computed(() => activeOrgQuery.data?.value)

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
