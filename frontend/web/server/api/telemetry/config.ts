import { defineEventHandler, readBody, getMethod } from 'h3'

export default defineEventHandler(async (event) => {
  const method = getMethod(event)
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'

  if (method === 'GET') {
    try {
      const res = await $fetch<any>(`${backendBase}/api/v1/SystemSettings/TelemetryConfigMaster`, {
        headers: event.headers as any
      })
      if (res && res.valueJson) {
        try {
          return JSON.parse(res.valueJson)
        } catch {
          return null
        }
      }
      return null
    } catch {
      return null
    }
  }

  if (method === 'PUT' || method === 'POST') {
    try {
      const body = await readBody(event)
      const valueJson = typeof body === 'string' ? body : JSON.stringify(body)
      const res = await $fetch<any>(`${backendBase}/api/v1/SystemSettings/TelemetryConfigMaster`, {
        method: 'PUT',
        headers: event.headers as any,
        body: {
          valueJson
        }
      })
      return { success: true, res }
    } catch (e: any) {
      return { success: false, error: e?.message || 'Failed to save telemetry config' }
    }
  }

  return { error: 'Method not allowed' }
})
