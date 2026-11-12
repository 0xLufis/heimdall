// @vitest-environment node
import { describe, it, expect, beforeAll } from 'vitest'
import { betterAuth } from 'better-auth'
import { memoryAdapter } from 'better-auth/adapters/memory'
import { admin, organization } from 'better-auth/plugins'
import { adminAc, userAc } from 'better-auth/plugins/admin/access'
import { adminEscalationGuard } from '~~/server/utils/adminGuard'
import { DEFAULT_ROLE, HEIMDALL_ROLES, USER_ADMIN_ROLES, isPlatformAdminRole } from '~/utils/roles'

/**
 * Runs the real Better-Auth admin + organization plugins (in-memory database) with the same role
 * map, escalation guard and organization-creation rule that server/utils/auth.ts configures.
 */
const db: Record<string, any[]> = { user: [], session: [], account: [], verification: [], organization: [], member: [], invitation: [] }
const userAdminRoles: readonly string[] = USER_ADMIN_ROLES

const auth = betterAuth({
  baseURL: 'http://localhost:3000',
  secret: 'heimdall-unit-test-secret-0123456789abcdef',
  database: memoryAdapter(db),
  emailAndPassword: { enabled: true },
  hooks: { before: adminEscalationGuard },
  plugins: [
    admin({
      adminRoles: [...USER_ADMIN_ROLES],
      defaultRole: DEFAULT_ROLE,
      roles: Object.fromEntries(HEIMDALL_ROLES.map(role => [role, userAdminRoles.includes(role) ? adminAc : userAc]))
    }),
    organization({ allowUserToCreateOrganization: user => isPlatformAdminRole((user as any).role) })
  ]
})

const users: Record<string, { id: string; headers: Headers }> = {}

async function createUser(key: string, role: string) {
  const email = `${key}@factory.test`
  const password = 'Password-123456'
  const created = await auth.api.signUpEmail({ body: { email, password, name: key } })
  db.user.find(u => u.id === created.user.id).role = role
  const signedIn = await auth.api.signInEmail({ body: { email, password }, returnHeaders: true })
  const cookie = signedIn.headers.getSetCookie().map(c => c.split(';')[0]).join('; ')
  users[key] = { id: created.user.id, headers: new Headers({ cookie }) }
}

const roleOf = (key: string) => db.user.find(u => u.id === users[key]!.id).role
const setRole = (actor: string, target: string, role: string) =>
  auth.api.setRole({ headers: users[actor]!.headers, body: { userId: users[target]!.id, role: role as any } })

describe('Better-Auth admin endpoints with the Heimdall role map and escalation guard', () => {
  beforeAll(async () => {
    await createUser('root', 'system_admin')
    await createUser('platform', 'heimdall_admin')
    await createUser('engadmin', 'engineering_admin')
    await createUser('tech', 'technician')
  })

  it('accepts every backend role (group_leader, plant_director, ... were rejected before)', async () => {
    for (const role of ['group_leader', 'shift_leader', 'lead_engineer', 'controls_engineer', 'operator', 'operative_planner', 'plant_director', 'it_site_admin']) {
      await setRole('root', 'tech', role)
      expect(roleOf('tech')).toBe(role)
    }
    await setRole('root', 'tech', 'technician')
  })

  it('rejects role names the backend does not know', async () => {
    for (const role of ['team_lead', 'generic']) {
      await expect(setRole('root', 'tech', role)).rejects.toMatchObject({ status: expect.stringMatching(/FORBIDDEN|BAD_REQUEST/) })
    }
    expect(roleOf('tech')).toBe('technician')
  })

  it('stops an engineering administrator from making themselves or anyone a system administrator', async () => {
    await expect(setRole('engadmin', 'engadmin', 'system_admin')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(setRole('engadmin', 'tech', 'system_admin')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(setRole('engadmin', 'tech', 'heimdall_admin')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(setRole('platform', 'platform', 'system_admin')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    expect(roleOf('engadmin')).toBe('engineering_admin')
    expect(roleOf('platform')).toBe('heimdall_admin')
    expect(roleOf('tech')).toBe('technician')
  })

  it('stops a lower administrator from touching a system administrator', async () => {
    const target = { userId: users.root!.id }
    await expect(setRole('engadmin', 'root', 'technician')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(auth.api.setUserPassword({ headers: users.engadmin!.headers, body: { ...target, newPassword: 'Hijacked-123456' } })).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(auth.api.banUser({ headers: users.platform!.headers, body: target })).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(auth.api.listUserSessions({ headers: users.engadmin!.headers, body: target })).rejects.toMatchObject({ status: 'FORBIDDEN' })
    expect(roleOf('root')).toBe('system_admin')
  })

  it('still allows ordinary user administration', async () => {
    await setRole('engadmin', 'tech', 'engineer')
    expect(roleOf('tech')).toBe('engineer')
    await setRole('platform', 'tech', 'technician')
    expect(roleOf('tech')).toBe('technician')
  })

  it('lets only platform administrators create organizations', async () => {
    const create = (actor: string, slug: string) =>
      auth.api.createOrganization({ headers: users[actor]!.headers, body: { name: slug, slug } })

    await expect(create('tech', 'tech-org')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    await expect(create('engadmin', 'eng-org')).rejects.toMatchObject({ status: 'FORBIDDEN' })
    expect((await create('platform', 'line-99'))?.slug).toBe('line-99')
  })
})
