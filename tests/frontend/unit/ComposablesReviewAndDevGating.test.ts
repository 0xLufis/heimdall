import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { useGlobalSearchModal } from '~/composables/useGlobalSearchModal'
import { useShortcuts } from '~/composables/useShortcuts'
import { getFeatureFlags, featureFlags } from '~~/server/utils/featureFlags'
import { getTicketsStore } from '~~/server/utils/ticketsStore'
import { getAllGroups } from '~~/server/utils/machineGroupsStore'
import { getAllRules, getAllAbsences, getTeamsOooStatuses } from '~~/server/utils/technicianRulesStore'
import { getCachedTelemetryDatapoints } from '~~/server/utils/telemetryCacheStore'
import { ExternalEnterpriseMaintenanceAdapter } from '~/services/maintenance/ExternalEnterpriseMaintenanceAdapter'

describe('Composables Review and Dev Gating Suite', () => {
  const originalEnv = { ...process.env }

  beforeEach(() => {
    process.env = { ...originalEnv }
  })

  afterEach(() => {
    process.env = { ...originalEnv }
  })

  describe('useGlobalSearchModal Composable', () => {
    it('initializes and manages modal state correctly', () => {
      const { isOpen, openModal, closeModal, toggleModal } = useGlobalSearchModal()

      closeModal()
      expect(isOpen.value).toBe(false)

      openModal()
      expect(isOpen.value).toBe(true)

      closeModal()
      expect(isOpen.value).toBe(false)

      toggleModal()
      expect(isOpen.value).toBe(true)

      toggleModal()
      expect(isOpen.value).toBe(false)
    })
  })

  describe('useShortcuts Composable', () => {
    it('returns platform meta symbol and labels', () => {
      const { metaSymbol, macOS } = useShortcuts()
      expect(typeof metaSymbol.value).toBe('string')
      expect(macOS).toBeDefined()
    })
  })

  describe('Server Store Feature Gating', () => {
    it('provides ticket store structure in development mode', () => {
      process.env.NODE_ENV = 'development'
      delete process.env.HEIMDALL_ENABLE_DEV

      expect(featureFlags.enableDevFeatures).toBe(true)
      const tickets = getTicketsStore()
      expect(Array.isArray(tickets)).toBe(true)
    })

    it('provides machine groups according to configuration', () => {
      const groups = getAllGroups()
      expect(Array.isArray(groups)).toBe(true)
      expect(groups.length).toBeGreaterThan(0)
    })

    it('provides technician rules, absences, and teams status', () => {
      const rules = getAllRules()
      expect(Array.isArray(rules)).toBe(true)
      expect(rules.length).toBeGreaterThan(0)

      const absences = getAllAbsences()
      expect(Array.isArray(absences)).toBe(true)

      const teams = getTeamsOooStatuses()
      expect(Array.isArray(teams)).toBe(true)
    })

    it('returns empty telemetry datapoints when dev features are disabled in production and no cache exists', async () => {
      process.env.NODE_ENV = 'production'
      process.env.HEIMDALL_ENABLE_DEV = 'false'

      const points = await getCachedTelemetryDatapoints('non-existent-target', 'non-existent-metric', '1h')
      expect(points).toEqual([])
    })
  })

  describe('ExternalEnterpriseMaintenanceAdapter Contract', () => {
    it('implements IMaintenanceService with valid empty fallbacks', async () => {
      const adapter = new ExternalEnterpriseMaintenanceAdapter({
        systemType: 'SAP_PM',
        endpointUrl: 'https://sap.internal.factory/api'
      })

      const tickets = await adapter.getTickets()
      expect(tickets).toEqual([])

      const ticket = await adapter.getTicketById('any')
      expect(ticket).toBeNull()

      const metrics = await adapter.getMetrics()
      expect(metrics.totalTickets).toBe(0)
      expect(metrics.slaCompliancePercent).toBe(100)

      const created = await adapter.createTicket({
        title: 'Hydraulic leak',
        description: 'Line 2 pump leak',
        priority: 'High'
      })
      expect(created.title).toBe('Hydraulic leak')
      expect(created.status).toBe('Open')

      const comment = await adapter.addComment(created.id, 'Tester', 'Inspecting pump')
      expect(comment.ticketId).toBe(created.id)
      expect(comment.authorName).toBe('Tester')

      const unsub = adapter.subscribeToEvents(() => {})
      expect(typeof unsub).toBe('function')
      unsub()
    })
  })
})
