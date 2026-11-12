import type { H3Event } from 'h3'
import { createError } from 'h3'
import { getAuthenticatedUser } from './authHelper'
import { DEFAULT_ROLE, normalizeRole, roleSatisfies, splitPermissions, type PolicyName } from '../../app/utils/roles'

/**
 * Role-based authorization for BFF routes that act on Nitro's own data (the `auth` schema and the
 * in-process stores) and therefore never pass through a backend `[Authorize(Policy = ...)]`.
 *
 * The caller's role comes from the session only (`getAuthenticatedUser`: Better-Auth session, or a
 * development SSO-simulation session) - never from the request body, query, a client cookie or the
 * backend's `auth/me` (which answers as a full-privilege dev administrator for unknown tokens in
 * Development). It is checked against the same role sets the backend policies use
 * (`app/utils/roles.ts`), so "who may do this" has one answer on both sides.
 *
 * Known difference: the backend can additionally promote `heimdall_admin` / `admin` to IT
 * administrators through the AdminRoleDelegation system setting. That setting lives in the backend
 * schema, so BFF-only routes guarded with `ItAdministration` do not honour it.
 */

export interface AuthorizedUser {
  id: string
  name?: string
  email?: string
  role: string
  /** Explicit per-user permission grants (`auth.user.permissions`), e.g. `organizations.manage`. */
  permissions: string[]
}

/** Resolves the signed-in caller or throws 401. */
export async function requireUser(event: H3Event): Promise<AuthorizedUser> {
  const user = await getAuthenticatedUser(event, true)
  return {
    id: String(user.id),
    name: user.name,
    email: user.email,
    role: normalizeRole(user.role) || DEFAULT_ROLE,
    permissions: splitPermissions((user as any).permissions)
  }
}

/**
 * 401 unless signed in; 403 unless the caller's role is accepted by the named backend policy.
 * Role-only: the OrganizationManagement policy is also met by an explicit grant, so organization
 * routes use `canManageOrganizations` (server/utils/organizationAccess.ts) instead.
 */
export async function requirePolicy(event: H3Event, policy: PolicyName): Promise<AuthorizedUser> {
  const user = await requireUser(event)
  if (roleSatisfies(user.role, policy)) return user
  throw createError({
    statusCode: 403,
    statusMessage: `Forbidden: this action requires the ${policy} policy.`
  })
}
