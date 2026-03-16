import { getHeader, proxyRequest } from "h3"

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path
  const target = `http://localhost:5099/api/${path}` // Using C# backend port 5099

  // Forward the session token if it exists in cookies
  const sessionToken = getCookie(event, "better-auth.session_token")
  
  return proxyRequest(event, target, {
    headers: {
      Authorization: sessionToken ? `Bearer ${sessionToken}` : undefined
    }
  })
})
