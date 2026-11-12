import { APIError, createAuthMiddleware, getSessionFromCtx } from 'better-auth/api'
import { isKnownRole, normalizeRole, roleRank, splitRoles } from '../../app/utils/roles'

/**
 * Privilege-escalation guard for Better-Auth's admin endpoints (`/api/auth/admin/*`).
 *
 * Better-Auth only asks "is the caller a user administrator?". Every user administrator
 * (system_admin, heimdall_admin, admin, engineering_admin) could therefore set anyone's role -
 * including their own - to `system_admin`, reset a system administrator's password, or read a more
 * privileged user's session tokens. The rules below close that:
 *
 *   1. Nobody changes their own role.
 *   2. A role can only be granted by a caller of at least that role's rank, and it must be one
 *      single role the backend knows.
 *   3. Any action on another user requires a rank at least as high as that user's.
 *
 * Ranks are defined in `app/utils/roles.ts` (system_admin > platform admins > IT/engineering
 * administration > everyone else).
 */

export interface AdminActionInput {
  /** Endpoint path, e.g. `/admin/set-role`. */
  path: string
  actorId: string
  actorRole: unknown
  /** User the action targets, when it targets one. */
  targetId?: string | null
  targetRole?: unknown
  /** Role(s) the request wants to assign, when it assigns any. */
  requestedRole?: unknown
}

/** Returns the reason the action must be refused, or `null` when it is allowed. */
export function evaluateAdminAction(input: AdminActionInput): string | null {
  const actorRank = roleRank(input.actorRole)

  if (input.requestedRole !== undefined) {
    const requested = splitRoles(input.requestedRole)
    if (requested.length !== 1 || !isKnownRole(requested[0])) {
      return 'A user must be assigned exactly one known Heimdall role.'
    }
    if (input.targetId && input.targetId === input.actorId) {
      return 'You cannot change your own role.'
    }
    if (roleRank(requested[0]) > actorRank) {
      return `Your role may not grant the '${requested[0]}' role.`
    }
  }

  if (input.targetId && input.targetId !== input.actorId && roleRank(input.targetRole) > actorRank) {
    return 'You cannot administer a user with a higher privilege level than your own.'
  }

  return null
}

function bodyOf(ctx: any): Record<string, any> {
  return ctx?.body && typeof ctx.body === 'object' ? ctx.body : {}
}

/** `hooks.before` middleware enforcing {@link evaluateAdminAction} on the admin endpoints. */
export const adminEscalationGuard = createAuthMiddleware(async (ctx: any) => {
  const path: string = ctx?.path || ''
  if (!path.startsWith('/admin/')) return

  const session = await getSessionFromCtx(ctx)
  // Unauthenticated calls are rejected by the endpoint itself.
  if (!session?.user) return

  const body = bodyOf(ctx)
  let targetId: string | null = typeof body.userId === 'string' ? body.userId : null
  let targetRole: unknown

  if (path === '/admin/revoke-user-session' && typeof body.sessionToken === 'string') {
    const found = await ctx.context.internalAdapter.findSession(body.sessionToken)
    targetId = found?.user?.id ?? null
    targetRole = found?.user?.role
  } else if (targetId) {
    const target = await ctx.context.internalAdapter.findUserById(targetId)
    targetRole = (target as any)?.role
  }

  let requestedRole: unknown
  if (path === '/admin/set-role') requestedRole = body.role
  else if (path === '/admin/create-user') requestedRole = body.role ?? body.data?.role
  else if (path === '/admin/update-user' && body.data && Object.prototype.hasOwnProperty.call(body.data, 'role')) requestedRole = body.data.role

  const refusal = evaluateAdminAction({
    path,
    actorId: session.user.id,
    actorRole: normalizeRole((session.user as any).role),
    targetId,
    targetRole,
    requestedRole
  })
  if (refusal) {
    throw new APIError('FORBIDDEN', { message: refusal })
  }
})
