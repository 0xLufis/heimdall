import { getCookie, getHeader, proxyRequest, getQuery } from 'h3'
import { auth } from '../../utils/auth'

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path || ''
  const normalizedPath = path.startsWith('v1/') ? path : `v1/${path}`
  const query = getQuery(event)
  const queryParams = new URLSearchParams(query as Record<string, string>).toString()
  const queryString = queryParams ? `?${queryParams}` : ''
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  const target = `${backendBase}/api/${normalizedPath}${queryString}`

  let rawToken = getCookie(event, 'better-auth.session_token') || getHeader(event, 'authorization')?.replace(/^Bearer\s+/i, '')
  let sessionToken = rawToken ? rawToken.trim().split('.')[0] : undefined
  let activeOrgId = getHeader(event, 'x-organization-id') as string | undefined

  if (rawToken) {
    try {
      const sessionData = await auth.api.getSession({ headers: event.headers })
      if (!activeOrgId) {
        activeOrgId = (sessionData?.session as any)?.activeOrganizationId
      }
      if (sessionData?.session?.token) {
        sessionToken = sessionData.session.token
      }
    } catch {
      // Continue without active org
    }
  }

  try {
    return await proxyRequest(event, target, {
      headers: {
        Authorization: sessionToken ? `Bearer ${sessionToken}` : undefined,
        'X-Organization-Id': activeOrgId || undefined
      }
    })
  } catch (err: any) {
    console.warn(`[Proxy] Target ${target} unreachable or connection reset:`, err?.message || err)
    setResponseStatus(event, 502)
    return {
      statusCode: 502,
      error: 'Bad Gateway',
      message: `Heimdall backend at ${target} is currently unavailable.`,
      timestamp: new Date().toISOString()
    }
  }
})