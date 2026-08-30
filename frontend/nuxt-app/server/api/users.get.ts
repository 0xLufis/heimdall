import { useDb } from "../utils/db"
import { user } from "../database/drizzle/schema"

export default defineEventHandler(async () => {
  try {
    const db = useDb()
    const userList = await db.select().from(user)
    return { success: true, users: userList }
  } catch (e: any) {
    console.error("Error fetching users via Drizzle:", e)
    return { success: false, users: [], error: e.message }
  }
})
