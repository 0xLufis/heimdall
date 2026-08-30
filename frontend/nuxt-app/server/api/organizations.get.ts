import { useDb } from "../utils/db"
import { organization } from "../database/drizzle/schema"

export default defineEventHandler(async () => {
  try {
    const db = useDb()
    const orgList = await db.select().from(organization)
    return { success: true, organizations: orgList }
  } catch (e: any) {
    console.error("Error fetching organizations via Drizzle:", e)
    return { success: false, organizations: [], error: e.message }
  }
})
