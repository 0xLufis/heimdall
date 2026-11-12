import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'

// ---------------------------------------------------------------------------------------------
// Test doubles: Better-Auth session lookup and the Drizzle query builder (same pattern as
// IdentityAuthorizationGuards.test.ts: every query resolves to the rows of the current test).
// ---------------------------------------------------------------------------------------------
const state = vi.hoisted(() => ({ betterAuthUser: null as any, dbRows: [] as any[] }))

vi.mock('~~/server/utils/auth', () => ({
  auth: { api: { getSession: vi.fn(async () => (state.betterAuthUser ? { user: state.betterAuthUser } : null)) } }
}))

vi.mock('~~/server/utils/db', () => {
  const chain: any = new Proxy(function () {}, {
    get(_target, prop) {
      if (prop === 'then') return (resolve: (v: any) => void) => resolve(state.dbRows)
      return () => chain
    }
  })
  return { useDb: () => chain }
})

import { evaluateOrganizationAction } from '~~/server/utils/organizationRules'
import { evaluateOrganizationPluginCall, evaluateAdminAction } from '~~/server/utils/adminGuard'
import createOrganization from '~~/server/api/organizations/index.post'
import renameOrganization from '~~/server/api/organizations/[id]/index.patch'
import deleteOrganization from '~~/server/api/organizations/[id]/index.delete'
import addMember from '~~/server/api/organizations/[id]/members.post'
import changeMemberRole from '~~/server/api/organizations/[id]/members/[memberId].patch'
import removeMember from '~~/server/api/organizations/[id]/members/[memberId].delete'
import inviteMember from '~~/server/api/organizations/[id]/invitations.post'
import setPermissions from '~~/server/api/users/[id]/permissions.put'

const ORIGINAL_ENV = { ...process.env }

/** h3 event carrying a session cookie, route params and (for write methods) a JSON body. */
function request(options: { role?: string | null; id?: string; permissions?: string | null; method?: string; body?: any; params?: Record<string, string> } = {}) {
  if (options.role !== undefined) {
    state.betterAuthUser = { id: options.id ?? 'u-caller', name: 'Caller', email: 'caller@factory.test', role: options.role, permissions: options.permissions ?? null }
  }
  const method = options.method ?? 'POST'
  const headers: Record<string, string> = { 'content-type': 'application/json' }
  if (options.role !== undefined) headers.cookie = 'better-auth.session_token=real-token.signature'
  return {
    method,
    context: { params: options.params ?? { id: 'org-a', memberId: 'mem-target' } },
    headers: new Headers(headers),
    node: { req: { headers, method, body: options.body ?? {} }, res: {} },
    path: '/'
  } as any
}

const call = (handler: (e: any) => any, e: any) => Promise.resolve().then(() => handler(e))

beforeEach(() => {
  state.betterAuthUser = null
  state.dbRows = []
  delete process.env.VITEST
  delete process.env.HEIMDALL_ENABLE_DEV
  process.env.NODE_ENV = 'development'
  // Session-cache eviction calls the backend; keep it local.
  vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 204 })))
})

afterEach(() => {
  process.env = { ...ORIGINAL_ENV }
  vi.unstubAllGlobals()
})

