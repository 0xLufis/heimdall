import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'
import { POLICY_ROLES, roleSatisfies } from '~/utils/roles'
import { MASTER_SYSTEM_USER_PROFILES } from '~~/server/utils/authProfiles'

// ---------------------------------------------------------------------------------------------
// Test doubles: Better-Auth session lookup and the Drizzle query builder
// ---------------------------------------------------------------------------------------------
const state = vi.hoisted(() => ({
  betterAuthUser: null as any,
  dbRows: [] as any[],
  dbCalls: 0,
  dbFails: false
}))

vi.mock('~~/server/utils/auth', () => ({
  auth: { api: { getSession: vi.fn(async () => (state.betterAuthUser ? { user: state.betterAuthUser } : null)) } }
}))

vi.mock('~~/server/utils/db', () => {
  // Every builder method returns the same thenable, which resolves to the rows of the current test.
  const chain: any = new Proxy(function () {}, {
    get(_target, prop) {
      if (prop === 'then') {
        return (resolve: (v: any) => void, reject: (e: any) => void) =>
          state.dbFails ? reject(new Error('database offline')) : resolve(state.dbRows)
      }
      return () => chain
    }
  })
  return { useDb: () => { state.dbCalls++; return chain } }
})

import { getAuthenticatedUser } from '~~/server/utils/authHelper'
import { requireUser, requirePolicy } from '~~/server/utils/authz'
import { evaluateAdminAction } from '~~/server/utils/adminGuard'
import { pickDefaultOrganization, evictBackendSessionCache } from '~~/server/utils/sessionSync'
import usersHandler from '~~/server/api/users.get'
import organizationsHandler from '~~/server/api/organizations.get'
import membersHandler from '~~/server/api/organizations/[id]/members.get'
import mappingsListHandler from '~~/server/api/security-groups/mappings/index.get'
import mappingsCreateHandler from '~~/server/api/security-groups/mappings/index.post'
import mappingsUpdateHandler from '~~/server/api/security-groups/mappings/[id].put'
import mappingsDeleteHandler from '~~/server/api/security-groups/mappings/[id].delete'
import evaluateHandler from '~~/server/api/security-groups/evaluate.post'
import syncOrgsHandler from '~~/server/api/security-groups/sync-orgs.post'
import apiKeysListHandler from '~~/server/api/security-groups/api-keys/index.get'
import apiKeysCreateHandler from '~~/server/api/security-groups/api-keys/index.post'
import apiKeysDeleteHandler from '~~/server/api/security-groups/api-keys/[id].delete'
import apiKeysTestHandler from '~~/server/api/security-groups/api-keys/test.post'
import { resetAutomationApiKeysStore, listAutomationApiKeys, validateAutomationApiKey } from '~~/server/utils/automationApiKeysStore'

const ORIGINAL_ENV = { ...process.env }

/** Minimal h3 event: only headers (cookies) and route params are read before the guards decide. */
function event(options: { cookie?: string; authorization?: string; params?: Record<string, string> } = {}) {
  const headers: Record<string, string> = {}
  if (options.cookie) headers.cookie = options.cookie
  if (options.authorization) headers.authorization = options.authorization
  return {
    context: { params: options.params || {} },
    headers: new Headers(headers),
    node: { req: { headers, method: 'GET' }, res: {} },
    path: '/'
  } as any
}

const sessionCookie = (token: string) => `better-auth.session_token=${token}`

function signedInAs(role: string | null, id = 'u-1') {
  state.betterAuthUser = { id, name: 'Test User', email: `${id}@factory.test`, role }
  return event({ cookie: sessionCookie('real-token.signature') })
}

/** No opt-in dev mode: anonymous callers must not become the synthetic administrator. */
function outsideExplicitDevMode() {
  delete process.env.VITEST
  delete process.env.HEIMDALL_ENABLE_DEV
  process.env.NODE_ENV = 'development'
}

beforeEach(() => {
  state.betterAuthUser = null
  state.dbRows = []
  state.dbCalls = 0
  state.dbFails = false
  outsideExplicitDevMode()
})

afterEach(() => {
  process.env = { ...ORIGINAL_ENV }
  vi.unstubAllGlobals()
})

