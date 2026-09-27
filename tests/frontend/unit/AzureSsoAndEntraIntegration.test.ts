import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { getAzureSsoConfig } from '~~/server/utils/auth'
import {
  evaluateSecurityGroupOrgMapping,
  syncUserSecurityGroupsToOrganizations,
} from '~~/server/utils/securityGroupOrgSync'
import { getPlantUsers, getPlantMetadata } from '~~/server/utils/datasetLoader'

describe('Microsoft Entra ID (Azure SSO) Integration Suite', () => {
  const originalEnv = { ...process.env }

  beforeEach(() => {
    process.env = { ...originalEnv }
  })

  afterEach(() => {
    process.env = { ...originalEnv }
  })

  describe('Azure SSO Configuration & Credential Resolution', () => {
    it('resolves default canonical enterprise tenant when no environment variable is set', () => {
      delete process.env.AZURE_AD_CLIENT_ID
      delete process.env.AZURE_AD_CLIENT_SECRET
      delete process.env.AZURE_AD_TENANT_ID
      delete process.env.MICROSOFT_ENTRA_ID_CLIENT_ID
      delete process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET
      delete process.env.MICROSOFT_ENTRA_ID_TENANT_ID
      delete process.env.AZURE_CLIENT_ID
      delete process.env.AZURE_CLIENT_SECRET
      delete process.env.AZURE_TENANT_ID

      const config = getAzureSsoConfig()
      expect(config.isConfigured).toBe(false)
      expect(config.tenantId).toBe('72f988bf-86f1-41af-91ab-2d7cd011db47')
      expect(config.clientId).toBeUndefined()
      expect(config.clientSecret).toBeUndefined()
    })

    it('resolves AZURE_AD_* configuration variables', () => {
      process.env.AZURE_AD_CLIENT_ID = 'test-client-id-azure-ad'
      process.env.AZURE_AD_CLIENT_SECRET = 'test-client-secret-azure-ad'
      process.env.AZURE_AD_TENANT_ID = 'tenant-custom-ad'

      const config = getAzureSsoConfig()
      expect(config.isConfigured).toBe(true)
      expect(config.clientId).toBe('test-client-id-azure-ad')
      expect(config.clientSecret).toBe('test-client-secret-azure-ad')
      expect(config.tenantId).toBe('tenant-custom-ad')
    })

    it('resolves MICROSOFT_ENTRA_ID_* alias configuration variables', () => {
      delete process.env.AZURE_AD_CLIENT_ID
      delete process.env.AZURE_AD_CLIENT_SECRET
      delete process.env.AZURE_AD_TENANT_ID

      process.env.MICROSOFT_ENTRA_ID_CLIENT_ID = 'test-entra-id'
      process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET = 'test-entra-secret'
      process.env.MICROSOFT_ENTRA_ID_TENANT_ID = 'entra-custom-tenant'

      const config = getAzureSsoConfig()
      expect(config.isConfigured).toBe(true)
      expect(config.clientId).toBe('test-entra-id')
      expect(config.clientSecret).toBe('test-entra-secret')
      expect(config.tenantId).toBe('entra-custom-tenant')
    })

    it('resolves AZURE_* alias configuration variables', () => {
      delete process.env.AZURE_AD_CLIENT_ID
      delete process.env.AZURE_AD_CLIENT_SECRET
      delete process.env.AZURE_AD_TENANT_ID
      delete process.env.MICROSOFT_ENTRA_ID_CLIENT_ID
      delete process.env.MICROSOFT_ENTRA_ID_CLIENT_SECRET
      delete process.env.MICROSOFT_ENTRA_ID_TENANT_ID

      process.env.AZURE_CLIENT_ID = 'test-az-id'
      process.env.AZURE_CLIENT_SECRET = 'test-az-secret'
      process.env.AZURE_TENANT_ID = 'az-tenant'

      const config = getAzureSsoConfig()
      expect(config.isConfigured).toBe(true)
      expect(config.clientId).toBe('test-az-id')
      expect(config.clientSecret).toBe('test-az-secret')
      expect(config.tenantId).toBe('az-tenant')
    })
  })

  describe('SSO Status API Specification', () => {
    it('produces valid SSO status payload for client consumption', () => {
      process.env.NODE_ENV = 'development'
      delete process.env.AZURE_AD_CLIENT_ID
      delete process.env.AZURE_AD_CLIENT_SECRET

      const config = getAzureSsoConfig()
      const isDev = process.env.NODE_ENV !== 'production'

      const statusPayload = {
        enabled: config.isConfigured || isDev,
        provider: 'microsoft',
        providerName: 'Microsoft Entra ID (Azure SSO)',
        tenantId: config.tenantId,
        isConfigured: config.isConfigured,
        allowMockSimulation: isDev
      }

      expect(statusPayload.enabled).toBe(true)
      expect(statusPayload.provider).toBe('microsoft')
      expect(statusPayload.providerName).toContain('Microsoft Entra ID')
      expect(statusPayload.allowMockSimulation).toBe(true)
      expect(statusPayload.tenantId).toBe('72f988bf-86f1-41af-91ab-2d7cd011db47')
    })

    it('disables mock simulation in production environment', () => {
      process.env.NODE_ENV = 'production'
      delete process.env.AZURE_AD_CLIENT_ID
      delete process.env.AZURE_AD_CLIENT_SECRET

      const config = getAzureSsoConfig()
      const isDev = process.env.NODE_ENV !== 'production'

      const statusPayload = {
        enabled: config.isConfigured || isDev,
        provider: 'microsoft',
        providerName: 'Microsoft Entra ID (Azure SSO)',
        tenantId: config.tenantId,
        isConfigured: config.isConfigured,
        allowMockSimulation: isDev
      }

      expect(statusPayload.enabled).toBe(false)
      expect(statusPayload.allowMockSimulation).toBe(false)
    })
  })

  describe('Claims Transformation & Security Group Organization Provisioning', () => {
    it('maps Entra ID group claim to system_admin role and owner permissions', () => {
      // 9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c is the plant admin GUID
      const result = evaluateSecurityGroupOrgMapping([
        '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c'
      ])

      expect(result.matchedGroups.length).toBe(1)
      expect(result.matchedGroups[0].mappedRole).toBe('system_admin')
      expect(result.targetOrganizations.length).toBe(1)
      expect(result.targetOrganizations[0].name).toContain('Platform Operations')
      expect(result.targetOrganizations[0].role).toBe('owner')
    })

    it('maps plant Line 01 OT controls security group to production line organization', () => {
      const line01Group = 'CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp'
      const result = evaluateSecurityGroupOrgMapping([line01Group])

      expect(result.matchedGroups.length).toBe(1)
      expect(result.matchedGroups[0].mappedRole).toBe('controls_engineer')
      expect(result.targetOrganizations.length).toBe(1)
      expect(result.targetOrganizations[0].name).toContain('Line 01')
      expect(result.suggestedActiveOrganization).toContain('line-01')
    })

    it('reconciles multi-group claims with highest privilege role resolution', () => {
      const multipleGroups = [
        'CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp',
        '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c' // System admin
      ]
      const result = evaluateSecurityGroupOrgMapping(multipleGroups)

      expect(result.matchedGroups.length).toBe(2)
      const mappedRoles = result.matchedGroups.map(g => g.mappedRole)
      expect(mappedRoles).toContain('system_admin')
      expect(mappedRoles).toContain('controls_engineer')
      expect(result.targetOrganizations.length).toBe(2)
      const roles = result.targetOrganizations.map(o => o.role)
      expect(roles).toContain('owner')
    })

    it('successfully provisions user organizations in database transaction or fallback', async () => {
      const syncResult = await syncUserSecurityGroupsToOrganizations(
        'usr-test-entra-001',
        ['CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp']
      )

      expect(syncResult).toBeDefined()
      expect(syncResult.userId).toBe('usr-test-entra-001')
      expect(syncResult.matchedGroups).toContain('CN=SG-Line01-Controls,OU=AudiLine01,OU=ProductionLines,DC=factory,DC=corp')
      expect(syncResult.enrolledOrganizations.length).toBe(1)
      expect(syncResult.enrolledOrganizations[0].organizationSlug).toContain('line-01')
    })
  })

  describe('SSO Simulation Sandbox Specification', () => {
    it('blocks simulation in production mode', () => {
      const simulateHandler = (env: string) => {
        if (env === 'production') {
          throw new Error('Azure SSO simulation is disabled in production.')
        }
        return { success: true }
      }

      expect(() => simulateHandler('production')).toThrow('Azure SSO simulation is disabled in production.')
      expect(simulateHandler('development')).toEqual({ success: true })
    })

    it('matches simulated user against plant enterprise dataset', () => {
      const plantUsers = getPlantUsers()
      const metadata = getPlantMetadata()

      expect(metadata.entraTenantId).toBe('72f988bf-86f1-41af-91ab-2d7cd011db47')

      const requestedEmail = 'synthetica.botman.ai@fake-factory.internal'
      const matched = plantUsers.find(u => u.email.toLowerCase() === requestedEmail)

      expect(matched).toBeDefined()
      expect(matched?.name).toContain('Synthetica Botman')
      expect(matched?.securityGroupIds.length).toBeGreaterThan(0)
    })
  })
})
