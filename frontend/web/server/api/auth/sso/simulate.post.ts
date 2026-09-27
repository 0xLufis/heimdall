import { defineEventHandler, readBody, createError, setCookie } from 'h3'
import { randomUUID } from 'node:crypto'
import { eq } from 'drizzle-orm'
import { useDb } from '../../../utils/db'
import * as hbSchema from '../../../database/drizzle/schema'
import { getPlantUsers, getPlantMetadata } from '../../../utils/datasetLoader'
import { syncUserSecurityGroupsToOrganizations } from '../../../utils/securityGroupOrgSync'

export default defineEventHandler(async (event) => {
  if (process.env.NODE_ENV === 'production') {
    throw createError({
      statusCode: 403,
      statusMessage: 'Azure SSO simulation is disabled in production.'
    })
  }

  const body = await readBody(event).catch(() => ({}))
  const plantUsers = getPlantUsers()
  const metadata = getPlantMetadata()

  const requestedEmail = (body?.email as string)?.trim().toLowerCase()
  const matchedUser = requestedEmail
    ? plantUsers.find(u => u.email.toLowerCase() === requestedEmail)
    : plantUsers[0] // Default to canonical plant user

  const email = requestedEmail || matchedUser?.email || 'entra.sso.operator@fake-factory.internal'
  const name = body?.name || matchedUser?.name || 'Azure SSO Simulated User'
  const role = body?.role || matchedUser?.primaryRole || 'controls_engineer'
  const tenantId = body?.tenantId || metadata.entraTenantId || '72f988bf-86f1-41af-91ab-2d7cd011db47'
  const securityGroupIds = body?.groups || matchedUser?.securityGroupIds || [
    'CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp'
  ]

  const userId = `usr-entra-${randomUUID().slice(0, 8)}`
  const sessionToken = `sso_${randomUUID().replace(/-/g, '')}`
  const expiresAt = new Date(Date.now() + 7 * 24 * 3600 * 1000)

  let syncResult: any = null

  try {
    const db = useDb()
    const existing = await db.select().from(hbSchema.user).where(eq(hbSchema.user.email, email))

    let finalUserId = userId
    if (existing.length > 0) {
      finalUserId = existing[0].id
      await db.update(hbSchema.user)
        .set({ role, name, updatedAt: new Date() })
        .where(eq(hbSchema.user.id, finalUserId))
    } else {
      await db.insert(hbSchema.user).values({
        id: finalUserId,
        name,
        email,
        emailVerified: true,
        role,
        image: `https://api.dicebear.com/7.x/identicon/svg?seed=${encodeURIComponent(name)}`
      })

      await db.insert(hbSchema.account).values({
        id: randomUUID(),
        accountId: `entra-${finalUserId}`,
        providerId: 'microsoft',
        userId: finalUserId
      })
    }

    // Insert active session
    await db.insert(hbSchema.session).values({
      id: randomUUID(),
      token: sessionToken,
      userId: finalUserId,
      expiresAt,
      ipAddress: '127.0.0.1 (Azure SSO Simulation)',
      userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) Microsoft-Entra-ID-SSO'
    })

    // Auto-sync directory security groups to organizations
    syncResult = await syncUserSecurityGroupsToOrganizations(finalUserId, securityGroupIds).catch(() => null)
  } catch (err: any) {
    console.warn('[simulateAzureSso] Database session write failed (offline mode):', err?.message || err)
  }

  // Set the session cookie so the browser is authenticated immediately
  setCookie(event, 'better-auth.session_token', sessionToken, {
    path: '/',
    httpOnly: false,
    sameSite: 'lax',
    maxAge: 7 * 24 * 3600
  })

  return {
    success: true,
    provider: 'microsoft',
    tenantId,
    user: {
      id: userId,
      email,
      name,
      role,
      securityGroupIds
    },
    session: {
      token: sessionToken,
      expiresAt: expiresAt.toISOString()
    },
    enrolledOrganizations: syncResult?.enrolledOrganizations || []
  }
})
