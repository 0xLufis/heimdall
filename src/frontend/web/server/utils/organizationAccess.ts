import type { H3Event } from 'h3'
import { createError } from 'h3'
import { and, eq } from 'drizzle-orm'
import { useDb } from './db'
import * as hbSchema from '../database/drizzle/schema'
import { requireUser, type AuthorizedUser } from './authz'
import { evaluateOrganizationAction, type OrganizationAction, type OrganizationActionContext, type OrganizationActor } from './organizationRules'

export * from './organizationRules'

/**
 * Database side of the OrganizationManagement policy (rules: ./organizationRules.ts). The BFF
 * organization routes resolve the caller, the target organization and the memberships here.
 */

/** The caller with the role and permission grants of their session; 401 when signed out. */
export async function requireOrganizationActor(event: H3Event): Promise<AuthorizedUser> {
  return requireUser(event)
}

/** Member role of the user in the organization, or null. */
export async function memberRoleOf(userId: string, organizationId: string): Promise<string | null> {
  const rows = await useDb()
    .select({ role: hbSchema.member.role })
    .from(hbSchema.member)
    .where(and(eq(hbSchema.member.userId, userId), eq(hbSchema.member.organizationId, organizationId)))
    .limit(1)
  return rows[0]?.role ?? null
}

/** 404 when the organization does not exist; returns its row. */
export async function requireOrganization(organizationId: string | undefined) {
  if (!organizationId) throw createError({ statusCode: 400, statusMessage: 'Organization ID is required.' })
  const rows = await useDb()
    .select({ id: hbSchema.organization.id, name: hbSchema.organization.name, slug: hbSchema.organization.slug })
    .from(hbSchema.organization)
    .where(eq(hbSchema.organization.id, organizationId))
    .limit(1)
  if (!rows[0]) throw createError({ statusCode: 404, statusMessage: `Organization '${organizationId}' was not found.` })
  return rows[0]
}

/** Throws 403 with the refusal reason when the action is not allowed. */
export function assertOrganizationAction(actor: OrganizationActor, action: OrganizationAction, context: OrganizationActionContext = {}) {
  const refusal = evaluateOrganizationAction(actor, action, context)
  if (refusal) throw createError({ statusCode: 403, statusMessage: `Forbidden: ${refusal}` })
}

