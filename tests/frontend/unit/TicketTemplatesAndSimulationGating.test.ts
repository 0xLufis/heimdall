import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import {
  getTemplatesStore,
  getTemplateById,
  addTemplateToStore,
  updateTemplateInStore,
  resetTemplatesStore,
  exportTwinCatDut
} from '~~/server/utils/ticketTemplatesStore'
import {
  getFeatureFlags,
  assertSimulationEnabled,
  assertDevFeaturesEnabled
} from '~~/server/utils/featureFlags'
import { useFeatureFlags, getAppFeatureFlags } from '~/composables/useFeatureFlags'
import templatesGetHandler from '~~/server/api/tickets/templates.get'
import templatesPostHandler from '~~/server/api/tickets/templates.post'
import templatesPutHandler from '~~/server/api/tickets/templates.put'
import simulatorHandler from '~~/server/api/simulator/[...action]'

describe('Ticket Templates Catalog & Simulation Gating Suite', () => {
  const originalEnv = { ...process.env }

  beforeEach(() => {
    process.env = { ...originalEnv }
    resetTemplatesStore()
  })

  afterEach(() => {
    process.env = { ...originalEnv }
    resetTemplatesStore()
  })

  describe('Server Ticket Templates Store & API', () => {
    it('initializes templates store with standard 4-tier failure profiles', () => {
      const templates = getTemplatesStore()
      expect(templates.length).toBeGreaterThan(10)

      const mot01 = getTemplateById('E-MOT-01')
      expect(mot01).toBeDefined()
      expect(mot01?.category).toBe('Error')
      expect(mot01?.errorCode).toBe('E-MOT-01')
      expect(mot01?.sampleFbState?.blockName).toBe('FB_AxisControl')
    })

    it('GET /api/tickets/templates returns all templates and categories', async () => {
      const event = {
        context: {},
        node: { req: { url: '/api/tickets/templates' } }
      } as any

      const res = await templatesGetHandler(event)
      expect(res.templates).toBeInstanceOf(Array)
      expect(res.total).toBe(res.templates.length)
      expect(res.categories).toContain('Error')
      expect(res.categories).toContain('Prevention')
      expect(res.categories).toContain('Improvement')
      expect(res.categories).toContain('ETC')
    })

    it('GET /api/tickets/templates filters by category and machineType', async () => {
      const event = {
        method: 'GET',
        path: '/api/tickets/templates?category=prevention&machineType=pressing',
        context: {},
        node: {
          req: {
            method: 'GET',
            url: '/api/tickets/templates?category=prevention&machineType=pressing',
            headers: {}
          }
        }
      } as any

      const res = await templatesGetHandler(event)
      expect(res.templates.every((t: any) => t.category.toLowerCase() === 'prevention')).toBe(true)
      expect(res.templates.every((t: any) => t.affectedMachineTypes?.some((m: string) => m.toLowerCase().includes('pressing')))).toBe(true)
    })

    it('GET /api/tickets/templates filters by search query', async () => {
      const event = {
        method: 'GET',
        path: '/api/tickets/templates?search=light%20curtain',
        context: {},
        node: {
          req: {
            method: 'GET',
            url: '/api/tickets/templates?search=light%20curtain',
            headers: {}
          }
        }
      } as any

      const res = await templatesGetHandler(event)
      expect(res.templates.length).toBeGreaterThanOrEqual(1)
      expect(res.templates[0].errorCode).toBe('E-SAFE-01')
    })

    it('POST /api/tickets/templates adds a custom template to catalog', async () => {
      const customPayload = {
        errorCode: 'E-CUST-99',
        category: 'Error',
        errorGroup: 'Custom Automation',
        shortDescription: 'Pneumatic Gripper Pressure Loss',
        detailedDescription: 'Gripper pneumatic cylinder pressure dropped below 4.5 bar.',
        targetKanbanState: 'In_Progress',
        defaultTags: ['#Pneumatic', '#Gripper'],
        affectedMachineTypes: ['Manipulator']
      }

      const event = {
        method: 'POST',
        path: '/api/tickets/templates',
        context: {},
        node: {
          req: {
            method: 'POST',
            url: '/api/tickets/templates',
            headers: { 'content-type': 'application/json' }
          }
        },
        _body: customPayload
      } as any

      const res = await templatesPostHandler(event)
      expect(res.success).toBe(true)
      expect(res.template.errorCode).toBe('E-CUST-99')

      const stored = getTemplateById('E-CUST-99')
      expect(stored).toBeDefined()
      expect(stored?.shortDescription).toBe('Pneumatic Gripper Pressure Loss')
      // Enforced: Ticket templates can only start in Open state
      expect(stored?.targetKanbanState).toBe('Open')
    })

    it('PUT /api/tickets/templates updates an existing template and enforces Open state', async () => {
      const updatePayload = {
        id: 'E-MOT-01',
        shortDescription: 'Axis Position Divergence (Updated Checksum)',
        assignedTechnologies: ['Robotics', 'Assembly', 'Vision'],
        assignedMachineGroups: ['grp-line01', 'grp-line03'],
        targetKanbanState: 'InProgress' // Attempting non-Open state
      }

      const event = {
        method: 'PUT',
        path: '/api/tickets/templates',
        context: {},
        node: {
          req: {
            method: 'PUT',
            url: '/api/tickets/templates',
            headers: { 'content-type': 'application/json' }
          }
        },
        _body: updatePayload
      } as any

      const res = await templatesPutHandler(event)
      expect(res.success).toBe(true)
      expect(res.template.shortDescription).toBe('Axis Position Divergence (Updated Checksum)')
      expect(res.template.assignedTechnologies).toContain('Vision')
      expect(res.template.assignedMachineGroups).toContain('grp-line03')
      // Must remain strictly Open
      expect(res.template.targetKanbanState).toBe('Open')
    })

    it('exportTwinCatDut generates valid Beckhoff TwinCAT 3 .TcDUT XML with error enums', () => {
      const dutXml = exportTwinCatDut()
      expect(dutXml).toContain('<?xml version="1.0" encoding="utf-8"?>')
      expect(dutXml).toContain('<DUT Name="E_HeimdallErrorCode"')
      expect(dutXml).toContain('TYPE E_HeimdallErrorCode :')
      expect(dutXml).toContain('NONE := 0')
      expect(dutXml).toContain('E_MOT_01')
      expect(dutXml).toContain('END_TYPE')
    })
  })

  describe('Simulation Feature Gating (SIM-GATE-001, 002, 003)', () => {
    it('assertSimulationEnabled throws 403 when HEIMDALL_ENABLE_SIMULATION is false', () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'
      process.env.HEIMDALL_ENABLE_SIMULATION = 'false'

      expect(() => assertSimulationEnabled()).toThrowError(/Forbidden: Simulation features are disabled/)
    })

    it('assertSimulationEnabled throws 403 in production when simulation is not enabled', () => {
      process.env.NODE_ENV = 'production'
      delete process.env.HEIMDALL_ENABLE_SIMULATION
      delete process.env.HEIMDALL_ENABLE_DEV

      expect(() => assertSimulationEnabled()).toThrowError(/Forbidden: Simulation features are disabled/)
    })

    it('assertSimulationEnabled does not throw when simulation is enabled', () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'
      process.env.HEIMDALL_ENABLE_SIMULATION = 'true'

      expect(() => assertSimulationEnabled()).not.toThrow()
    })

    it('rejects /api/simulator/* with 403 when simulation is disabled', async () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'
      process.env.HEIMDALL_ENABLE_SIMULATION = 'false'

      const mockEvent = {
        context: { params: { action: 'status' } },
        method: 'GET'
      } as any

      await expect(simulatorHandler(mockEvent)).rejects.toMatchObject({
        statusCode: 403
      })
    })

    it('useFeatureFlags exposes enableSimulation reactive property', () => {
      process.env.NODE_ENV = 'development'
      process.env.HEIMDALL_ENABLE_DEV = 'true'
      process.env.HEIMDALL_ENABLE_SIMULATION = 'true'

      const flags = useFeatureFlags()
      expect(flags.enableSimulation.value).toBe(true)

      process.env.HEIMDALL_ENABLE_SIMULATION = 'false'
      expect(getAppFeatureFlags().enableSimulation).toBe(false)
    })
  })
})
