import { defineEventHandler } from 'h3'
import { useDb } from '../utils/db'
import * as hbSchema from '../database/drizzle/schema'
import { getAllKnownUserProfiles } from '../utils/authProfiles'

export default defineEventHandler(async (event) => {
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const data = await $fetch<{ success: boolean; users: any[] }>(`${backendBase}/api/v1/auth/users`, {
      headers: event.headers as any
    })
    if (data && data.success && data.users && data.users.length > 0) {
      return data
    }
  } catch (e: any) {}

  // Fallback to local Better-Auth PostgreSQL database
  try {
    const db = useDb()
    const dbUsers = await db
      .select({
        id: hbSchema.user.id,
        name: hbSchema.user.name,
        email: hbSchema.user.email,
        role: hbSchema.user.role,
        username: hbSchema.user.username,
        banned: hbSchema.user.banned,
        createdAt: hbSchema.user.createdAt
      })
      .from(hbSchema.user)

    if (dbUsers && dbUsers.length > 0) {
      return { success: true, users: dbUsers }
    }
  } catch (err: any) {}

  // Fallback to master system catalog ensuring no unprofiled users
  const fallbackProfiles = getAllKnownUserProfiles()
  return { success: true, users: fallbackProfiles }
})
