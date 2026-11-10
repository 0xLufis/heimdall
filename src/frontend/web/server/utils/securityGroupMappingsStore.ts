import { randomUUID } from 'node:crypto'
import { getPlantSecurityGroups } from './datasetLoader'
import { addTicketToStore, MaintenanceTicket } from './ticketsStore'
import { ensureBetterAuthProfile } from './authProfiles'

export interface SecurityGroupMapping {
  id: string
  identityProvider: string
  groupIdentifier: string
  displayName: string
  mappedRole: string
  organizationId?: string | null
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  sourceTicketId?: string | null
  sourceSystem?: string | null
  requestedBy?: string | null
  approvedBy?: string | null
  reason?: string | null
}

const mappingsStore = new Map<string, SecurityGroupMapping>()

/**
 * Initialize default security group mappings from plant dataset
 */
function initializeMappings() {
  if (mappingsStore.size > 0) return

  // Seed default mapping entries
  const defaults: SecurityGroupMapping[] = [
    {
      id: '1',
      identityProvider: 'EntraID',
      groupIdentifier: '9a2f1c8e-3d4b-4f5a-8b1c-7e6d5a4f3b2c',
      displayName: 'OT Plant Administrators',
      mappedRole: 'admin',
      organizationId: null,
      isEnabled: true,
      createdAt: '2026-08-15T08:00:00Z',
      updatedAt: '2026-08-15T08:00:00Z',
      sourceSystem: 'System Initialization',
      reason: 'Root enterprise plant administration group'
    },
    {
      id: '2',
      identityProvider: 'ActiveDirectory',
      groupIdentifier: 'CN=OT-Controls-Engineers,OU=Groups,DC=factory,DC=corp',
      displayName: 'On-Prem Controls Engineers',
      mappedRole: 'engineer',
      organizationId: 'Production Floor B',
      isEnabled: true,
      createdAt: '2026-08-18T10:30:00Z',
      updatedAt: '2026-08-18T10:30:00Z',
      sourceTicketId: 'CHG-002914',
      sourceSystem: 'ServiceNow',
      requestedBy: 'lead.controls@factory.corp',
      reason: 'Automation line controls engineering group'
    },
    {
      id: '3',
      identityProvider: 'ActiveDirectory',
      groupIdentifier: 'CN=OT-Maintenance-Technicians,OU=Groups,DC=factory,DC=corp',
      displayName: 'Plant Maintenance Technicians',
      mappedRole: 'technician',
      organizationId: null,
      isEnabled: true,
      createdAt: '2026-08-20T14:15:00Z',
      updatedAt: '2026-08-20T14:15:00Z',
      sourceTicketId: 'JIRA-1092',
      sourceSystem: 'Jira Service Management',
      requestedBy: 'maintenance.sup@factory.corp',
      reason: 'Shift maintenance and telemetry monitoring team'
    }
  ]

  // Add items from dataset loader if available
  try {
    const datasetGroups = getPlantSecurityGroups()
    if (datasetGroups && datasetGroups.length > 0) {
      for (const dg of datasetGroups) {
        if (!defaults.some(d => d.groupIdentifier.toLowerCase() === dg.groupIdentifier.toLowerCase())) {
          defaults.push({
            id: dg.id || randomUUID(),
            identityProvider: dg.identityProvider || 'ActiveDirectory',
            groupIdentifier: dg.groupIdentifier,
            displayName: dg.displayName,
            mappedRole: dg.mappedRole,
            organizationId: dg.mappedOrganizationName || null,
            isEnabled: dg.isEnabled !== false,
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString()
          })
        }
      }
    }
  } catch {
    // If dataset cannot be loaded in standalone/test mode, defaults are already seeded
  }

  for (const item of defaults) {
    mappingsStore.set(item.id, item)
  }
}

// Initial seed
initializeMappings()

export function getAllSecurityGroupMappings(): SecurityGroupMapping[] {
  initializeMappings()
  return Array.from(mappingsStore.values()).sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  )
}

export function getSecurityGroupMappingById(id: string): SecurityGroupMapping | null {
  initializeMappings()
  return mappingsStore.get(id) || null
}

export interface CreateMappingInput {
  identityProvider?: string
  groupIdentifier: string
  displayName: string
  mappedRole: string
  organizationId?: string | null
  isEnabled?: boolean
  sourceTicketId?: string | null
  sourceSystem?: string | null
  requestedBy?: string | null
  approvedBy?: string | null
  reason?: string | null
  autoCreateAuditTicket?: boolean
  apiKeyName?: string
}

