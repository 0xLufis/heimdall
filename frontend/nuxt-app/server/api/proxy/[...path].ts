import { getCookie, proxyRequest, getQuery } from "h3"

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path
  const query = getQuery(event)
  const queryString = Object.keys(query).length ? '?' + new URLSearchParams(query as any).toString() : ''
  const target = `http://localhost:5099/api/${path}${queryString}`

  // Forward the session token if it exists in cookies
  const sessionToken = getCookie(event, "better-auth.session_token")
  
  return proxyRequest(event, target, {
    headers: {
      Authorization: sessionToken ? `Bearer ${sessionToken}` : undefined
    }
  })
})