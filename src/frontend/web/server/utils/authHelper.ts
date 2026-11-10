import { H3Event, getHeader, getCookie, createError } from 'h3'
import { auth } from './auth'
import { isExplicitDevMode } from './terminalAuth'

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

  // Synthetic admin fallback ONLY under an explicit opt-in (HEIMDALL_ENABLE_DEV=true, set by
  // .env.dev) or Vitest. Previously any NODE_ENV other than "production" (including an unset
  // NODE_ENV) silently granted anonymous callers admin rights.
  if (!user && isExplicitDevMode()) {
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
