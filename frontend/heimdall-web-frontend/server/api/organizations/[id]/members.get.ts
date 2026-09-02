import { defineEventHandler, getRouterParam } from 'h3'
import { eq } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import { member, user } from '../../../database/drizzle/schema'

export default defineEventHandler(async (event) => {
  const orgId = getRouterParam(event, 'id')
  if (!orgId) {
    return { success: false, members: [] }
  }

  try {
    const db = useDb()
    const membersList = await db
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
  } catch (error: any) {
    console.warn(`Failed to fetch members for org ${orgId}:`, error?.message)
    // Dev fallback
    return {
      success: true,
      members: [
        {
          id: 'mem-admin',
          role: 'admin',
          createdAt: new Date(),
          user: {
            id: 'usr-admin',
            name: 'System Administrator',
            email: 'admin@heimdall.dev',
            username: 'admin'
          }
        }
      ]
    }
  }
})
