import { canManageOrganizations, isOrganizationAdminRole } from '../../app/utils/roles'

/**
 * Who may do what to an organization - the "OrganizationManagement" policy (app/utils/roles.ts,
 * Program.cs, SecurityConstants.cs):
 *
 *   - The policy itself: Heimdall administrators and middle managers and up by role, anyone else
 *     through the explicit per-user grant `organizations.manage`.
 *   - Heimdall administrators (system_admin, heimdall_admin, admin) manage every organization.
 *   - Everyone else holding the policy creates organizations (becoming their owner) and manages the
 *     organizations they are a member of: rename, add / remove members, change member roles, invite.
 *     Deleting requires being an owner of that organization.
 *   - Owner rights cannot be taken by the back door: only an owner (or a Heimdall administrator)
 *     grants the `owner` member role or changes / removes an owner, and nobody but a Heimdall
 *     administrator changes their own member role.
 *
 * Better-Auth's organization plugin only lets an organization's own owners / admins act, so these
 * operations are BFF routes that enforce this and write the auth schema directly.
 */

export const ORGANIZATION_MEMBER_ROLES = ['owner', 'admin', 'member'] as const
export type OrganizationMemberRole = typeof ORGANIZATION_MEMBER_ROLES[number]

export type OrganizationAction = 'create' | 'update' | 'delete' | 'manageMembers'

export interface OrganizationActor {
  id: string
  role: unknown
  permissions?: unknown
}

export interface OrganizationActionContext {
  /** The caller's member role in the target organization; null / undefined when not a member. */
  actorMemberRole?: string | null
  /** Member role being granted (add member, invite, role change). */
  grantedRole?: string | null
  /** Current member role of the member being changed or removed. */
  targetMemberRole?: string | null
  /** True when the member being changed or removed is the caller. */
  targetIsActor?: boolean
}

export function isOrganizationMemberRole(role: unknown): role is OrganizationMemberRole {
  return typeof role === 'string' && (ORGANIZATION_MEMBER_ROLES as readonly string[]).includes(role)
}

/** Returns the reason the action must be refused, or `null` when it is allowed. */
export function evaluateOrganizationAction(
  actor: OrganizationActor,
  action: OrganizationAction,
  context: OrganizationActionContext = {}
): string | null {
  if (!canManageOrganizations(actor.role, actor.permissions)) {
    return 'Managing organizations requires the OrganizationManagement right (Heimdall administrators, middle managers and up, or an explicit grant).'
  }
  if (context.grantedRole != null && !isOrganizationMemberRole(context.grantedRole)) {
    return `Unknown member role '${context.grantedRole}'. Use one of: ${ORGANIZATION_MEMBER_ROLES.join(', ')}.`
  }

  const heimdallAdmin = isOrganizationAdminRole(actor.role)
  if (heimdallAdmin || action === 'create') return null

  const actorRole = context.actorMemberRole ?? null
  if (!actorRole) return 'You can only manage organizations you are a member of.'
  const actorIsOwner = actorRole === 'owner'

  if (action === 'delete') {
    return actorIsOwner ? null : 'Only an owner of this organization can delete it.'
  }

  if (action === 'manageMembers') {
    if (context.targetIsActor && context.grantedRole != null && context.grantedRole !== context.targetMemberRole) {
      return 'You cannot change your own member role.'
    }
    if (!actorIsOwner && (context.grantedRole === 'owner' || context.targetMemberRole === 'owner')) {
      return 'Only an owner of this organization can grant the owner role or change an owner.'
    }
  }
  return null
}

/** Lower-case slug from a name ("Line 09 – Press Shop" -> "line-09-press-shop"). */
export function slugify(value: string): string {
  return value
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64)
}