// ---------------------------------------------------------------------------------------------
describe('getAuthenticatedUser: who counts as signed in', () => {
  it('rejects anonymous callers outside explicit dev mode', async () => {
    await expect(getAuthenticatedUser(event(), true)).rejects.toMatchObject({ statusCode: 401 })
    expect(await getAuthenticatedUser(event(), false)).toBeUndefined()
  })

  it('uses the Better-Auth session when there is one', async () => {
    const user = await getAuthenticatedUser(signedInAs('technician'), true)
    expect(user).toMatchObject({ id: 'u-1', role: 'technician' })
    expect(state.dbCalls).toBe(0)
  })

  it('accepts a development SSO-simulation session that exists, is unexpired and not banned', async () => {
    state.dbRows = [{ id: 'usr-admin-primary', name: 'Admin', email: 'admin@heimdall.dev', role: 'system_admin', banned: null, expiresAt: new Date(Date.now() + 60_000) }]
    const user = await getAuthenticatedUser(event({ cookie: sessionCookie('sso_0123456789abcdef') }), true)
    expect(user).toMatchObject({ id: 'usr-admin-primary', role: 'system_admin' })
  })

  it('rejects forged, expired and banned SSO-simulation tokens', async () => {
    const sso = () => event({ cookie: sessionCookie('sso_forged') })
    state.dbRows = []
    await expect(getAuthenticatedUser(sso(), true)).rejects.toMatchObject({ statusCode: 401 })

    state.dbRows = [{ id: 'u', name: 'U', email: 'u@x', role: 'system_admin', banned: null, expiresAt: new Date(Date.now() - 1000) }]
    await expect(getAuthenticatedUser(sso(), true)).rejects.toMatchObject({ statusCode: 401 })

    state.dbRows = [{ id: 'u', name: 'U', email: 'u@x', role: 'system_admin', banned: true, expiresAt: new Date(Date.now() + 60_000) }]
    await expect(getAuthenticatedUser(sso(), true)).rejects.toMatchObject({ statusCode: 401 })

    state.dbFails = true
    await expect(getAuthenticatedUser(sso(), true)).rejects.toMatchObject({ statusCode: 401 })
  })

  it('never looks up bare tokens that are not simulation tokens, and never in production', async () => {
    state.dbRows = [{ id: 'u', name: 'U', email: 'u@x', role: 'system_admin', banned: null, expiresAt: new Date(Date.now() + 60_000) }]
    await expect(getAuthenticatedUser(event({ cookie: sessionCookie('some-other-bare-token') }), true)).rejects.toMatchObject({ statusCode: 401 })
    expect(state.dbCalls).toBe(0)

    process.env.NODE_ENV = 'production'
    await expect(getAuthenticatedUser(event({ cookie: sessionCookie('sso_0123456789abcdef') }), true)).rejects.toMatchObject({ statusCode: 401 })
    expect(state.dbCalls).toBe(0)
  })

  it('gives the explicit-dev-mode fallback the seeded system_admin identity', async () => {
    process.env.HEIMDALL_ENABLE_DEV = 'true'
    expect(await getAuthenticatedUser(event(), true)).toMatchObject({ id: 'usr-admin-primary', email: 'admin@heimdall.dev', role: 'system_admin' })
  })
})

describe('requireUser / requirePolicy', () => {
  it('maps a missing role to the least-privileged role', async () => {
    expect((await requireUser(signedInAs(null))).role).toBe('user')
  })

  it('checks the session role against the backend policy role sets', async () => {
    for (const role of ['system_admin', 'it_admin', 'it_site_admin']) {
      expect((await requirePolicy(signedInAs(role), 'ItAdministration')).role).toBe(role)
    }
    // "admin" in the name does not make an IT administrator, and neither does a client claim.
    for (const role of ['heimdall_admin', 'admin', 'engineering_admin', 'plant_director', 'technician', 'user', 'team_lead']) {
      await expect(requirePolicy(signedInAs(role), 'ItAdministration'), role).rejects.toMatchObject({ statusCode: 403 })
    }
    await expect(requirePolicy(event(), 'ItAdministration')).rejects.toMatchObject({ statusCode: 401 })
  })

  it('lets a system_admin pass every policy, exactly like the backend', async () => {
    for (const policy of Object.keys(POLICY_ROLES) as Array<keyof typeof POLICY_ROLES>) {
      expect(roleSatisfies('system_admin', policy), policy).toBe(true)
      expect((await requirePolicy(signedInAs('system_admin'), policy)).role, policy).toBe('system_admin')
    }
  })

  it('lets the administrator signed in through the SSO simulation pass every policy', async () => {
    // Regression: the bootstrap account was stored as the legacy `admin` role, which the BFF (like
    // the backend) refuses for ItAdministration - the admin got 403 on the Security Groups page.
    state.dbRows = [{ id: 'usr-admin-primary', name: 'System Administrator', email: 'admin@heimdall.dev', role: 'system_admin', banned: null, expiresAt: new Date(Date.now() + 60_000) }]
    for (const policy of Object.keys(POLICY_ROLES) as Array<keyof typeof POLICY_ROLES>) {
      const caller = await requirePolicy(event({ cookie: sessionCookie('sso_0123456789abcdef') }), policy)
      expect(caller).toMatchObject({ id: 'usr-admin-primary', role: 'system_admin' })
    }
    expect(Array.isArray(await mappingsListHandler(event({ cookie: sessionCookie('sso_0123456789abcdef') })))).toBe(true)
    expect(Array.isArray(await apiKeysListHandler(event({ cookie: sessionCookie('sso_0123456789abcdef') })))).toBe(true)
  })

  it('agrees with the backend that the legacy `admin` role is not an IT administrator', async () => {
    expect(POLICY_ROLES.ItAdministration).not.toContain('admin')
    expect(POLICY_ROLES.SystemAdministration).toContain('admin')
    await expect(requirePolicy(signedInAs('admin'), 'ItAdministration')).rejects.toMatchObject({ statusCode: 403 })
    expect((await requirePolicy(signedInAs('admin'), 'SystemAdministration')).role).toBe('admin')
  })
})

