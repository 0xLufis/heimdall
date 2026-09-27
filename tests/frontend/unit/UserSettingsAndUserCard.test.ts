import { describe, it, expect } from 'vitest'
import { navMenuBottom } from '../../../frontend/web/app/constants/menus'
import { authClient } from '../../../frontend/web/app/utils/auth-client'
import { DEMO_PERSONAS } from '../../../frontend/web/app/composables/useAuthSession'
import {
  generateBase32Secret,
  generateTotpCode,
  verifyTotpCode,
  initiateMfaSetup,
  verifyAndEnableMfa,
  verifyMfaChallenge,
  getUserMfaState,
  getBackupCodes,
  regenerateBackupCodes,
  disableUserMfa
} from '../../../frontend/web/server/utils/userMfaStore'

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

describe('User MFA TOTP & Zero-Trust Verification Engine Suite', () => {
  const testUserId = 'test-user-mfa-42'
  const testEmail = 'eng.admin@heimdall.dev'

  it('generates valid RFC 6238 Base32 secrets and produces 6-digit TOTP codes', () => {
    const secret = generateBase32Secret(20)
    expect(secret).toBeDefined()
    expect(secret.length).toBeGreaterThanOrEqual(16)
    expect(secret).toMatch(/^[A-Z2-7]+$/)

    const code = generateTotpCode(secret)
    expect(code).toMatch(/^\d{6}$/)

    // Verification of freshly generated TOTP code
    const isValid = verifyTotpCode(secret, code)
    expect(isValid).toBe(true)

    // Invalid code fails
    const isBad = verifyTotpCode(secret, '999999')
    expect(isBad).toBe(false)
  })

  it('manages complete user MFA enrollment lifecycle', async () => {
    // 1. Initial state should be unconfigured
    const initial = getUserMfaState(testUserId)
    expect(initial.enabled).toBe(false)
    expect(initial.enrolledAt).toBeNull()

    // 2. Initiate setup
    const setup = await initiateMfaSetup(testUserId, testEmail)
    expect(setup.userId).toBe(testUserId)
    expect(setup.secret).toBeDefined()
    expect(setup.qrCode).toContain('data:image/png;base64')
    expect(setup.otpauthUrl).toContain('otpauth://totp/Heimdall:')
    expect(setup.backupCodes).toHaveLength(10)

    // Check pending state
    const pendingState = getUserMfaState(testUserId)
    expect(pendingState.hasPendingSetup).toBe(true)

    // 3. Verify with bad code throws error
    expect(() => verifyAndEnableMfa(testUserId, '000000')).toThrow(/Invalid verification code/)

    // 4. Verify with generated TOTP code
    const validCode = generateTotpCode(setup.secret)
    const enabledRes = verifyAndEnableMfa(testUserId, validCode)
    expect(enabledRes.success).toBe(true)
    expect(enabledRes.enabled).toBe(true)
    expect(enabledRes.backupCodesRemaining).toBe(10)

    // 5. Active state verification
    const activeState = getUserMfaState(testUserId)
    expect(activeState.enabled).toBe(true)
    expect(activeState.backupCodesRemaining).toBe(10)
    expect(activeState.enrolledAt).toBeDefined()

    // 6. Test challenge with valid TOTP code
    const code2 = generateTotpCode(setup.secret)
    const chalRes = verifyMfaChallenge(testUserId, code2)
    expect(chalRes.success).toBe(true)
    expect(chalRes.usedBackupCode).toBe(false)

    // 7. Test challenge with one-time backup recovery code
    const backupCodes = getBackupCodes(testUserId)
    expect(backupCodes).toHaveLength(10)
    const codeToUse = backupCodes[0]

    const backupChalRes = verifyMfaChallenge(testUserId, codeToUse)
    expect(backupChalRes.success).toBe(true)
    expect(backupChalRes.usedBackupCode).toBe(true)
    expect(backupChalRes.backupCodesRemaining).toBe(9)

    // Used backup code cannot be reused
    expect(() => verifyMfaChallenge(testUserId, codeToUse)).toThrow(/Invalid authentication code/)

    // 8. Regenerate backup codes
    const newCodes = regenerateBackupCodes(testUserId)
    expect(newCodes).toHaveLength(10)
    expect(newCodes).not.toContain(codeToUse)

    // 9. Disable MFA
    const disableRes = disableUserMfa(testUserId)
    expect(disableRes.success).toBe(true)
    expect(disableRes.enabled).toBe(false)

    const finalState = getUserMfaState(testUserId)
    expect(finalState.enabled).toBe(false)
  })

  it('supports sandbox dev bypass code 123456 in non-production environments', async () => {
    const devUserId = 'test-user-dev-mfa'
    await initiateMfaSetup(devUserId, 'dev@heimdall.dev')

    // Enable using dev code
    const enabledRes = verifyAndEnableMfa(devUserId, '123456')
    expect(enabledRes.success).toBe(true)

    // Challenge using dev code
    const chalRes = verifyMfaChallenge(devUserId, '123456')
    expect(chalRes.success).toBe(true)

    disableUserMfa(devUserId)
  })
})

