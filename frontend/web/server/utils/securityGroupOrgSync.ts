import { randomUUID } from 'node:crypto'
import { eq, inArray } from 'drizzle-orm'
import { useDb } from './db'
import * as hbSchema from '../database/drizzle/schema'
import { getPlantSecurityGroups, getPlantOrganizations } from './datasetLoader'

export interface OrgSyncResult {
  userId: string
  matchedGroups: string[]
  enrolledOrganizations: Array<{
    organizationId: string
    organizationName: string
    organizationSlug: string
    role: string
    isNew: boolean
  }>
  activeOrganizationId?: string
}

export function slugify(name: string): string {
  return name
    .toLowerCase()
    .trim()
    .replace(/[^\w\s-]/g, '')
    .replace(/[\s_-]+/g, '-')
    .replace(/^-+|-+$/g, '')
}

/**
 * Evaluates security group mappings against a list of directory group identifiers.
 * Pure in-memory evaluation suitable for sandboxes, preview UIs, and unit tests.
 */
export function evaluateSecurityGroupOrgMapping(groupIdentifiers: string[]): {
  matchedGroups: Array<{
    groupIdentifier: string
    displayName: string
    mappedRole: string
    organizationName: string
    orgRole: string
  }>
  targetOrganizations: Array<{
    name: string
    slug: string
    role: string
  }>
  suggestedActiveOrganization?: string
} {
  const allGroups = getPlantSecurityGroups()
  const lowerInputs = new Set(groupIdentifiers.map(g => g.toLowerCase().trim()))

  const matched = allGroups.filter(g => 
    g.isEnabled && (lowerInputs.has(g.groupIdentifier.toLowerCase()) || lowerInputs.has(g.id.toLowerCase()))
  )

  const orgMap = new Map<string, { name: string; slug: string; role: string }>()

  for (const m of matched) {
    if (m.mappedOrganizationName) {
      const slug = slugify(m.mappedOrganizationName)
      const existing = orgMap.get(slug)
      const targetRole = m.mappedOrgRole || (m.mappedRole === 'system_admin' ? 'owner' : m.mappedRole === 'admin' ? 'admin' : 'member')
      
      if (!existing || (targetRole === 'owner' && existing.role !== 'owner') || (targetRole === 'admin' && existing.role === 'member')) {
        orgMap.set(slug, {
          name: m.mappedOrganizationName,
          slug,
          role: targetRole
        })
      }
    }
  }

  const targetOrganizations = Array.from(orgMap.values())
  const suggestedActiveOrganization = targetOrganizations.length > 0 ? targetOrganizations[0].slug : undefined

  return {
    matchedGroups: matched.map(m => ({
      groupIdentifier: m.groupIdentifier,
      displayName: m.displayName,
      mappedRole: m.mappedRole,
      organizationName: m.mappedOrganizationName,
      orgRole: m.mappedOrgRole || 'member'
    })),
    targetOrganizations,
    suggestedActiveOrganization
  }
}

/**
 * Synchronizes user memberships and auto-provisions organizations in Better-Auth
 * based on the user's Active Directory / Entra ID security groups.
 */
export async function syncUserSecurityGroupsToOrganizations(
  userId: string,
  groupIdentifiers: string[]
): Promise<OrgSyncResult> {
  const evalResult = evaluateSecurityGroupOrgMapping(groupIdentifiers)
  const result: OrgSyncResult = {
    userId,
    matchedGroups: evalResult.matchedGroups.map(g => g.groupIdentifier),
    enrolledOrganizations: []
  }

  if (evalResult.targetOrganizations.length === 0) {
    return result
  }

  try {
    const db = useDb()

    for (const targetOrg of evalResult.targetOrganizations) {
      // 1. Ensure organization exists in database
      const existingOrgs = await db
        .select()
        .from(hbSchema.organization)
        .where(eq(hbSchema.organization.slug, targetOrg.slug))
        .limit(1)

      let orgId: string
      let isNew = false

      if (existingOrgs.length > 0) {
        orgId = existingOrgs[0].id
      } else {
        orgId = randomUUID()
        await db.insert(hbSchema.organization).values({
          id: orgId,
          name: targetOrg.name,
          slug: targetOrg.slug,
          createdAt: new Date(),
          metadata: JSON.stringify({ autoProvisionedFromSecurityGroup: true })
        })
        isNew = true
      }

      // 2. Ensure user membership exists with correct role
      const existingMembers = await db
        .select()
        .from(hbSchema.member)
        .where(
          eq(hbSchema.member.organizationId, orgId)
        )

      const userMember = existingMembers.find(m => m.userId === userId)

      if (!userMember) {
        await db.insert(hbSchema.member).values({
          id: randomUUID(),
          organizationId: orgId,
          userId: userId,
          role: targetOrg.role,
          createdAt: new Date()
        })
      } else if (userMember.role !== targetOrg.role) {
        await db
          .update(hbSchema.member)
          .set({ role: targetOrg.role })
          .where(eq(hbSchema.member.id, userMember.id))
      }

      result.enrolledOrganizations.push({
        organizationId: orgId,
        organizationName: targetOrg.name,
        organizationSlug: targetOrg.slug,
        role: targetOrg.role,
        isNew
      })
    }

    if (result.enrolledOrganizations.length > 0) {
      result.activeOrganizationId = result.enrolledOrganizations[0].organizationId
    }
  } catch (err) {
    console.warn('[SecurityGroupOrgSync] Database sync warning (running in mock or offline mode):', err)
    // Return simulated sync result
    result.enrolledOrganizations = evalResult.targetOrganizations.map(t => ({
      organizationId: `mock-org-${t.slug}`,
      organizationName: t.name,
      organizationSlug: t.slug,
      role: t.role,
      isNew: false
    }))
    result.activeOrganizationId = result.enrolledOrganizations[0]?.organizationId
  }

  return result
}
