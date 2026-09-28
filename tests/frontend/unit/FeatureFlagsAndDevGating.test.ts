import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import {
  getFeatureFlags,
  assertDevFeaturesEnabled,
  assertDebugFeaturesEnabled,
  featureFlags
} from '~~/server/utils/featureFlags'
import { getAppFeatureFlags, useFeatureFlags } from '~/composables/useFeatureFlags'
import { useAuthSession, DEMO_PERSONAS } from '~/composables/useAuthSession'
import adMockHandler from '~~/server/api/ad-mock/[...path]'
import simulatorHandler from '~~/server/api/simulator/[...action]'
import seedAdminHandler from '~~/server/api/dev/seed-admin.get'

describe('Frontend & Nitro Feature Flags Suite', () => {
  const originalEnv = { ...process.env }

  beforeEach(() => {
    process.env = { ...originalEnv }
  })

  afterEach(() => {
    process.env = { ...originalEnv }
  })

  describe('Server Feature Flags (featureFlags.ts)', () => {
    it('enables dev features in development/test by default', () => {
      process.env.NODE_ENV = 'development'
      delete process.env.HEIMDALL_ENABLE_DEV

      const flags = getFeatureFlags()
      expect(flags.enableDevFeatures).toBe(true)
      expect(featureFlags.enableDevFeatures).toBe(true)
    })

    it('disables dev features in production by default', () => {
      process.env.NODE_ENV = 'production'
      delete process.env.HEIMDALL_ENABLE_DEV

      const flags = getFeatureFlags()
      expect(flags.enableDevFeatures).toBe(false)
      expect(featureFlags.enableDevFeatures).toBe(false)
    })

    it('enables dev features in production if HEIMDALL_ENABLE_DEV is true', () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'true'

      const flags = getFeatureFlags()
      expect(flags.enableDevFeatures).toBe(true)
      expect(featureFlags.enableDevFeatures).toBe(true)
    })

    it('disables debug features by default in production and development', () => {
      delete process.env.HEIMDALL_ENABLE_DEBUG
      delete process.env.HEIMDALL_DEBUG

      process.env.NODE_ENV = 'production'
      expect(getFeatureFlags().enableDebugFeatures).toBe(false)

      process.env.NODE_ENV = 'development'
      expect(getFeatureFlags().enableDebugFeatures).toBe(false)
    })

    it('enables debug features if HEIMDALL_ENABLE_DEBUG or HEIMDALL_DEBUG is true', () => {
      process.env.HEIMDALL_ENABLE_DEBUG = 'true'
      expect(getFeatureFlags().enableDebugFeatures).toBe(true)

      delete process.env.HEIMDALL_ENABLE_DEBUG
      process.env.HEIMDALL_DEBUG = 'true'
      expect(getFeatureFlags().enableDebugFeatures).toBe(true)
    })

    it('assertDevFeaturesEnabled throws 403 when dev features are disabled', () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'

      expect(() => assertDevFeaturesEnabled()).toThrowError(/Forbidden: Development features are disabled/)
    })

    it('assertDevFeaturesEnabled does not throw when dev features are enabled', () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'

      expect(() => assertDevFeaturesEnabled()).not.toThrow()
    })

    it('assertDebugFeaturesEnabled throws 403 when debug features are disabled', () => {
      delete process.env.HEIMDALL_ENABLE_DEBUG
      delete process.env.HEIMDALL_DEBUG

      expect(() => assertDebugFeaturesEnabled()).toThrowError(/Forbidden: Debug features are disabled/)
    })

    it('assertDebugFeaturesEnabled does not throw when debug features are enabled', () => {
      process.env.HEIMDALL_ENABLE_DEBUG = 'true'

      expect(() => assertDebugFeaturesEnabled()).not.toThrow()
    })
  })

  describe('Dev Endpoint Gating (HTTP 403 Forbidden)', () => {
    it('rejects /api/ad-mock/* with 403 when dev features are disabled', async () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'

      const mockEvent = {
        context: { params: { path: 'v1.0/me/memberOf' } },
        method: 'GET'
      } as any

      await expect(adMockHandler(mockEvent)).rejects.toMatchObject({
        statusCode: 403
      })
    })

    it('rejects /api/simulator/* with 403 when dev features are disabled', async () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'

      const mockEvent = {
        context: { params: { action: 'status' } },
        method: 'GET'
      } as any

      await expect(simulatorHandler(mockEvent)).rejects.toMatchObject({
        statusCode: 403
      })
    })

    it('rejects /api/dev/seed-admin with 403 when dev features are disabled', async () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'

      const mockEvent = {} as any

      await expect(seedAdminHandler(mockEvent)).rejects.toMatchObject({
        statusCode: 403
      })
    })

    it('allows /api/ad-mock/* when dev features are enabled', async () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'

      const mockEvent = {
        context: { params: { path: 'v1.0/users' } },
        method: 'GET'
      } as any

      const response = await adMockHandler(mockEvent)
      expect(response).toBeDefined()
      expect((response as any).value).toBeInstanceOf(Array)
    })
  })

  describe('Frontend Composable (useFeatureFlags & useAuthSession Persona Spoofing)', () => {
    it('useFeatureFlags returns strongly-typed reactive flags', () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'
      process.env.HEIMDALL_ENABLE_DEBUG = 'false'

      const { enableDevFeatures, enableDebugFeatures } = useFeatureFlags()
      expect(enableDevFeatures.value).toBe(true)
      expect(enableDebugFeatures.value).toBe(false)
    })

    it('blocks simulated persona switching when dev and debug flags are disabled', () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'
      process.env.HEIMDALL_ENABLE_DEBUG = 'false'
      delete process.env.HEIMDALL_DEBUG

      const { setSimulatedPersona, simulatedPersona, clearSimulatedPersona, isPersonaSimulationAllowed } = useAuthSession()
      clearSimulatedPersona()

      expect(isPersonaSimulationAllowed.value).toBe(false)

      const targetPersona = DEMO_PERSONAS[1]
      setSimulatedPersona(targetPersona)

      // Persona spoofing should be completely blocked
      expect(simulatedPersona.value).toBeNull()
    })

    it('permits simulated persona switching when dev features are enabled', () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'

      const { setSimulatedPersona, simulatedPersona, clearSimulatedPersona, isPersonaSimulationAllowed } = useAuthSession()
      clearSimulatedPersona()

      expect(isPersonaSimulationAllowed.value).toBe(true)

      const targetPersona = DEMO_PERSONAS[2]
      setSimulatedPersona(targetPersona)

      expect(simulatedPersona.value).toEqual(targetPersona)
      clearSimulatedPersona()
      expect(simulatedPersona.value).toBeNull()
    })

    it('in staging mode (DEV=false, DEBUG=true), blocks dev bypass while allowing debug inspection', () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'
      process.env.HEIMDALL_ENABLE_DEBUG = 'true'

      const flags = getFeatureFlags()
      expect(flags.enableDevFeatures).toBe(false)
      expect(flags.enableDebugFeatures).toBe(true)

      expect(() => assertDevFeaturesEnabled()).toThrowError(/Forbidden: Development features are disabled/)
      expect(() => assertDebugFeaturesEnabled()).not.toThrow()
    })
  })
})
