import { proxyRequest, getQuery } from 'h3'

export default defineEventHandler(async (event) => {
  const path = event.context.params?.path || ''
  const query = getQuery(event)
  const queryParams = new URLSearchParams(query as Record<string, string>).toString()
  const queryString = queryParams ? `?${queryParams}` : ''
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  const target = `${backendBase}/hubs/${path}${queryString}`

  return await proxyRequest(event, target)
})
