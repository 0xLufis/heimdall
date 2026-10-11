import { defineEventHandler, getQuery } from 'h3'
import { getTicketsStore } from '../../utils/ticketsStore'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export interface UnifiedAuditLogEntry {
  id: string
  timestamp: string
  action: 'CREATE' | 'UPDATE' | 'DELETE' | 'ESCALATION' | 'STATUS_CHANGE' | 'DECOMMISSION' | 'DISPATCH' | 'CONFIG_CHANGE'
  entityType: 'Machine' | 'Ticket' | 'InventoryItem' | 'SecurityGroup' | 'ClientPc' | 'SystemPolicy'
  entityId: string
  entityName?: string
  userId: string
  userName: string
  summary: string
  severity: 'info' | 'warning' | 'critical'
  details?: Record<string, any>
}

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const entityTypeFilter = (query.entityType as string || 'all').trim()
  const minSeverity = (query.minSeverity as string || '').toLowerCase().trim()
  const searchQuery = (query.query as string || '').toLowerCase().trim()
  const limit = Math.min(100, Math.max(10, parseInt(query.limit as string || '50', 10)))

  const entries: UnifiedAuditLogEntry[] = []

  // 1. Ingest ticket audit & history records
  const allTickets = getTicketsStore()
  for (const t of allTickets) {
    if (t.isEscalated && t.escalatedAt) {
      entries.push({
        id: `aud-esc-${t.id}`,
        timestamp: t.escalatedAt,
        action: 'ESCALATION',
        entityType: 'Ticket',
        entityId: t.id,
        entityName: t.ticketNumber || t.title,
        userId: t.reportedByUserId || 'usr-tech-01',
        userName: t.escalatedBy || 'Technician User',
        summary: `Escalated ticket ${t.ticketNumber} to ${t.escalationTarget || 'DedicatedEngineer'} (${t.escalationHandoverState || 'HandOff'}): ${t.escalationReason || 'Operational escalation'}`,
        severity: 'warning',
        details: {
          handoverState: t.escalationHandoverState,
          target: t.escalationTarget,
          reason: t.escalationReason
        }
      })
    }

    if (t.changeHistory && Array.isArray(t.changeHistory)) {
      for (const ch of t.changeHistory) {
        entries.push({
          id: ch.id || `aud-chg-${t.id}-${ch.timestamp}`,
          timestamp: ch.timestamp,
          action: 'UPDATE',
          entityType: 'Ticket',
          entityId: t.id,
          entityName: t.ticketNumber,
          userId: 'usr-tech-01',
          userName: ch.changedBy || 'System Administrator',
          summary: `Updated field '${ch.field}' on ticket ${t.ticketNumber}: ${String(ch.oldValue ?? '')} ➔ ${String(ch.newValue ?? '')}`,
          severity: ch.field === 'priority' && ch.newValue === 'Critical' ? 'critical' : 'info',
          details: {
            field: ch.field,
            oldValue: ch.oldValue,
            newValue: ch.newValue,
            note: ch.note
          }
        })
      }
    }

    if (t.comments && Array.isArray(t.comments)) {
      for (const c of t.comments) {
        if (c.transition) {
          entries.push({
            id: `aud-tr-${c.id}`,
            timestamp: c.createdAt,
            action: 'STATUS_CHANGE',
            entityType: 'Ticket',
            entityId: t.id,
            entityName: t.ticketNumber,
            userId: c.authorUserId || 'usr-sys',
            userName: c.authorName || c.transition.actor || 'System',
            summary: `Ticket status transition ${c.transition.fromStatus} ➔ ${c.transition.toStatus}`,
            severity: c.transition.toStatus === 'Resolved' ? 'info' : 'warning',
            details: c.transition
          })
        }
      }
    }
  }

  // 2. Ingest inventory audit log records
  try {
    const invLogs = partsInventoryStore.getAuditLog()
    for (const log of invLogs) {
      entries.push({
        id: log.id || `aud-inv-${Date.now()}-${Math.random()}`,
        timestamp: log.timestamp,
        action: log.action === 'STOCK_CONSUMED' ? 'UPDATE' : (log.action === 'PART_REGISTERED' ? 'CREATE' : 'UPDATE'),
        entityType: 'InventoryItem',
        entityId: log.partId,
        entityName: log.partNumber,
        userId: log.performedBy || 'usr-tech-01',
        userName: log.performedByName || 'Warehouse Dispatcher',
        summary: `${log.details || log.action} on ${log.partNumber} (Qty: ${log.quantityChange > 0 ? `+${log.quantityChange}` : log.quantityChange})`,
        severity: 'info',
        details: log
      })
    }
  } catch {}

  // 3. Fallback baseline records for machines and infrastructure governance
  entries.push({
    id: 'aud-sys-001',
    timestamp: new Date(Date.now() - 2 * 3600 * 1000).toISOString(),
    action: 'CONFIG_CHANGE',
    entityType: 'SystemPolicy',
    entityId: 'POL-MFA-01',
    entityName: 'Step-up MFA Enforcement',
    userId: 'usr-admin-primary',
    userName: 'System Administrator',
    summary: 'Updated PKI mTLS trust bundle and re-enforced MFA challenge for OT Engineering roles.',
    severity: 'info'
  })

  entries.push({
    id: 'aud-sys-002',
    timestamp: new Date(Date.now() - 5 * 3600 * 1000).toISOString(),
    action: 'DECOMMISSION',
    entityType: 'Machine',
    entityId: 'STATION-OLD-09',
    entityName: 'Line 03 Legacy Hydraulic Press',
    userId: 'usr-plant-eng-1',
    userName: 'Gábor Varga',
    summary: 'Machine decommission completed: Salvaged 4 servo amplifiers into warehouse stock.',
    severity: 'warning',
    details: {
      salvagedCount: 4,
      disposition: 'salvage'
    }
  })

  // Filter
  let filtered = entries
  if (minSeverity === 'warning' || minSeverity === 'above_info' || minSeverity === 'above-info') {
    filtered = filtered.filter(e => e.severity === 'warning' || e.severity === 'critical')
  } else if (minSeverity === 'critical') {
    filtered = filtered.filter(e => e.severity === 'critical')
  }

  if (entityTypeFilter !== 'all') {
    filtered = filtered.filter(e => e.entityType.toLowerCase() === entityTypeFilter.toLowerCase())
  }
  if (searchQuery) {
    filtered = filtered.filter(e =>
      e.summary.toLowerCase().includes(searchQuery) ||
      e.userName.toLowerCase().includes(searchQuery) ||
      e.entityName?.toLowerCase().includes(searchQuery) ||
      e.action.toLowerCase().includes(searchQuery)
    )
  }

  // Sort descending by timestamp
  filtered.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())

  return {
    total: filtered.length,
    entries: filtered.slice(0, limit)
  }
})
