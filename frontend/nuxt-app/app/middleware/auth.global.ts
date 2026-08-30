import { authClient } from '~/utils/auth-client'

export default defineNuxtRouteMiddleware(async (to) => {
  // Check test session bypass across SSR and client
  const testCookie = useCookie('heimdall_test_session')
  const isClientBypass = typeof window !== 'undefined' && (
    document.cookie.includes('heimdall_test_session=true') || 
    window.location.search.includes('mock_auth=true')
  )

  if (testCookie.value === 'true' || to.query.mock_auth === 'true' || isClientBypass) {
    return
  }

  // If navigating to /dashboard or nested dashboard routes
  if (to.path.startsWith('/dashboard')) {
    const sessionCookie = useCookie('better-auth.session_token')
    if (!sessionCookie.value) {
      if (typeof window !== 'undefined') {
        try {
          const session = await authClient.getSession()
          if (!session || !session.data?.user) {
            return navigateTo('/auth/login')
          }
        } catch {
          // Dev fallback
        }
      }
    }
  }

  // If already authenticated and navigating to auth login/signup
  if (to.path === '/auth/login' || to.path === '/auth/signup') {
    const sessionCookie = useCookie('better-auth.session_token')
    if (sessionCookie.value) {
      return navigateTo('/dashboard')
    }
  }
})
