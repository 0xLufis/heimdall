import { useDb } from "../utils/db"
import { organization, member } from "../database/drizzle/schema"
import { eq, sql } from "drizzle-orm"
import { getPlantOrganizations, getPlantActiveDirectoryOUs } from "../utils/datasetLoader"

export default defineEventHandler(async () => {
  let ous: any[] = []
  try {
    ous = getPlantActiveDirectoryOUs()
  } catch {}

  function findMatchingOu(name: string, slug: string) {
    if (!ous || ous.length === 0) return undefined
    const cleanSlug = slug.toLowerCase().replace('org-', '').replace(/[^a-z0-9]/g, '')
    const cleanName = name.toLowerCase().replace(/[^a-z0-9]/g, '')
    return ous.find(ou => {
      const ouNameClean = ou.name.toLowerCase().replace(/[^a-z0-9]/g, '')
      const ouPathClean = ou.ouPath.toLowerCase().replace(/[^a-z0-9]/g, '')
      return ouNameClean.includes(cleanSlug) || cleanName.includes(ouNameClean) || ouPathClean.includes(cleanSlug)
    })
  }

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

    if (orgList && orgList.length > 0) {
      const enriched = orgList.map(o => {
        const matched = findMatchingOu(o.name, o.slug)
        return {
          ...o,
          ouPath: matched?.ouPath,
          vlanId: matched?.vlanId,
          vlanName: matched?.vlanName
        }
      })
      return { success: true, organizations: enriched }
    }
  } catch (e: any) {
    console.warn("Could not query organizations via Drizzle, using plant dataset fallback:", e?.message)
  }

  // Graceful fallback to canonical plant dataset (OU-based plant hierarchy)
  try {
    const plantOrgs = getPlantOrganizations()
    if (plantOrgs && plantOrgs.length > 0) {
      const fallbackList = plantOrgs.map(po => {
        const matched = findMatchingOu(po.name, po.slug)
        return {
          id: po.id,
          name: po.name,
          slug: po.slug,
          description: po.description,
          createdAt: new Date().toISOString(),
          memberCount: matched ? matched.candidateHostnames?.length || matched.hostCount || 4 : 4,
          ouPath: matched?.ouPath,
          vlanId: matched?.vlanId,
          vlanName: matched?.vlanName
        }
      })
      return { success: true, organizations: fallbackList }
    }
  } catch (err) {
    console.error("Error reading plant dataset organizations:", err)
  }

  return { success: true, organizations: [] }
})