describe('bootstrap administrator role', () => {
  it('is system_admin in the boot plugin, the dev seed endpoint and the master profile catalogue', () => {
    const read = (relative: string) => fs.readFileSync(path.resolve(__dirname, '../../../src/frontend/web', relative), 'utf-8')
    const plugin = read('server/plugins/ensureAdminUser.ts')
    const seedEndpoint = read('server/api/dev/seed-admin.get.ts')

    expect(plugin).toContain('const adminRole = "system_admin"')
    for (const source of [plugin, seedEndpoint]) {
      // No code path may write the legacy role onto the bootstrap account any more.
      expect(source).not.toMatch(/role:\s*"admin"\s*\}\)\s*\n\s*\.where\(eq\(user\.id/)
    }
    expect(seedEndpoint).toContain('role: "system_admin"')
    expect(MASTER_SYSTEM_USER_PROFILES.find(p => p.id === 'usr-admin-primary')).toMatchObject({ email: 'admin@heimdall.dev', role: 'system_admin' })
  })
})

describe('identity BFF routes are no longer anonymous', () => {
  const guarded: Array<[string, (e: any) => any]> = [
    ['GET /api/users', usersHandler],
    ['GET /api/organizations', organizationsHandler],
    ['GET /api/organizations/:id/members', membersHandler],
    ['GET /api/security-groups/mappings', mappingsListHandler],
    ['POST /api/security-groups/mappings', mappingsCreateHandler],
    ['PUT /api/security-groups/mappings/:id', mappingsUpdateHandler],
    ['DELETE /api/security-groups/mappings/:id', mappingsDeleteHandler],
    ['POST /api/security-groups/evaluate', evaluateHandler],
    ['POST /api/security-groups/sync-orgs', syncOrgsHandler],
    ['GET /api/security-groups/api-keys', apiKeysListHandler],
    ['POST /api/security-groups/api-keys', apiKeysCreateHandler],
    ['DELETE /api/security-groups/api-keys/:id', apiKeysDeleteHandler],
    ['POST /api/security-groups/api-keys/test', apiKeysTestHandler]
  ]

  it.each(guarded)('%s answers 401 without a session', async (_name, handler) => {
    await expect(Promise.resolve().then(() => handler(event({ params: { id: 'org-a' } })))).rejects.toMatchObject({ statusCode: 401 })
  })

  it.each(guarded.slice(3))('%s answers 403 to a signed-in user who is not an IT administrator', async (_name, handler) => {
    for (const role of ['technician', 'engineering_admin', 'heimdall_admin']) {
      await expect(Promise.resolve().then(() => handler(signedInAs(role))), role).rejects.toMatchObject({ statusCode: 403 })
    }
  })

  it('lets IT administrators read mappings and API keys', async () => {
    expect(Array.isArray(await mappingsListHandler(signedInAs('it_admin')))).toBe(true)
    expect(Array.isArray(await apiKeysListHandler(signedInAs('system_admin')))).toBe(true)
  })

  it('serves the whole user directory, with real membership counts, to identity administrators', async () => {
    state.dbRows = [
      { id: 'u-1', name: 'A', email: 'a@x', role: 'technician', username: 'a', banned: null, createdAt: new Date(), userId: 'u-1', organizationId: 'org-a', organizations: 2 },
      { id: 'u-2', name: 'B', email: 'b@x', role: 'engineer', username: 'b', banned: true, createdAt: new Date(), userId: 'u-1', organizationId: 'org-a', organizations: 2 }
    ]
    for (const role of ['system_admin', 'heimdall_admin', 'it_admin', 'engineering_admin', 'plant_engineering_manager']) {
      const res = await usersHandler(signedInAs(role, 'u-admin'))
      expect(res.users.map((u: any) => u.id), role).toEqual(['u-1', 'u-2'])
      expect(res.users[0]).toMatchObject({ banned: false, organizationCount: 2 })
      expect(res.users[1]).toMatchObject({ banned: true })
    }
  })

  it('shows other users only the colleagues they share an organization with', async () => {
    // Membership rows name u-1 only: u-2 is in no organization the caller belongs to.
    state.dbRows = [
      { id: 'u-1', name: 'A', email: 'a@x', role: 'technician', username: 'a', banned: null, createdAt: new Date(), userId: 'u-1', organizationId: 'org-a', organizations: 1 },
      { id: 'u-2', name: 'B', email: 'b@x', role: 'engineer', username: 'b', banned: null, createdAt: new Date(), userId: 'u-1', organizationId: 'org-a', organizations: 1 }
    ]
    for (const role of ['technician', 'operator', 'group_leader', 'manager', 'user']) {
      const res = await usersHandler(signedInAs(role, 'u-1'))
      expect(res.users.map((u: any) => u.id), role).toEqual(['u-1'])
    }
  })

  it('answers 503 instead of a made-up directory when the database is unreachable', async () => {
    state.dbFails = true
    await expect(usersHandler(signedInAs('system_admin'))).rejects.toMatchObject({ statusCode: 503 })
    await expect(organizationsHandler(signedInAs('system_admin'))).rejects.toMatchObject({ statusCode: 503 })
    await expect(membersHandler({ ...signedInAs('system_admin'), context: { params: { id: 'org-a' } } })).rejects.toMatchObject({ statusCode: 503 })
  })

  it('shows an organization roster only to its members and to platform administrators', async () => {
    const roster = [{ id: 'm1', role: 'member', createdAt: new Date(), userId: 'u-member', name: 'M', email: 'm@x', username: 'm' }]
    const forOrg = (e: any) => ({ ...e, context: { params: { id: 'org-a' } } })

    state.dbRows = roster
    await expect(membersHandler(forOrg(signedInAs('technician', 'u-outsider')))).rejects.toMatchObject({ statusCode: 403 })
    // An administrator of another kind is still an outsider to this tenant.
    await expect(membersHandler(forOrg(signedInAs('engineering_admin', 'u-outsider')))).rejects.toMatchObject({ statusCode: 403 })

    expect((await membersHandler(forOrg(signedInAs('technician', 'u-member')))).members).toHaveLength(1)
    for (const role of ['system_admin', 'heimdall_admin', 'admin', 'plant_director']) {
      expect((await membersHandler(forOrg(signedInAs(role, 'u-outsider')))).members, role).toHaveLength(1)
    }
  })

  it('returns no organizations to a user without memberships instead of every tenant', async () => {
    state.dbRows = []
    expect(await organizationsHandler(signedInAs('technician'))).toEqual({ success: true, organizations: [] })
  })
})

