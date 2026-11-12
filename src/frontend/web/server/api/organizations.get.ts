import { defineEventHandler, createError } from 'h3'
import { count, eq, inArray } from 'drizzle-orm'
import { useDb } from '../utils/db'
import * as hbSchema from '../database/drizzle/schema'
import { getPlantActiveDirectoryOUs } from '../utils/datasetLoader'
import { requireUser } from '../utils/authz'
import { canManageOrganizations, isOrganizationAdminRole, isPlatformAdminRole } from '../../app/utils/roles'
import { evaluateOrganizationAction } from '../utils/organizationRules'


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
  /** The caller may create organizations (OrganizationManagement policy). */
  canCreate?: boolean
  organizations: EnrichedOrganization[]
}

/**
 * Organization directory with real member counts, read from Better-Auth's `auth.organization`.
 *
 * Requires a signed-in caller. Platform administrators (the backend's SystemAdministration policy)
 * see every organization; everyone else only the organizations they are a member of. Each entry
 * says whether the caller may manage / delete it (OrganizationManagement policy). It used to be
 * anonymous, and when the backend was unreachable it answered with the demo plant dataset and an
 * invented member count.
 */
export default defineEventHandler(async (event): Promise<OrganizationsResponse> => {
  const user = await requireUser(event)

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

  try {
    const db = useDb()

    const canCreate = canManageOrganizations(user.role, user.permissions)
    const memberships = await db
      .select({ organizationId: hbSchema.member.organizationId, role: hbSchema.member.role })
      .from(hbSchema.member)
      .where(eq(hbSchema.member.userId, user.id))
    const myRole = new Map(memberships.map(m => [m.organizationId, m.role]))

    let visibleIds: string[] | null = null
    if (!isPlatformAdminRole(user.role) && !isOrganizationAdminRole(user.role)) {
      visibleIds = [...myRole.keys()]
      if (visibleIds.length === 0) return { success: true, canCreate, organizations: [] }
    }

    const orgQuery = db
      .select({
        id: hbSchema.organization.id,
        name: hbSchema.organization.name,
        slug: hbSchema.organization.slug,
        createdAt: hbSchema.organization.createdAt
      })
      .from(hbSchema.organization)
    const [orgs, memberCounts] = await Promise.all([
      visibleIds ? orgQuery.where(inArray(hbSchema.organization.id, visibleIds)) : orgQuery,
      db
        .select({ organizationId: hbSchema.member.organizationId, members: count() })
        .from(hbSchema.member)
        .groupBy(hbSchema.member.organizationId)
    ])

    const countByOrg = new Map(memberCounts.map(m => [m.organizationId, Number(m.members)]))
    const organizations = orgs
      .map(o => {
        const slug = o.slug || o.id
        const matched = findMatchingOu(o.name, slug)
        return {
          id: o.id,
          name: o.name,
          slug,
          createdAt: o.createdAt ? new Date(o.createdAt).toISOString() : undefined,
          memberCount: countByOrg.get(o.id) ?? 0,
          // What the caller may do here (same rules as the /api/organizations/* mutation routes).
          canManage: evaluateOrganizationAction(user, 'update', { actorMemberRole: myRole.get(o.id) }) === null,
          canDelete: evaluateOrganizationAction(user, 'delete', { actorMemberRole: myRole.get(o.id) }) === null,
          ouPath: matched?.ouPath,
          vlanId: matched?.vlanId,
          vlanName: matched?.vlanName
        }
      })
      .sort((a, b) => a.name.localeCompare(b.name))

    return { success: true, canCreate, organizations }
  } catch (err: any) {
    console.warn('[organizations] Failed to read organizations:', err?.message || err)
    throw createError({ statusCode: 503, statusMessage: 'The organization directory is currently unavailable.' })
  }
})
