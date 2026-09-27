import { randomBytes, createHash } from 'node:crypto'

export interface AutomationApiKey {
  id: string
  name: string
  keyPrefix: string // e.g. "hmd_auto_9a2f..."
  hashedKey: string // SHA-256 hash of the full secret token
  scopes: string[]
  systemType: 'Jira' | 'ServiceNow' | 'Ansible' | 'PowerAutomate' | 'Custom'
  createdBy: string
  createdAt: string
  expiresAt: string | null
  lastUsedAt: string | null
  isActive: boolean
  description?: string
}

export type SafeAutomationApiKey = Omit<AutomationApiKey, 'hashedKey'>

export const DEFAULT_AUTOMATION_SCOPES = [
  'security_groups:read',
  'security_groups:write',
  'security_groups:evaluate',
  'tickets:create'
]

// In-memory key store with persistent singleton lifecycle across server requests
const apiKeyStore = new Map<string, AutomationApiKey>()

export function hashApiKey(rawKey: string): string {
  return createHash('sha256').update(rawKey.trim()).digest('hex')
}

/**
 * Seed initial sample API keys for development and testing
 */
function initializeDefaultApiKeys() {
  if (apiKeyStore.size > 0) return

  // 1. ServiceNow ITSM Production Connector Key
  const snRawKey = 'hmd_auto_servicenow_demo_9a8b7c6d5e4f3a2b1c'
  const snKeyId = 'key-servicenow-default-01'
  apiKeyStore.set(snKeyId, {
    id: snKeyId,
    name: 'ServiceNow ITSM - Access Request Automation',
    keyPrefix: `${snRawKey.slice(0, 16)}...${snRawKey.slice(-4)}`,
    hashedKey: hashApiKey(snRawKey),
    scopes: [...DEFAULT_AUTOMATION_SCOPES],
    systemType: 'ServiceNow',
    createdBy: 'system_admin',
    createdAt: new Date('2026-09-01T08:00:00Z').toISOString(),
    expiresAt: null,
    lastUsedAt: new Date().toISOString(),
    isActive: true,
    description: 'Automated provisioning of OT security group mappings upon ServiceNow incident/change approval.'
  })

  // 2. Jira Service Management Connector Key
  const jiraRawKey = 'hmd_auto_jira_demo_1a2b3c4d5e6f7a8b9c'
  const jiraKeyId = 'key-jira-default-02'
  apiKeyStore.set(jiraKeyId, {
    id: jiraKeyId,
    name: 'Jira Service Management - Delegation Webhook',
    keyPrefix: `${jiraRawKey.slice(0, 16)}...${jiraRawKey.slice(-4)}`,
    hashedKey: hashApiKey(jiraRawKey),
    scopes: ['security_groups:read', 'security_groups:write', 'security_groups:evaluate', 'tickets:create'],
    systemType: 'Jira',
    createdBy: 'it_admin',
    createdAt: new Date('2026-09-10T10:00:00Z').toISOString(),
    expiresAt: null,
    lastUsedAt: null,
    isActive: true,
    description: 'External webhook triggering directory group mapping updates for factory line support.'
  })
}

// Ensure defaults are populated
initializeDefaultApiKeys()

/**
 * List all registered API keys without exposing hashed secrets
 */
export function listAutomationApiKeys(): SafeAutomationApiKey[] {
  initializeDefaultApiKeys()
  return Array.from(apiKeyStore.values())
    .map(({ hashedKey, ...safe }) => safe)
    .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
}

/**
 * Get an API key record by ID (sanitized)
 */
export function getAutomationApiKeyById(id: string): SafeAutomationApiKey | null {
  initializeDefaultApiKeys()
  const key = apiKeyStore.get(id)
  if (!key) return null
  const { hashedKey, ...safe } = key
  return safe
}

export interface CreateApiKeyInput {
  name: string
  systemType?: 'Jira' | 'ServiceNow' | 'Ansible' | 'PowerAutomate' | 'Custom'
  scopes?: string[]
  expiresInDays?: number | null
  createdBy?: string
  description?: string
  customRawKey?: string // For deterministic unit test seeding
}

/**
 * Generate a new cryptographically secure API key
 */
export function generateAutomationApiKey(input: CreateApiKeyInput): {
  apiKey: SafeAutomationApiKey
  rawKey: string
} {
  initializeDefaultApiKeys()

  const id = `key-auto-${Date.now()}-${randomBytes(4).toString('hex')}`
  const rawKey = input.customRawKey || `hmd_auto_${randomBytes(24).toString('hex')}`
  const hashedKey = hashApiKey(rawKey)
  const keyPrefix = `${rawKey.slice(0, 16)}...${rawKey.slice(-4)}`

  let expiresAt: string | null = null
  if (typeof input.expiresInDays === 'number') {
    const d = new Date()
    d.setDate(d.getDate() + input.expiresInDays)
    expiresAt = d.toISOString()
  }

  const record: AutomationApiKey = {
    id,
    name: input.name.trim(),
    keyPrefix,
    hashedKey,
    scopes: input.scopes && input.scopes.length > 0 ? input.scopes : [...DEFAULT_AUTOMATION_SCOPES],
    systemType: input.systemType || 'Custom',
    createdBy: input.createdBy || 'it_admin',
    createdAt: new Date().toISOString(),
    expiresAt,
    lastUsedAt: null,
    isActive: true,
    description: input.description
  }

  apiKeyStore.set(id, record)

  const { hashedKey: _, ...safe } = record
  return {
    apiKey: safe,
    rawKey
  }
}

/**
 * Revoke or deactivate an API key
 */
export function revokeAutomationApiKey(id: string): boolean {
  initializeDefaultApiKeys()
  const existing = apiKeyStore.get(id)
  if (!existing) return false
  existing.isActive = false
  return true
}

/**
 * Hard delete an API key
 */
export function deleteAutomationApiKey(id: string): boolean {
  initializeDefaultApiKeys()
  return apiKeyStore.delete(id)
}

/**
 * Validate an incoming API key string, checking active status, expiration, and scopes.
 * If valid, updates lastUsedAt and returns the key record.
 */
export function validateAutomationApiKey(
  rawKey: string,
  requiredScope?: string
): AutomationApiKey | null {
  if (!rawKey || typeof rawKey !== 'string') return null
  initializeDefaultApiKeys()

  const trimmed = rawKey.trim()
  const hashed = hashApiKey(trimmed)

  for (const record of apiKeyStore.values()) {
    if (record.hashedKey === hashed) {
      if (!record.isActive) {
        return null // Revoked
      }
      if (record.expiresAt && new Date(record.expiresAt).getTime() < Date.now()) {
        return null // Expired
      }
      if (requiredScope && !record.scopes.includes(requiredScope) && !record.scopes.includes('*') && !record.scopes.includes('admin')) {
        return null // Missing required permission scope
      }

      // Update lastUsedAt timestamp
      record.lastUsedAt = new Date().toISOString()
      return record
    }
  }

  return null
}

/**
 * Reset store (for testing)
 */
export function resetAutomationApiKeysStore() {
  apiKeyStore.clear()
  initializeDefaultApiKeys()
}
