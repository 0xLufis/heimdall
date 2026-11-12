import { describe, it, expect } from 'vitest'
import fs from 'node:fs'
import path from 'node:path'
import {
  HEIMDALL_ROLES,
  POLICY_ROLES,
  USER_ADMIN_ROLES,
  IT_ADMIN_DELEGABLE_ROLES,
  ROLE_LABELS,
  ROLE_DESCRIPTIONS,
  DEFAULT_ROLE,
  isKnownRole,
  isPlatformAdminRole,
  isUserAdminRole,
  roleSatisfies,
  roleRank,
  splitRoles,
  roleLabel,
  roleDescription
} from '~/utils/roles'

const repoRoot = path.resolve(__dirname, '../../..')
const read = (relative: string) => fs.readFileSync(path.join(repoRoot, relative), 'utf-8')

/** `public const string SystemAdmin = "system_admin";` inside `class HeimdallRoles` -> { SystemAdmin: 'system_admin' } */
function parseBackendRoles(): Record<string, string> {
  const source = read('src/shared/App.Contracts/Security/SecurityConstants.cs')
  const body = source.slice(source.indexOf('class HeimdallRoles'), source.indexOf('class AuthorizationPolicies'))
  const roles: Record<string, string> = {}
  for (const m of body.matchAll(/public const string (\w+)\s*=\s*"([^"]+)";/g)) roles[m[1]!] = m[2]!
  return roles
}

/** `options.AddPolicy(<name>, policy => policy.RequireRole(HeimdallRoles.A, ...))` -> { name: ['a', ...] } */
function parseBackendPolicies(roles: Record<string, string>): Record<string, string[]> {
  const source = read('src/backend/App.Backend.Api/Program.cs')
  const policies: Record<string, string[]> = {}
  const pattern = /options\.AddPolicy\(\s*(?:AuthorizationPolicies\.(\w+)|"(\w+)")\s*,\s*policy\s*=>\s*policy\.RequireRole\(([^;]*?)\)\s*\)\s*;/g
  for (const m of source.matchAll(pattern)) {
    const name = (m[1] || m[2])!
    policies[name] = [...m[3]!.matchAll(/HeimdallRoles\.(\w+)/g)].map(r => {
      const value = roles[r[1]!]
      if (!value) throw new Error(`Program.cs references unknown HeimdallRoles.${r[1]}`)
      return value
    })
  }
  return policies
}

describe('role catalog (app/utils/roles.ts) matches the backend', () => {
  const backendRoles = parseBackendRoles()
  const backendPolicies = parseBackendPolicies(backendRoles)

  it('lists exactly the roles declared in HeimdallRoles, in the same order', () => {
    expect(Object.values(backendRoles).length).toBeGreaterThan(10)
    expect([...HEIMDALL_ROLES]).toEqual(Object.values(backendRoles))
  })

  it('mirrors every authorization policy registered in Program.cs', () => {
    expect(Object.keys(backendPolicies).length).toBeGreaterThanOrEqual(11)
    expect(Object.keys(POLICY_ROLES).sort()).toEqual(Object.keys(backendPolicies).sort())
    for (const [policy, roles] of Object.entries(backendPolicies)) {
      expect([...POLICY_ROLES[policy as keyof typeof POLICY_ROLES]].sort(), policy).toEqual([...roles].sort())
    }
  })

  it('only references known roles and labels/describes every role', () => {
    const referenced = [...Object.values(POLICY_ROLES).flat(), ...USER_ADMIN_ROLES, ...IT_ADMIN_DELEGABLE_ROLES, DEFAULT_ROLE]
    for (const role of referenced) expect(isKnownRole(role), role).toBe(true)
    for (const role of HEIMDALL_ROLES) {
      expect(ROLE_LABELS[role], role).toBeTruthy()
      expect(ROLE_DESCRIPTIONS[role], role).toBeTruthy()
    }
    expect(Object.keys(ROLE_LABELS).sort()).toEqual([...HEIMDALL_ROLES].sort())
    expect(Object.keys(ROLE_DESCRIPTIONS).sort()).toEqual([...HEIMDALL_ROLES].sort())
  })

  it('rejects role names the backend does not know', () => {
    for (const stray of ['team_lead', 'generic', 'sysadmin', 'owner', 'member', '', null, undefined]) {
      expect(isKnownRole(stray), String(stray)).toBe(false)
    }
  })
})

describe('role helpers', () => {
  it('defines platform administrators exactly as the SystemAdministration policy', () => {
    const platformAdmins = HEIMDALL_ROLES.filter(isPlatformAdminRole)
    expect(platformAdmins).toEqual(['system_admin', 'heimdall_admin', 'admin', 'plant_director'])
    // A role is not a platform administrator just because its name contains "admin".
    for (const role of ['it_admin', 'it_site_admin', 'engineering_admin']) expect(isPlatformAdminRole(role)).toBe(false)
  })

  it('treats missing, unknown and mixed-case roles safely', () => {
    expect(isPlatformAdminRole(undefined)).toBe(false)
    expect(isPlatformAdminRole('')).toBe(false)
    expect(isPlatformAdminRole('administrator')).toBe(false)
    expect(isPlatformAdminRole(' System_Admin ')).toBe(true)
    expect(roleSatisfies('technician', 'MaintenanceOperations')).toBe(true)
    expect(roleSatisfies('technician', 'RemoteExecution')).toBe(false)
    expect(roleSatisfies('controls_engineer', 'EndpointConfigManagement')).toBe(true)
    expect(roleSatisfies('controls_engineer', 'RemoteExecution')).toBe(false)
    expect(splitRoles('admin, user')).toEqual(['admin', 'user'])
    expect(roleSatisfies('user,admin', 'SystemAdministration')).toBe(true)
  })

  it('ranks roles so that nobody outranks system_admin and plain roles rank lowest', () => {
    expect(roleRank('system_admin')).toBe(3)
    for (const role of ['heimdall_admin', 'admin', 'plant_director']) expect(roleRank(role), role).toBe(2)
    for (const role of ['it_admin', 'it_site_admin', 'engineering_admin', 'plant_engineering_manager', 'senior_engineering_manager']) {
      expect(roleRank(role), role).toBe(1)
    }
    for (const role of ['group_leader', 'engineer', 'technician', 'operator', 'manager', 'user', 'unknown', '', undefined]) {
      expect(roleRank(role), String(role)).toBe(0)
    }
    expect(HEIMDALL_ROLES.filter(r => roleRank(r) === 3)).toEqual(['system_admin'])
  })

  it('knows which roles administer users and falls back to the default role for display', () => {
    expect(HEIMDALL_ROLES.filter(isUserAdminRole)).toEqual(['system_admin', 'heimdall_admin', 'admin', 'engineering_admin'])
    expect(roleLabel('plant_director')).toBe('Plant Director')
    expect(roleLabel(undefined)).toBe('User')
    expect(roleDescription('plant_director')).toContain('Plant Director')
    expect(roleDescription('no_such_role')).toBe(ROLE_DESCRIPTIONS.user)
  })
})
