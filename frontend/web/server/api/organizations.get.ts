import { defineEventHandler } from 'h3'
import { getPlantOrganizations, getPlantActiveDirectoryOUs } from "../utils/datasetLoader"


export interface EnrichedOrganization {
  id: string
  name: string
  slug: string
  description?: string
  createdAt?: string
  memberCount?: number
  ouPath?: string
  vlanId?: string
  vlanName?: string
  [key: string]: any
}

export interface OrganizationsResponse {
  success: boolean
  organizations: EnrichedOrganization[]
}

export default defineEventHandler(async (event): Promise<OrganizationsResponse> => {
  let ous: any[] = []
  try {
    ous = getPlantActiveDirectoryOUs()
  } catch { }

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

  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const res = await $fetch<any>(`${backendBase}/api/v1/organization`, {
      headers: event.headers as any
    })
    const orgList = res?.organizations || res
    if (Array.isArray(orgList) && orgList.length > 0) {
      const enriched = orgList.map((o: any) => {
        const matched = findMatchingOu(o.name, o.slug || o.id || '')
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
    // Graceful fallback if backend is unavailable or starting up
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
