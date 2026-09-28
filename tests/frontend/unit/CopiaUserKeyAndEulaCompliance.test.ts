import { describe, it, expect, beforeEach } from 'vitest'
import {
  getUserCopiaKey,
  setUserCopiaKey,
  clearUserCopiaKey,
  maskApiKey
} from '~~/server/utils/copiaUserKeyStore'
import copiaKeyGetHandler from '~~/server/api/user/copia-key.get'
import copiaKeyPostHandler from '~~/server/api/user/copia-key.post'
import copiaSyncPostHandler from '~~/server/api/integrations/copia/sync.post'
import pendingSyncsGetHandler from '~~/server/api/integrations/copia/pending-syncs.get'

describe('Copia User-Specific API Keys & EULA Compliance Suite', () => {
  const testUserId = 'usr-engineer-01'

  beforeEach(() => {
    clearUserCopiaKey(testUserId)
  })

  describe('User Copia Key Store (copiaUserKeyStore.ts)', () => {
    it('saves and retrieves personal API key', () => {
      setUserCopiaKey(testUserId, 'copia_pat_valid_personal_key_1234')
      expect(getUserCopiaKey(testUserId)).toBe('copia_pat_valid_personal_key_1234')
    })

    it('rejects generic service account keys with EULA compliance error', () => {
      expect(() => setUserCopiaKey(testUserId, 'service-user')).toThrowError(/EULA Compliance Violation/)
      expect(() => setUserCopiaKey(testUserId, 'heimdall-probe')).toThrowError(/EULA Compliance Violation/)
      expect(() => setUserCopiaKey(testUserId, 'shared-token-plant')).toThrowError(/EULA Compliance Violation/)
    })

    it('masks personal API keys safely', () => {
      expect(maskApiKey('copia_pat_998877665544332211')).toBe('copi••••••••2211')
      expect(maskApiKey('')).toBe('')
    })
  })

  describe('Copia Key API Endpoints', () => {
    it('GET /api/user/copia-key reports hasKey: false when key not configured', async () => {
      const event = {
        context: { user: { id: testUserId } },
        node: { req: { url: `/api/user/copia-key?userId=${testUserId}` } }
      } as any

      const res = await copiaKeyGetHandler(event)
      expect(res.hasKey).toBe(false)
      expect(res.maskedKey).toBeNull()
    })

    it('POST /api/user/copia-key rejects generic service key with 400', async () => {
      const event = {
        context: { user: { id: testUserId } },
        node: { req: { method: 'POST', url: '/api/user/copia-key' } },
        _body: { userId: testUserId, apiKey: 'service-user' }
      } as any

      await expect(copiaKeyPostHandler(event)).rejects.toMatchObject({
        statusCode: 400
      })
    })

    it('POST /api/user/copia-key saves personal key and returns masked representation', async () => {
      const event = {
        context: { user: { id: testUserId } },
        node: { req: { method: 'POST', url: '/api/user/copia-key' } },
        _body: { userId: testUserId, apiKey: 'copia_pat_engineer_secret_key_8899' }
      } as any

      const res = await copiaKeyPostHandler(event)
      expect(res.success).toBe(true)
      expect(res.hasKey).toBe(true)
      expect(res.maskedKey).toContain('••••••••')
      expect(getUserCopiaKey(testUserId)).toBe('copia_pat_engineer_secret_key_8899')
    })
  })

  describe('Copia Cloud Sync Gating (EULA Anti-Pooling Enforcement)', () => {
    it('rejects sync without user API key with 400 EULA compliance error', async () => {
      clearUserCopiaKey(testUserId)

      const event = {
        context: { user: { id: testUserId, email: 'tech@plant.com' } },
        node: { req: { method: 'POST', url: '/api/integrations/copia/sync' } },
        _body: { syncId: 'sync-test-01', userId: testUserId }
      } as any

      await expect(copiaSyncPostHandler(event)).rejects.toMatchObject({
        statusCode: 400
      })
    })

    it('rejects sync with service user or shared key with 400 EULA compliance error', async () => {
      const event = {
        context: { user: { id: testUserId, email: 'tech@plant.com' } },
        node: { req: { method: 'POST', url: '/api/integrations/copia/sync' } },
        _body: { syncId: 'sync-test-01', userId: testUserId, apiKey: 'shared-pool-key' }
      } as any

      await expect(copiaSyncPostHandler(event)).rejects.toMatchObject({
        statusCode: 400
      })
    })

    it('succeeds when user-specific personal key is provided', async () => {
      const event = {
        context: { user: { id: testUserId, email: 'tech@plant.com', name: 'Alex' } },
        node: { req: { method: 'POST', url: '/api/integrations/copia/sync' } },
        _body: {
          syncId: 'sync-test-01',
          userId: testUserId,
          apiKey: 'copia_pat_personal_alex_99',
          userEmail: 'alex@plant.com'
        }
      } as any

      const res = await copiaSyncPostHandler(event)
      expect(res.success).toBe(true)
      expect(res.userEmail).toBe('alex@plant.com')
      expect(res.cloudCommitHash).toBeDefined()
    })

    it('GET /api/integrations/copia/pending-syncs returns pending sync list', async () => {
      const event = {
        context: {},
        headers: {},
        node: { req: { url: '/api/integrations/copia/pending-syncs' } }
      } as any

      const res = await pendingSyncsGetHandler(event)
      expect(res.pendingSyncs).toBeInstanceOf(Array)
      expect(res.pendingSyncs[0].serviceUserAuthor).toContain('heimdall-probe')
    })
  })
})
