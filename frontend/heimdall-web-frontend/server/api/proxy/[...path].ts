import { getCookie, getHeader, proxyRequest, getQuery } from 'h3'
import { auth } from '../../utils/auth'

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path || ''
  const normalizedPath = path.startsWith('v1/') ? path : `v1/${path}`
  const query = getQuery(event)
  const queryString = Object.keys(query).length ? '?' + new URLSearchParams(query as any).toString() : ''
  const target = `http://localhost:5099/api/${normalizedPath}${queryString}`

  let sessionToken = getCookie(event, 'better-auth.session_token') || getHeader(event, 'authorization')?.replace(/^Bearer\s+/i, '')
  let activeOrgId = getHeader(event, 'x-organization-id') as string | undefined

  if (!activeOrgId && sessionToken) {
    try {
      const sessionData = await auth.api.getSession({ headers: event.headers })
      activeOrgId = (sessionData?.session as any)?.activeOrganizationId
    } catch {
      // Continue without active org
    }
  }

  return proxyRequest(event, target, {
    headers: {
      Authorization: sessionToken ? `Bearer ${sessionToken}` : undefined,
      'X-Organization-Id': activeOrgId || undefined
    }
  })
})