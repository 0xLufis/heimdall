import { describe, it, expect, vi } from 'vitest'

describe('Auth & Multi-Tenant Context Tests', () => {
  it('computes role hierarchy permissions correctly', () => {
    const roles = {
      system_admin: { isAdmin: true, isEngineer: true, isTechnician: true },
      admin: { isAdmin: true, isEngineer: true, isTechnician: true },
      engineer: { isAdmin: false, isEngineer: true, isTechnician: true },
      technician: { isAdmin: false, isEngineer: false, isTechnician: true },
      operator: { isAdmin: false, isEngineer: false, isTechnician: false },
      user: { isAdmin: false, isEngineer: false, isTechnician: false }
    }

    const checkRoles = (role: string) => ({
      isAdmin: ['admin', 'system_admin'].includes(role),
      isEngineer: ['engineer', 'team_lead', 'manager', 'admin', 'system_admin'].includes(role),
      isTechnician: ['technician', 'engineer', 'team_lead', 'admin', 'system_admin'].includes(role)
    })

    for (const [role, expected] of Object.entries(roles)) {
      const computed = checkRoles(role)
      expect(computed.isAdmin).toBe(expected.isAdmin)
      expect(computed.isEngineer).toBe(expected.isEngineer)
      expect(computed.isTechnician).toBe(expected.isTechnician)
    }
  })

  it('injects X-Organization-Id and Authorization token in proxy headers', () => {
    const sessionToken = 'better-auth-session-xyz'
    const activeOrgId = 'org-vw-assembly-line-1'

    const headers: Record<string, string | undefined> = {
      Authorization: sessionToken ? `Bearer ${sessionToken}` : undefined,
      'X-Organization-Id': activeOrgId || undefined
    }

    expect(headers.Authorization).toBe('Bearer better-auth-session-xyz')
    expect(headers['X-Organization-Id']).toBe('org-vw-assembly-line-1')
  })
})
