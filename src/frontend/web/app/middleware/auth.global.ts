import { authClient } from '~/utils/auth-client'

export default defineNuxtRouteMiddleware(async (to) => {
  const authSession = useState<{ authenticated: boolean; user?: any } | null>('auth_user_session', () => null)
  const testCookie = useCookie('heimdall_test_session')
  const isClientBypass = typeof window !== 'undefined' && window.location.search.includes('mock_auth=true')
  const reqCookie = import.meta.server ? (useRequestHeader('cookie') || '') : ''
  const hasTestSession = testCookie.value === 'true' || reqCookie.includes('heimdall_test_session=true') || to.query.mock_auth === 'true' || isClientBypass

  // 1. Test session bypass for E2E tests & mock auth
  if (hasTestSession) {
    authSession.value = { authenticated: true }
    if (to.path === '/auth/login' || to.path === '/auth/signup') {
      return
    }
    if (to.path.startsWith('/dashboard')) {
      return
    }
  }

  // 2. Server-side check from incoming HTTP cookie header
  if (import.meta.server) {
    if (reqCookie.includes('better-auth.session_token=')) {
      authSession.value = { authenticated: true }
    }
  }

  // 3. Client-side check: if not yet marked authenticated in useState, query Better-Auth getSession()
  if (import.meta.client && !authSession.value?.authenticated) {
    try {
      const res = await authClient.getSession()
      if (res?.data?.session) {
        authSession.value = { authenticated: true, user: res.data.user }
      }
    } catch {
      // Session verification failed or offline
    }
  }

  const isAuthenticated = authSession.value?.authenticated === true

  // If navigating to /dashboard or nested dashboard routes
  if (to.path.startsWith('/dashboard')) {
    if (!isAuthenticated) {
      return navigateTo('/auth/login')
    }
  }

  // If already authenticated and navigating to auth login/signup
  if (to.path === '/auth/login' || to.path === '/auth/signup') {
    if (isAuthenticated) {
      return navigateTo('/dashboard')
    }
  }
})