describe('demo automation API keys', () => {
  afterEach(() => {
    process.env = { ...ORIGINAL_ENV }
    resetAutomationApiKeysStore()
  })

  it('are not valid credentials when development features are off', () => {
    process.env.NODE_ENV = 'production'
    process.env.HEIMDALL_ENABLE_DEV = 'false'
    resetAutomationApiKeysStore()
    expect(listAutomationApiKeys()).toHaveLength(0)
    expect(validateAutomationApiKey('hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c')).toBeNull()
    expect(validateAutomationApiKey('hmd_auto_jira_demo_1a2b3c4d5e6f7a8b9c')).toBeNull()
  })

  it('remain available for the sandbox while development features are on', () => {
    process.env.NODE_ENV = 'development'
    resetAutomationApiKeysStore()
    expect(validateAutomationApiKey('hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c')).not.toBeNull()
  })
})

describe('evaluateAdminAction: user administrators cannot escalate', () => {
  const act = (overrides: Partial<Parameters<typeof evaluateAdminAction>[0]>) =>
    evaluateAdminAction({ path: '/admin/set-role', actorId: 'actor', actorRole: 'engineering_admin', ...overrides })

  it('refuses changing your own role, even downwards', () => {
    expect(act({ targetId: 'actor', targetRole: 'engineering_admin', requestedRole: 'system_admin' })).toMatch(/own role/)
    expect(act({ actorRole: 'system_admin', targetId: 'actor', targetRole: 'system_admin', requestedRole: 'technician' })).toMatch(/own role/)
  })

  it('refuses granting a role above the caller’s own rank', () => {
    expect(act({ targetId: 'other', targetRole: 'technician', requestedRole: 'system_admin' })).toMatch(/may not grant/)
    expect(act({ targetId: 'other', targetRole: 'technician', requestedRole: 'heimdall_admin' })).toMatch(/may not grant/)
    expect(act({ actorRole: 'heimdall_admin', targetId: 'other', targetRole: 'technician', requestedRole: 'system_admin' })).toMatch(/may not grant/)
    expect(act({ path: '/admin/create-user', requestedRole: 'system_admin' })).toMatch(/may not grant/)
  })

  it('refuses acting on a more privileged user (role, password, ban, sessions, impersonation)', () => {
    for (const path of ['/admin/set-user-password', '/admin/ban-user', '/admin/remove-user', '/admin/list-user-sessions', '/admin/revoke-user-sessions', '/admin/revoke-user-session', '/admin/impersonate-user']) {
      expect(act({ path, targetId: 'root', targetRole: 'system_admin' }), path).toMatch(/higher privilege/)
    }
    expect(act({ targetId: 'root', targetRole: 'system_admin', requestedRole: 'technician' })).toMatch(/higher privilege/)
  })

  it('refuses unknown, empty and multiple roles', () => {
    for (const requestedRole of ['team_lead', 'generic', 'owner', '', ['admin', 'user'], 'admin,user']) {
      expect(act({ actorRole: 'system_admin', targetId: 'other', targetRole: 'user', requestedRole }), String(requestedRole)).toMatch(/exactly one known/)
    }
  })

  it('allows ordinary user administration within the caller’s rank', () => {
    expect(act({ targetId: 'other', targetRole: 'technician', requestedRole: 'group_leader' })).toBeNull()
    expect(act({ targetId: 'other', targetRole: 'user', requestedRole: 'plant_engineering_manager' })).toBeNull()
    expect(act({ path: '/admin/ban-user', targetId: 'other', targetRole: 'engineer' })).toBeNull()
    expect(act({ actorRole: 'heimdall_admin', targetId: 'other', targetRole: 'admin', requestedRole: 'plant_director' })).toBeNull()
    expect(act({ actorRole: 'system_admin', targetId: 'other', targetRole: 'system_admin', requestedRole: 'system_admin' })).toBeNull()
    expect(act({ path: '/admin/list-users' })).toBeNull()
  })
})