export function createSecurityGroupMapping(input: CreateMappingInput): {
  mapping: SecurityGroupMapping
  auditTicket?: MaintenanceTicket
} {
  initializeMappings()

  // Check if mapping for this group already exists
  const existing = Array.from(mappingsStore.values()).find(
    m => m.groupIdentifier.toLowerCase() === input.groupIdentifier.trim().toLowerCase()
  )

  const now = new Date().toISOString()
  const id = existing ? existing.id : randomUUID()

  const mapping: SecurityGroupMapping = {
    id,
    identityProvider: input.identityProvider || 'EntraID',
    groupIdentifier: input.groupIdentifier.trim(),
    displayName: input.displayName.trim(),
    mappedRole: input.mappedRole.trim(),
    organizationId: input.organizationId?.trim() || null,
    isEnabled: input.isEnabled !== false,
    createdAt: existing ? existing.createdAt : now,
    updatedAt: now,
    sourceTicketId: input.sourceTicketId?.trim() || null,
    sourceSystem: input.sourceSystem?.trim() || null,
    requestedBy: input.requestedBy?.trim() || null,
    approvedBy: input.approvedBy?.trim() || null,
    reason: input.reason?.trim() || null
  }

  mappingsStore.set(id, mapping)

  if (mapping.requestedBy) {
    ensureBetterAuthProfile(mapping.requestedBy, { role: mapping.mappedRole }).catch(() => {})
  }
  if (mapping.approvedBy) {
    ensureBetterAuthProfile(mapping.approvedBy, { role: 'it_admin' }).catch(() => {})
  }

  let auditTicket: MaintenanceTicket | undefined

  // Auto-generate a governance maintenance ticket if ticketId is provided or requested
  if (input.autoCreateAuditTicket !== false && (input.sourceTicketId || input.sourceSystem)) {
    const ticketRef = input.sourceTicketId || `AUTO-${Math.floor(1000 + Math.random() * 9000)}`
    const cleanRef = ticketRef.replace(/[^a-zA-Z0-9_-]/g, '')
    const tktId = `tkt-gov-${Date.now()}`

    auditTicket = {
      id: tktId,
      ticketNumber: `TKT-GOV-${cleanRef}`,
      stationId: 'IT-SECURITY-GOVERNANCE',
      stationName: 'Active Directory & Entra ID Identity Management',
      title: `[IT Automation] Security Group Mapping: ${mapping.displayName} (${mapping.mappedRole})`,
      description: `Automated access provisioning from external ticketing system (${input.sourceSystem || 'Outside IT Automation'}).\n\nTicket Reference: ${ticketRef}\nRequester: ${input.requestedBy || 'Outside IT Automation'}\nApprover: ${input.approvedBy || 'Enterprise IT Workflow'}\nReason: ${input.reason || 'Directory security group mapped to Heimdall role'}\nGroup Identifier: ${mapping.groupIdentifier}\nMapped Role: ${mapping.mappedRole}\nTarget Organization: ${mapping.organizationId || 'Global (All Floors)'}`,
      status: 'Resolved',
      priority: 'Medium',
      category: 'Improvement',
      tags: ['IT-Automation', 'Security-Group', input.sourceSystem || 'Ticketing', ticketRef],
      reportedByUserId: 'usr-automation-bot',
      reportedByUserName: input.requestedBy || `${input.sourceSystem || 'Outside IT'} Automation Service`,
      createdAt: now,
      updatedAt: now,
      slaDueAt: now,
      resolvedAt: now,
      comments: [
        {
          id: `comment-${Date.now()}`,
          ticketId: tktId,
          authorUserId: 'usr-automation-bot',
          authorName: `${input.sourceSystem || 'Outside IT'} Automation Bot`,
          content: `Security group mapping '${mapping.groupIdentifier}' registered with role '${mapping.mappedRole}' via API key '${input.apiKeyName || 'Automation Key'}'.`,
          createdAt: now
        }
      ],
      attachments: [],
      metadata: {
        externalTicketId: ticketRef,
        sourceSystem: input.sourceSystem || 'Ticketing Automation',
        groupIdentifier: mapping.groupIdentifier,
        mappedRole: mapping.mappedRole,
        organizationId: mapping.organizationId
      }
    }

    try {
      addTicketToStore(auditTicket)
    } catch (e) {
      console.warn('[SecurityGroupMappingsStore] Failed to write audit ticket:', e)
    }
  }

  return { mapping, auditTicket }
}

export function updateSecurityGroupMapping(
  id: string,
  updates: Partial<SecurityGroupMapping>
): SecurityGroupMapping | null {
  initializeMappings()
  const existing = mappingsStore.get(id)
  if (!existing) return null

  const updated: SecurityGroupMapping = {
    ...existing,
    ...updates,
    id: existing.id,
    createdAt: existing.createdAt,
    updatedAt: new Date().toISOString()
  }

  mappingsStore.set(id, updated)
  return updated
}

export function toggleSecurityGroupMapping(id: string): SecurityGroupMapping | null {
  initializeMappings()
  const existing = mappingsStore.get(id)
  if (!existing) return null

  existing.isEnabled = !existing.isEnabled
  existing.updatedAt = new Date().toISOString()
  return existing
}

export function deleteSecurityGroupMapping(id: string): boolean {
  initializeMappings()
  return mappingsStore.delete(id)
}

export function resetSecurityGroupMappings() {
  mappingsStore.clear()
  initializeMappings()
}
