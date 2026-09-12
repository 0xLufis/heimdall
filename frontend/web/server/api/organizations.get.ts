import { useDb } from "../utils/db"
import { organization, member } from "../database/drizzle/schema"
import { eq, sql } from "drizzle-orm"

export default defineEventHandler(async () => {
  try {
    const db = useDb()
    const orgList = await db
      .select({
        id: organization.id,
        name: organization.name,
        slug: organization.slug,
        createdAt: organization.createdAt,
        memberCount: sql<number>`cast(count(${member.id}) as int)`
      })
      .from(organization)
      .leftJoin(member, eq(member.organizationId, organization.id))
      .groupBy(organization.id, organization.name, organization.slug, organization.createdAt)
    return { success: true, organizations: orgList }
  } catch (e: any) {
    console.error("Error fetching organizations via Drizzle:", e)
    return { success: false, organizations: [], error: e.message }
  }
})
