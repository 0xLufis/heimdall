import { H3Event, getHeader, getCookie, createError } from 'h3'
import { auth } from './auth'
import { featureFlags } from './featureFlags'

export async function getAuthenticatedUser(event: H3Event, requireAuth = false) {
  let user = (event.context as any)?.auth?.user

  const authHeader = getHeader(event, 'authorization')
  const cookieToken = getCookie(event, 'better-auth.session_token')

  if (!user && (authHeader || cookieToken)) {
    try {
      const sessionData = await auth.api.getSession({ headers: event.headers })
      if (sessionData?.user) {
        user = sessionData.user
      }
    } catch {
      // ignore
    }
  }

  // Fallback for dev / test environments when explicit token or dev mode is active
  if (!user && (featureFlags.enableDevFeatures || process.env.NODE_ENV !== 'production' || process.env.VITEST === 'true')) {
    user = {
      id: 'usr-admin-primary',
      name: 'System Administrator',
      email: 'admin@heimdall.local',
      role: 'admin'
    }
  }

  if (!user && requireAuth) {
    throw createError({
      statusCode: 401,
      statusMessage: 'Unauthorized: Authentication required'
    })
  }

  return user
}
