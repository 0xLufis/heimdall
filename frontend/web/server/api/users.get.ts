import { defineEventHandler } from 'h3'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const data = await $fetch<{ success: boolean; users: any[] }>(`${backendBase}/api/v1/auth/users`, {
      headers: event.headers as any
    })
    return data
  } catch (e: any) {
    return { success: false, users: [], error: e?.message || 'Backend service unavailable' }
  }
})
