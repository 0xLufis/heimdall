import { defineEventHandler, getRouterParam, createError } from 'h3'
import { eq } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import { member, user } from '../../../database/drizzle/schema'
import { requireUser } from '../../../utils/authz'
import { isPlatformAdminRole } from '../../../../app/utils/roles'

/**
 * Member roster of one organization.
 *
 * Only platform administrators and members of that organization may read it. It used to be
 * anonymous (any caller could list names and e-mail addresses of any tenant) and returned a
 * made-up administrator when the query failed.
 */
export default defineEventHandler(async (event) => {
  const caller = await requireUser(event)

  const orgId = getRouterParam(event, 'id')
  if (!orgId) {
    throw createError({ statusCode: 400, statusMessage: 'Organization ID is required.' })
  }

  let membersList: Array<{
    id: string
    role: string
    createdAt: Date
    userId: string
    name: string
    email: string
    username: string | null
  }>
  try {
    const db = useDb()
    membersList = await db
      .select({
        id: member.id,
        role: member.role,
        createdAt: member.createdAt,
        userId: user.id,
        name: user.name,
        email: user.email,
        username: user.username
      })
      .from(member)
      .innerJoin(user, eq(member.userId, user.id))
      .where(eq(member.organizationId, orgId))
  } catch (error: any) {
    console.warn(`Failed to fetch members for org ${orgId}:`, error?.message)
    throw createError({ statusCode: 503, statusMessage: 'The organization roster is currently unavailable.' })
  }

  if (!isPlatformAdminRole(caller.role) && !membersList.some(m => m.userId === caller.id)) {
    throw createError({ statusCode: 403, statusMessage: 'Forbidden: you are not a member of this organization.' })
  }

  return {
    success: true,
    members: membersList.map(m => ({
      id: m.id,
      role: m.role,
      createdAt: m.createdAt,
      user: {
        id: m.userId,
        name: m.name,
        email: m.email,
        username: m.username
      }
    }))
  }
})