// ---------------------------------------------------------------------------------------------
describe('evaluateOrganizationAction: the OrganizationManagement policy', () => {
  const actor = (role: string, permissions?: string) => ({ id: 'u', role, permissions })

  it('lets Heimdall administrators do everything to every organization', () => {
    for (const role of ['system_admin', 'heimdall_admin', 'admin']) {
      for (const action of ['create', 'update', 'delete', 'manageMembers'] as const) {
        expect(evaluateOrganizationAction(actor(role), action, { actorMemberRole: null }), `${role} ${action}`).toBeNull()
      }
      expect(evaluateOrganizationAction(actor(role), 'manageMembers', { actorMemberRole: null, grantedRole: 'owner', targetMemberRole: 'owner' })).toBeNull()
    }
  })

  it('lets managers and granted users create, and manage only the organizations they belong to', () => {
    for (const a of [actor('group_leader'), actor('manager'), actor('plant_director'), actor('engineering_admin'), actor('shift_leader', 'organizations.manage')]) {
      expect(evaluateOrganizationAction(a, 'create'), a.role).toBeNull()
      expect(evaluateOrganizationAction(a, 'update', { actorMemberRole: null }), a.role).toMatch(/member of/)
      expect(evaluateOrganizationAction(a, 'manageMembers', { actorMemberRole: null, grantedRole: 'member' }), a.role).toMatch(/member of/)
      expect(evaluateOrganizationAction(a, 'update', { actorMemberRole: 'member' }), a.role).toBeNull()
      expect(evaluateOrganizationAction(a, 'manageMembers', { actorMemberRole: 'admin', grantedRole: 'admin', targetMemberRole: 'member' }), a.role).toBeNull()
      // Deleting needs ownership.
      expect(evaluateOrganizationAction(a, 'delete', { actorMemberRole: 'admin' }), a.role).toMatch(/owner/)
      expect(evaluateOrganizationAction(a, 'delete', { actorMemberRole: 'owner' }), a.role).toBeNull()
    }
  })

  it('refuses everyone without the right, by role or grant', () => {
    for (const role of ['shift_leader', 'lead_engineer', 'it_admin', 'it_site_admin', 'technician', 'operator', 'user']) {
      for (const action of ['create', 'update', 'delete', 'manageMembers'] as const) {
        expect(evaluateOrganizationAction(actor(role), action, { actorMemberRole: 'owner' }), `${role} ${action}`).toMatch(/OrganizationManagement/)
      }
      expect(evaluateOrganizationAction(actor(role, 'something.else'), 'create'), role).toMatch(/OrganizationManagement/)
    }
  })

  it('keeps owner rights with owners: no owner grants, owner changes or own-role changes by non-owners', () => {
    const manager = actor('group_leader')
    expect(evaluateOrganizationAction(manager, 'manageMembers', { actorMemberRole: 'admin', grantedRole: 'owner', targetMemberRole: 'member' })).toMatch(/owner/)
    expect(evaluateOrganizationAction(manager, 'manageMembers', { actorMemberRole: 'admin', targetMemberRole: 'owner' })).toMatch(/owner/)
    expect(evaluateOrganizationAction(manager, 'manageMembers', { actorMemberRole: 'member', grantedRole: 'owner', targetMemberRole: 'member', targetIsActor: true })).toMatch(/own member role/)
    expect(evaluateOrganizationAction(manager, 'manageMembers', { actorMemberRole: 'owner', grantedRole: 'owner', targetMemberRole: 'member' })).toBeNull()
    // Leaving (removing oneself) is not a role change.
    expect(evaluateOrganizationAction(manager, 'manageMembers', { actorMemberRole: 'member', targetMemberRole: 'member', targetIsActor: true })).toBeNull()
    expect(evaluateOrganizationAction(manager, 'manageMembers', { actorMemberRole: 'owner', grantedRole: 'superuser' })).toMatch(/Unknown member role/)
  })
})

describe('Better-Auth organization plugin endpoints', () => {
  it('require the OrganizationManagement right for every mutation, but not for joining or switching', () => {
    for (const path of ['/organization/create', '/organization/update', '/organization/delete', '/organization/invite-member', '/organization/remove-member', '/organization/update-member-role']) {
      expect(evaluateOrganizationPluginCall(path, 'technician', null), path).toMatch(/OrganizationManagement/)
      expect(evaluateOrganizationPluginCall(path, 'technician', 'organizations.manage'), path).toBeNull()
      expect(evaluateOrganizationPluginCall(path, 'group_leader', null), path).toBeNull()
    }
    for (const path of ['/organization/accept-invitation', '/organization/set-active', '/organization/leave', '/organization/list']) {
      expect(evaluateOrganizationPluginCall(path, 'technician', null), path).toBeNull()
    }
  })

  it('still applies the admin escalation rules unchanged', () => {
    expect(evaluateAdminAction({ path: '/admin/set-role', actorId: 'a', actorRole: 'engineering_admin', targetId: 'b', targetRole: 'user', requestedRole: 'system_admin' })).toMatch(/may not grant/)
  })
})

