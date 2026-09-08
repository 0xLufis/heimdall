import { defineEventHandler, readBody } from 'h3'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const body = await readBody(event)
    const { clientPcId, config, signature } = body

    if (!clientPcId) {
      return { success: false, error: 'clientPcId is required' }
    }

    const res = await $fetch<any>(`${backendBase}/api/v1/AgentCommand/${clientPcId}/update-config`, {
      method: 'POST',
      headers: event.headers as any,
      body: {
        config: config || {},
        signature: signature || ''
      }
    })

    return { success: true, res }
  } catch (e: any) {
    return { success: false, error: e?.message || 'Failed to dispatch agent config command' }
  }
})
