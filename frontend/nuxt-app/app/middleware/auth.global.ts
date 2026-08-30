import { authClient } from '~/utils/auth-client'

export default defineNuxtRouteMiddleware(async (to) => {
  // If navigating to /dashboard or nested dashboard routes
  if (to.path.startsWith('/dashboard')) {
    if (typeof window !== 'undefined') {
      try {
        const session = await authClient.getSession()
        if (!session || !session.data?.user) {
          return navigateTo('/auth/login')
        }
      } catch {
        // Fallback in dev
      }
    }
  }

  // If already authenticated and navigating to auth login/signup
  if (to.path === '/auth/login' || to.path === '/auth/signup') {
    if (typeof window !== 'undefined') {
      try {
        const session = await authClient.getSession()
        if (session && session.data?.user) {
          return navigateTo('/dashboard')
        }
      } catch {
        // Continue
      }
    }
  }
})
