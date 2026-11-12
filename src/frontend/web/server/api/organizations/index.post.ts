import { defineEventHandler, readBody, createError, setResponseStatus } from 'h3'
import { randomUUID } from 'node:crypto'
import { eq } from 'drizzle-orm'
import { useDb } from '../../utils/db'
import * as hbSchema from '../../database/drizzle/schema'
import { assertOrganizationAction, requireOrganizationActor, slugify } from '../../utils/organizationAccess'
import { evictBackendSessionCacheForUser } from '../../utils/sessionSync'

/**
 * Creates an organization; the caller becomes its owner. Requires the OrganizationManagement right
 * (Heimdall administrators, middle managers and up, or the explicit `organizations.manage` grant).
 */
export default defineEventHandler(async (event) => {
  const caller = await requireOrganizationActor(event)
  assertOrganizationAction(caller, 'create')

  const body = await readBody(event).catch(() => ({}))
  const name = typeof body?.name === 'string' ? body.name.trim() : ''
  if (!name) throw createError({ statusCode: 400, statusMessage: 'An organization name is required.' })
  const slug = slugify(typeof body?.slug === 'string' && body.slug.trim() ? body.slug : name)
  if (!slug) throw createError({ statusCode: 400, statusMessage: 'The organization name must contain letters or digits.' })

  const db = useDb()
  const taken = await db.select({ id: hbSchema.organization.id }).from(hbSchema.organization).where(eq(hbSchema.organization.slug, slug)).limit(1)
  if (taken[0]) throw createError({ statusCode: 409, statusMessage: `An organization with the slug '${slug}' already exists.` })

  const now = new Date()
  const organization = { id: randomUUID(), name, slug, createdAt: now }
  await db.transaction(async (tx) => {
    await tx.insert(hbSchema.organization).values(organization)
    await tx.insert(hbSchema.member).values({ id: randomUUID(), organizationId: organization.id, userId: caller.id, role: 'owner', createdAt: now })
  })
  await evictBackendSessionCacheForUser(caller.id)

  setResponseStatus(event, 201)
  return { success: true, organization: { ...organization, createdAt: now.toISOString(), memberCount: 1 } }
})