describe('session synchronisation helpers', () => {
  it('starts a session in the organization where the user has the highest role, then the oldest', () => {
    expect(pickDefaultOrganization([])).toBeUndefined()
    expect(pickDefaultOrganization([
      { organizationId: 'org-b', role: 'member', createdAt: '2026-01-01' },
      { organizationId: 'org-c', role: 'admin', createdAt: '2026-03-01' },
      { organizationId: 'org-a', role: 'owner', createdAt: '2026-06-01' }
    ])).toBe('org-a')
    expect(pickDefaultOrganization([
      { organizationId: 'org-z', role: 'member', createdAt: '2026-01-01' },
      { organizationId: 'org-y', role: 'member', createdAt: '2025-01-01' }
    ])).toBe('org-y')
    expect(pickDefaultOrganization([
      { organizationId: 'org-b', role: 'member' },
      { organizationId: 'org-a', role: 'member' }
    ])).toBe('org-a')
  })

  it('evicts each affected session from the backend cache using that session’s own token', async () => {
    const fetchMock = vi.fn(async () => new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    process.env.BACKEND_API_URL = 'http://backend:5099/'

    await evictBackendSessionCache(['tok-1', 'tok-1', null, '', 'tok-2'])

    expect(fetchMock).toHaveBeenCalledTimes(2)
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('http://backend:5099/api/v1/auth/session/evict')
    expect(init.method).toBe('POST')
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer tok-1')
  })

  it('never fails the caller when the backend is unreachable', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new Error('ECONNREFUSED') }))
    await expect(evictBackendSessionCache(['tok-1'])).resolves.toBeUndefined()
  })
})
