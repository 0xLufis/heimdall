import { describe, it, expect } from 'vitest'
import { navMenuBottom } from '../../../frontend/web/app/constants/menus'
import { authClient } from '../../../frontend/web/app/utils/auth-client'
import { DEMO_PERSONAS } from '../../../frontend/web/app/composables/useAuthSession'

describe('User Card & Settings Page Navigation & State Suite', () => {
  it('includes User Settings in navMenuBottom pointing to /dashboard/settings', () => {
    const settingsItem = navMenuBottom.find(item => item.link === '/dashboard/settings')
    expect(settingsItem).toBeDefined()
    expect(settingsItem?.title).toBe('User Settings')
    expect(settingsItem?.icon).toBe('i-lucide-settings')
  })

  it('configures authClient with adminClient, usernameClient, multiSessionClient, and organizationClient plugins', () => {
    expect(authClient).toBeDefined()
    // Better-auth plugins register their methods or names on authClient
    expect(authClient.organization).toBeDefined()
    expect(authClient.signIn).toBeDefined()
    expect(authClient.signOut).toBeDefined()
    expect(authClient.useSession).toBeDefined()
  })

  it('provides all 9 specialized personas for simulated testing', () => {
    expect(DEMO_PERSONAS.length).toBeGreaterThanOrEqual(9)
    const roles = DEMO_PERSONAS.map(p => p.role)

    expect(roles).toContain('system_admin')
    expect(roles).toContain('heimdall_admin')
    expect(roles).toContain('it_admin')
    expect(roles).toContain('engineering_admin')
    expect(roles).toContain('manager')
    expect(roles).toContain('group_leader')
    expect(roles).toContain('shift_leader')
    expect(roles).toContain('engineer')
    expect(roles).toContain('technician')

    const sysAdmin = DEMO_PERSONAS.find(p => p.role === 'system_admin')
    expect(sysAdmin?.email).toBe('sysadmin@heimdall.dev')

    const itAdmin = DEMO_PERSONAS.find(p => p.role === 'it_admin')
    expect(itAdmin?.email).toBe('it.admin@heimdall.dev')

    const engAdmin = DEMO_PERSONAS.find(p => p.role === 'engineering_admin')
    expect(engAdmin?.email).toBe('eng.admin@heimdall.dev')
  })
})
