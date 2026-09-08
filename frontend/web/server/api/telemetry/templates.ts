import { defineEventHandler, readBody, getMethod } from 'h3'

export default defineEventHandler(async (event) => {
  const method = getMethod(event)
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'

  if (method === 'GET') {
    try {
      const res = await $fetch<any>(`${backendBase}/api/v1/SystemSettings/TelemetryTemplates`, {
        headers: event.headers as any
      })
      if (res && res.valueJson) {
        try {
          const parsed = JSON.parse(res.valueJson)
          return parsed
        } catch {
          return []
        }
      }
      return []
    } catch {
      return []
    }
  }

  if (method === 'PUT' || method === 'POST') {
    try {
      const body = await readBody(event)
      const valueJson = typeof body === 'string' ? body : JSON.stringify(body)
      const res = await $fetch<any>(`${backendBase}/api/v1/SystemSettings/TelemetryTemplates`, {
        method: 'PUT',
        headers: event.headers as any,
        body: {
          valueJson
        }
      })
      return { success: true, res }
    } catch (e: any) {
      return { success: false, error: e?.message || 'Failed to save telemetry templates' }
    }
  }

  return { error: 'Method not allowed' }
})
