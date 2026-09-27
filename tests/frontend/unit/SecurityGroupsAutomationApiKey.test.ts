import { describe, it, expect, beforeEach } from 'vitest'
import {
  generateAutomationApiKey,
  validateAutomationApiKey,
  revokeAutomationApiKey,
  deleteAutomationApiKey,
  listAutomationApiKeys,
  resetAutomationApiKeysStore,
  hashApiKey
} from '~~/server/utils/automationApiKeysStore'
import {
  createSecurityGroupMapping,
  getAllSecurityGroupMappings,
  getSecurityGroupMappingById,
  updateSecurityGroupMapping,
  deleteSecurityGroupMapping,
  resetSecurityGroupMappings
} from '~~/server/utils/securityGroupMappingsStore'
import { getTicketsStore } from '~~/server/utils/ticketsStore'

describe('Outside IT Automation & API Key Integration Suite', () => {
  beforeEach(() => {
    resetAutomationApiKeysStore()
    resetSecurityGroupMappings()
  })

  describe('API Key Cryptographic Store & Lifecycle', () => {
    it('seeds default development keys for ServiceNow and Jira', () => {
      const keys = listAutomationApiKeys()
      expect(keys.length).toBeGreaterThanOrEqual(2)

      const snKey = keys.find(k => k.systemType === 'ServiceNow')
      expect(snKey).toBeDefined()
      expect(snKey?.name).toContain('ServiceNow')
      expect(snKey?.isActive).toBe(true)
      expect(snKey?.keyPrefix).toContain('hmd_auto_service')

      const jiraKey = keys.find(k => k.systemType === 'Jira')
      expect(jiraKey).toBeDefined()
      expect(jiraKey?.name).toContain('Jira')
    })

    it('generates a new cryptographically secure API key with prefix and hash', () => {
      const result = generateAutomationApiKey({
        name: 'ServiceNow Production Change Runner',
        systemType: 'ServiceNow',
        scopes: ['security_groups:read', 'security_groups:write', 'tickets:create'],
        expiresInDays: 30,
        createdBy: 'it_admin',
        description: 'Auto-provisioning for change requests'
      })

      expect(result.rawKey).toMatch(/^hmd_auto_[0-9a-f]{48}$/)
      expect(result.apiKey.id).toBeDefined()
      expect(result.apiKey.name).toBe('ServiceNow Production Change Runner')
      expect(result.apiKey.keyPrefix).toBe(`${result.rawKey.slice(0, 16)}...${result.rawKey.slice(-4)}`)
      expect(result.apiKey.scopes).toEqual(['security_groups:read', 'security_groups:write', 'tickets:create'])
      expect(result.apiKey.isActive).toBe(true)
      expect(result.apiKey.expiresAt).toBeDefined()
    })

    it('validates a valid API key and updates lastUsedAt', () => {
      const { rawKey, apiKey } = generateAutomationApiKey({
        name: 'Jira Service Management',
        systemType: 'Jira',
        scopes: ['security_groups:read', 'security_groups:write']
      })

      const validated = validateAutomationApiKey(rawKey, 'security_groups:read')
      expect(validated).toBeDefined()
      expect(validated?.id).toBe(apiKey.id)
      expect(validated?.lastUsedAt).toBeDefined()
    })

    it('rejects an invalid or tampered API key', () => {
      const validated = validateAutomationApiKey('hmd_auto_completely_invalid_key')
      expect(validated).toBeNull()
    })

    it('rejects an API key when required scope is missing', () => {
      const { rawKey } = generateAutomationApiKey({
        name: 'Read Only Auditor',
        scopes: ['security_groups:read']
      })

      const writeCheck = validateAutomationApiKey(rawKey, 'security_groups:write')
      expect(writeCheck).toBeNull()

      const readCheck = validateAutomationApiKey(rawKey, 'security_groups:read')
      expect(readCheck).toBeDefined()
    })

    it('rejects revoked or deactivated API keys', () => {
      const { rawKey, apiKey } = generateAutomationApiKey({
        name: 'Temporary Script',
        scopes: ['security_groups:read']
      })

      expect(validateAutomationApiKey(rawKey)).toBeDefined()

      const revoked = revokeAutomationApiKey(apiKey.id)
      expect(revoked).toBe(true)

      expect(validateAutomationApiKey(rawKey)).toBeNull()
    })

    it('rejects expired API keys', () => {
      const { rawKey } = generateAutomationApiKey({
        name: 'Expired Key',
        expiresInDays: -1 // Expired in past
      })

      expect(validateAutomationApiKey(rawKey)).toBeNull()
    })
  })

  describe('Security Group Mapping Store & Ticketing Cross-Link', () => {
    it('seeds initial default group mappings', () => {
      const mappings = getAllSecurityGroupMappings()
      expect(mappings.length).toBeGreaterThanOrEqual(3)

      const adminMapping = mappings.find(m => m.mappedRole === 'admin')
      expect(adminMapping).toBeDefined()
      expect(adminMapping?.displayName).toBe('OT Plant Administrators')
    })

    it('provisions mapping via external ticketing automation and auto-creates audit ticket', () => {
      const initialTicketsCount = getTicketsStore().length

      const { mapping, auditTicket } = createSecurityGroupMapping({
        identityProvider: 'EntraID',
        groupIdentifier: '8f7e6d5c-4b3a-2a1f-0e9d-8c7b6a5f4e3d',
        displayName: 'Line 06 Battery Controls Engineers',
        mappedRole: 'controls_engineer',
        organizationId: 'Line 06 – Battery Module Line',
        sourceTicketId: 'INC0091823',
        sourceSystem: 'ServiceNow',
        requestedBy: 'sally.vance@factory.corp',
        approvedBy: 'it.director@factory.corp',
        reason: 'Emergency line 06 troubleshooting delegation',
        autoCreateAuditTicket: true,
        apiKeyName: 'ServiceNow Production Change Runner'
      })

      expect(mapping.id).toBeDefined()
      expect(mapping.sourceTicketId).toBe('INC0091823')
      expect(mapping.sourceSystem).toBe('ServiceNow')
      expect(mapping.requestedBy).toBe('sally.vance@factory.corp')

      // Verify mapping in store
      const retrieved = getSecurityGroupMappingById(mapping.id)
      expect(retrieved).toBeDefined()
      expect(retrieved?.displayName).toBe('Line 06 Battery Controls Engineers')

      // Verify Heimdall Audit Governance Ticket creation
      expect(auditTicket).toBeDefined()
      expect(auditTicket?.ticketNumber).toBe('TKT-GOV-INC0091823')
      expect(auditTicket?.status).toBe('Resolved')
      expect(auditTicket?.title).toContain('Security Group Mapping: Line 06 Battery Controls Engineers')
      expect(auditTicket?.description).toContain('INC0091823')
      expect(auditTicket?.tags).toContain('ServiceNow')
      expect(auditTicket?.tags).toContain('INC0091823')

      const ticketsAfter = getTicketsStore()
      expect(ticketsAfter.length).toBe(initialTicketsCount + 1)
      const foundInStore = ticketsAfter.find(t => t.ticketNumber === 'TKT-GOV-INC0091823')
      expect(foundInStore).toBeDefined()
    })

    it('updates an existing mapping via ticketing automation', () => {
      const { mapping } = createSecurityGroupMapping({
        groupIdentifier: 'test-group-guid-1234',
        displayName: 'Test Maintenance Group',
        mappedRole: 'technician'
      })

      const updated = updateSecurityGroupMapping(mapping.id, {
        mappedRole: 'lead_engineer',
        sourceTicketId: 'CHG0049102',
        reason: 'Promotion to lead engineer role'
      })

      expect(updated?.mappedRole).toBe('lead_engineer')
      expect(updated?.sourceTicketId).toBe('CHG0049102')
    })

    it('deletes a mapping via ticketing deprovisioning', () => {
      const { mapping } = createSecurityGroupMapping({
        groupIdentifier: 'to-be-deleted-group',
        displayName: 'Temporary Group',
        mappedRole: 'operator'
      })

      expect(getSecurityGroupMappingById(mapping.id)).toBeDefined()
      const deleted = deleteSecurityGroupMapping(mapping.id)
      expect(deleted).toBe(true)
      expect(getSecurityGroupMappingById(mapping.id)).toBeNull()
    })
  })

  describe('API Key Authentication Middleware (verifyAutomationApiKey)', () => {
    it('authenticates request with valid X-API-Key header', async () => {
      const { verifyAutomationApiKey } = await import('~~/server/utils/verifyApiKey')
      const { rawKey } = generateAutomationApiKey({
        name: 'Header Test Key',
        scopes: ['security_groups:read']
      })

      const mockEvent: any = {
        headers: new Headers({
          'x-api-key': rawKey
        }),
        context: {}
      }

      const key = verifyAutomationApiKey(mockEvent, 'security_groups:read')
      expect(key).toBeDefined()
      expect(key.name).toBe('Header Test Key')
      expect(mockEvent.context.automationKey).toBeDefined()
    })

    it('authenticates request with valid Authorization Bearer header', async () => {
      const { verifyAutomationApiKey } = await import('~~/server/utils/verifyApiKey')
      const { rawKey } = generateAutomationApiKey({
        name: 'Bearer Test Key',
        scopes: ['security_groups:write']
      })

      const mockEvent: any = {
        headers: new Headers({
          authorization: `Bearer ${rawKey}`
        }),
        context: {}
      }

      const key = verifyAutomationApiKey(mockEvent, 'security_groups:write')
      expect(key).toBeDefined()
      expect(key.name).toBe('Bearer Test Key')
    })

    it('throws 401 when API key is missing from headers', async () => {
      const { verifyAutomationApiKey } = await import('~~/server/utils/verifyApiKey')
      const mockEvent: any = {
        headers: new Headers(),
        context: {}
      }

      expect(() => verifyAutomationApiKey(mockEvent)).toThrowError(/Missing API Key/)
    })

    it('throws 403 when API key lacks required scope', async () => {
      const { verifyAutomationApiKey } = await import('~~/server/utils/verifyApiKey')
      const { rawKey } = generateAutomationApiKey({
        name: 'Limited Scope Key',
        scopes: ['security_groups:read']
      })

      const mockEvent: any = {
        headers: new Headers({
          'x-api-key': rawKey
        }),
        context: {}
      }

      expect(() => verifyAutomationApiKey(mockEvent, 'security_groups:write')).toThrowError(/lacks the required scope/)
    })
  })
})