// ---------------------------------------------------------------------------------------------
describe('organization management BFF routes', () => {
  const routes: Array<[string, (e: any) => any]> = [
    ['POST /api/organizations', createOrganization],
    ['PATCH /api/organizations/:id', renameOrganization],
    ['DELETE /api/organizations/:id', deleteOrganization],
    ['POST /api/organizations/:id/members', addMember],
    ['PATCH /api/organizations/:id/members/:memberId', changeMemberRole],
    ['DELETE /api/organizations/:id/members/:memberId', removeMember],
    ['POST /api/organizations/:id/invitations', inviteMember],
    ['PUT /api/users/:id/permissions', setPermissions]
  ]

  it.each(routes)('%s answers 401 without a session', async (_name, handler) => {
    await expect(call(handler, request())).rejects.toMatchObject({ statusCode: 401 })
  })

  it.each(routes)('%s answers 403 to a role without the right and without a grant', async (_name, handler) => {
    state.dbRows = [{ id: 'org-a', name: 'Line A', slug: 'line-a', role: 'owner', userId: 'u-caller', permissions: null }]
    for (const role of ['technician', 'shift_leader', 'lead_engineer', 'it_admin']) {
      await expect(call(handler, request({ role, body: { name: 'X', role: 'member', email: 'x@factory.test', grant: ['organizations.manage'] } })), role)
        .rejects.toMatchObject({ statusCode: 403 })
    }
  })

  it('lets a manager or a granted user create an organization and become its owner', async () => {
    state.dbRows = [] // slug not taken
    for (const caller of [{ role: 'group_leader' }, { role: 'technician', permissions: 'organizations.manage' }]) {
      const e = request({ ...caller, body: { name: 'Line 09 – Press Shop' } })
      const res = await createOrganization(e)
      expect(res.organization).toMatchObject({ name: 'Line 09 – Press Shop', slug: 'line-09-press-shop', memberCount: 1 })
    }
    await expect(call(createOrganization, request({ role: 'group_leader', body: { name: '  ' } }))).rejects.toMatchObject({ statusCode: 400 })
  })

  it('lets a manager rename only organizations they belong to, and delete only ones they own', async () => {
    const asMember = (memberRole: string | undefined) => {
      state.dbRows = [{ id: 'org-a', name: 'Line A', slug: 'line-a', role: memberRole, userId: 'u-caller' }]
    }
    asMember(undefined) // not a member
    await expect(call(renameOrganization, request({ role: 'group_leader', method: 'PATCH', body: { name: 'New' } }))).rejects.toMatchObject({ statusCode: 403 })
    asMember('member')
    expect((await renameOrganization(request({ role: 'group_leader', method: 'PATCH', body: { name: 'New' } }))).organization.name).toBe('New')
    await expect(call(deleteOrganization, request({ role: 'group_leader', method: 'DELETE' }))).rejects.toMatchObject({ statusCode: 403 })
    asMember('owner')
    expect(await deleteOrganization(request({ role: 'group_leader', method: 'DELETE' }))).toMatchObject({ success: true, deleted: 'org-a' })
  })

  it('lets Heimdall administrators manage organizations they are not a member of', async () => {
    state.dbRows = [{ id: 'org-a', name: 'Line A', slug: 'line-a', role: undefined, userId: 'u-target' }]
    for (const role of ['system_admin', 'heimdall_admin', 'admin']) {
      expect((await renameOrganization(request({ role, method: 'PATCH', body: { name: 'Renamed' } }))).success, role).toBe(true)
      expect((await inviteMember(request({ role, body: { email: 'new.hire@factory.test', role: 'admin' } }))).invitation, role)
        .toMatchObject({ organizationId: 'org-a', email: 'new.hire@factory.test', role: 'admin', status: 'pending', inviterId: 'u-caller' })
      expect((await deleteOrganization(request({ role, method: 'DELETE' }))).success, role).toBe(true)
    }
  })

  it('validates member roles and invitation addresses', async () => {
    state.dbRows = [{ id: 'org-a', name: 'Line A', slug: 'line-a', role: 'owner', userId: 'u-caller' }]
    await expect(call(inviteMember, request({ role: 'system_admin', body: { email: 'not-an-address', role: 'member' } }))).rejects.toMatchObject({ statusCode: 400 })
    await expect(call(addMember, request({ role: 'system_admin', body: { email: 'x@factory.test', role: 'superuser' } }))).rejects.toMatchObject({ statusCode: 403 })
  })

  it('lets only holders of the right by role grant or revoke permissions', async () => {
    state.dbRows = [{ id: 'u-target', permissions: null }]
    const grant = (role: string, permissions?: string, body: any = { grant: ['organizations.manage'] }) =>
      call(setPermissions, request({ role, permissions, method: 'PUT', body, params: { id: 'u-target' } }))

    // A granted user cannot pass the grant on.
    await expect(grant('shift_leader', 'organizations.manage')).rejects.toMatchObject({ statusCode: 403 })
    for (const role of ['group_leader', 'manager', 'heimdall_admin', 'system_admin']) {
      expect(await grant(role), role).toMatchObject({ success: true, permissions: ['organizations.manage'] })
    }
    state.dbRows = [{ id: 'u-target', permissions: 'organizations.manage' }]
    expect(await grant('group_leader', undefined, { revoke: ['organizations.manage'] })).toMatchObject({ permissions: [] })
    await expect(grant('group_leader', undefined, { permissions: ['system:settings:manage'] })).rejects.toMatchObject({ statusCode: 400 })
  })
})
