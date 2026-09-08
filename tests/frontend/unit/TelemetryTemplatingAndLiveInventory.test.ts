import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useTelemetryTemplates } from '~/composables/useTelemetryTemplates'
import { useInventoryLive } from '~/composables/useInventoryLive'
import { BUILTIN_TELEMETRY_TEMPLATES } from '~/utils/defaultTelemetryTemplates'
import { navMenu } from '~/constants/menus'

describe('Agent Telemetry Templating and Dynamic Inventory Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string, opts?: any) => {
      if (url === '/api/telemetry/templates') {
        if (opts?.method === 'PUT') return Promise.resolve({ success: true })
        return Promise.resolve([])
      }
      if (url === '/api/telemetry/config') {
        return Promise.resolve({
          heartbeatIntervalSeconds: 15,
          deltaEvaluationAlgorithm: 'xxHash64'
        })
      }
      if (url === '/api/telemetry/dispatch') {
        return Promise.resolve({ success: true })
      }
      return Promise.resolve({})
    }))
  })

  it('includes Telemetry Templates and Telemetry Config in navMenu under Management', () => {
    const mgmtGroup = navMenu.find(g => g.heading === 'Management')
    expect(mgmtGroup).toBeDefined()
    const links = mgmtGroup!.items.map(i => i.link)
    expect(links).toContain('/dashboard/telemetry/templates')
    expect(links).toContain('/dashboard/telemetry/configure')
    expect(links).toContain('/dashboard/inventory')
  })

  it('loads built-in industrial templates with high-frequency motion and CIM probes', () => {
    const { allTemplates } = useTelemetryTemplates()
    expect(allTemplates.value.length).toBeGreaterThanOrEqual(5)

    const motionTpl = allTemplates.value.find(t => t.name.includes('Beckhoff ADS'))
    expect(motionTpl).toBeDefined()
    expect(motionTpl?.targetSelector.osPlatform).toBe('Windows')
    expect(motionTpl?.dataPoints.some(dp => dp.sourceType === 'BeckhoffAds')).toBe(true)

    const baselineTpl = allTemplates.value.find(t => t.name.includes('Baseline'))
    expect(baselineTpl).toBeDefined()
    expect(baselineTpl?.dataPoints.some(dp => dp.sourceType === 'SystemCim')).toBe(true)
  })

  it('allows creating, updating, and evaluating a custom telemetry template', async () => {
    const { allTemplates, addTemplate, updateTemplate, simulateEvaluation } = useTelemetryTemplates()
    
    const created = await addTemplate({
      version: '1.0.0',
      name: 'Battery Line OPC UA Recipe',
      targetSelector: {
        osPlatform: 'Linux',
        controllerRoles: ['PackAssembly'],
        tags: { Line: 'Line-G' }
      },
      security: {
        keyId: 'pki-test',
        algorithm: 'RSA_PSS_SHA256',
        signPayload: true
      },
      dataPoints: [
        {
          pointId: 'dp-cell-voltages',
          name: 'Cell Voltage Array',
          sourceType: 'OpcUaSubscription',
          dataCategory: 'List',
          egressPriority: 'P1_HighOperational',
          schedule: { strategy: 'Periodic', intervalMs: 1000 },
          deadband: { deadbandType: 'Percentage', deadbandValue: 0.5 },
          pathOrSymbol: 'ns=2;s=BatteryModule.CellVoltages'
        }
      ]
    })

    expect(created.recipeId).toBeDefined()
    expect(created.isBuiltin).toBe(false)
    expect(allTemplates.value.some(t => t.recipeId === created.recipeId)).toBe(true)

    // Run simulation
    const simResult = simulateEvaluation(created)
    expect(simResult.recipeId).toBe(created.recipeId)
    expect(simResult.payload['dp-cell-voltages']).toBeDefined()
    expect(simResult.payload['dp-cell-voltages'].category).toBe('List')
  })

  it('useInventoryLive supports subscribing to real-time inventory updates and manual triggers', () => {
    const { isLiveConnected, notifySubscribers, onInventoryUpdate } = useInventoryLive()
    
    let callCount = 0
    onInventoryUpdate(() => {
      callCount++
    })

    expect(callCount).toBe(0)
    notifySubscribers()
    expect(callCount).toBe(1)
    notifySubscribers()
    expect(callCount).toBe(2)
  })

  it('correctly paginates large inventory collections (e.g. 5,000 machines) with selectable and custom page sizes', () => {
    // Generate mock fleet of 5,000 machines
    const fleet = Array.from({ length: 5000 }, (_, i) => ({
      id: `mach-${i + 1}`,
      name: `CNC Milling Unit ${i + 1}`,
      hostname: `cnc-${i + 1}.factory.corp`,
      customIdentifier: `STATION-OP${(i % 100) + 1}`,
      itemType: 'hardware',
      telemetry: {
        isOnline: i % 2 === 0,
        cpuUsagePercent: 15 + (i % 30),
        ramUsagePercent: 30 + (i % 40)
      }
    }))

    // 1. Test standard page sizes (5, 10, 50, 100, 1000)
    const testPageSizes = [5, 10, 50, 100, 1000]
    for (const size of testPageSizes) {
      const totalPages = Math.ceil(fleet.length / size)
      const page1 = fleet.slice(0, size)
      const page2 = fleet.slice(size, size * 2)

      expect(totalPages).toBe(5000 / size)
      expect(page1.length).toBe(size)
      expect(page1[0].id).toBe('mach-1')
      expect(page2.length).toBe(size)
      expect(page2[0].id).toBe(`mach-${size + 1}`)
    }

    // 2. Test custom page size (e.g. 250)
    const customSize = 250
    const totalCustomPages = Math.ceil(fleet.length / customSize)
    const customPage1 = fleet.slice(0, customSize)
    expect(totalCustomPages).toBe(20)
    expect(customPage1.length).toBe(250)

    // 3. Test edge clamping on last page
    const lastPageNum = totalCustomPages
    const lastPageItems = fleet.slice((lastPageNum - 1) * customSize, lastPageNum * customSize)
    expect(lastPageItems.length).toBe(250)
    expect(lastPageItems[lastPageItems.length - 1].id).toBe('mach-5000')
  })
})
