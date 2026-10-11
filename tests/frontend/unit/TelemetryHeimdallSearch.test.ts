import { describe, it, expect, vi, beforeEach } from 'vitest'
import { searchRegistry } from '~/utils/search/searchRegistry'
import { defaultTelemetrySource, TELEMETRY_SEARCH_CONTEXT } from '~/utils/search/defaultContexts'
import { useHeimdallSearch } from '~/composables/useHeimdallSearch'
import { resolveSearchTemplate } from '~/utils/search/searchTemplates'

describe('Telemetry Heimdall Search & Workspace Integration Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('$fetch', vi.fn().mockImplementation((url: string) => {
      if (url === '/api/proxy/v1/ClientPc') {
        return Promise.resolve([
          {
            id: 'ctrl-laser-01',
            hostname: 'IPC-LASER-CELL-01',
            name: 'Laser Welder IPC',
            ipAddress: '192.168.10.45',
            macAddress: '00:01:05:AA:BB:01',
            isOnline: true,
            telemetry: { isOnline: true, cpuUsagePercent: 32, ramUsagePercent: 48 }
          },
          {
            id: 'ctrl-cnc-02',
            hostname: 'IPC-CNC-MILL-02',
            name: 'CNC Milling Center',
            ipAddress: '192.168.10.46',
            macAddress: '00:01:05:AA:BB:02',
            isOnline: false,
            telemetry: { isOnline: false, cpuUsagePercent: 0, ramUsagePercent: 0 }
          }
        ])
      }
      if (url === '/api/proxy/v1/machine') {
        return Promise.resolve([
          {
            id: 'mch-op20',
            name: 'OP20 Laser Welding Cell',
            displayName: 'OP20 Laser Cell',
            customIdentifier: 'LINE-A-OP20',
            status: 'online'
          }
        ])
      }
      return Promise.resolve([])
    }))
  })

  describe('1. Search Registry Provider Configuration', () => {
    it('registers the telemetry provider in SearchRegistry with correct properties', () => {
      const provider = searchRegistry.getProvider('telemetry')
      expect(provider).toBeDefined()
      expect(provider?.id).toBe('telemetry')
      expect(provider?.entityType).toBe('TelemetryMetric')
      expect(provider?.indicatorLabel).toBe('TEL')
      expect(provider?.columns).toContain('name')
      expect(provider?.columns).toContain('category')
      expect(provider?.columns).toContain('dataType')
      expect(provider?.columns).toContain('unit')
    })

    it('returns strictly telemetry provider for telemetry context', () => {
      const providers = searchRegistry.getProvidersForContext('telemetry')
      const ids = providers.map(p => p.id)
      expect(ids).toEqual(['telemetry'])
      expect(ids).not.toContain('client_pcs')
      expect(ids).not.toContain('machines')
    })

    it('TELEMETRY_SEARCH_CONTEXT contains defaultTelemetrySource', () => {
      expect(TELEMETRY_SEARCH_CONTEXT.id).toBe('telemetry-workspace')
      expect(TELEMETRY_SEARCH_CONTEXT.sources.some(s => s.entityType === 'TelemetryMetric')).toBe(true)
    })
  })

  describe('2. Telemetry Search Instance via useHeimdallSearch', () => {
    it('executes telemetry search and retrieves strictly telemetry metrics', async () => {
      const search = useHeimdallSearch({
        config: {
          instanceId: 'telemetry',
          allowedTagKeys: ['metric', 'category', 'unit', 'datatype', 'source', 'quality', 'key']
        }
      })

      await search.executeSearch('')

      expect(search.results.value.length).toBeGreaterThan(0)
      const telemetryItems = search.results.value.filter(r => r.sourceTable === 'telemetry')
      const controllerItems = search.results.value.filter(r => r.sourceTable === 'nodes')

      expect(telemetryItems.length).toBeGreaterThan(0)
      expect(controllerItems.length).toBe(0)

      // Spindle speed metric is present
      const spindle = telemetryItems.find(i => i.name.toLowerCase().includes('spindle'))
      expect(spindle).toBeDefined()
      expect(spindle?.subtitle).toContain('RPM')
    })

    it('filters telemetry metrics by free-text search query', async () => {
      const search = useHeimdallSearch({
        config: {
          instanceId: 'telemetry'
        }
      })

      await search.executeSearch('vibration')

      const vibrationItem = search.results.value.find(i =>
        i.name.toLowerCase().includes('vibration') ||
        (i.displayName && i.displayName.toLowerCase().includes('vibration'))
      )
      expect(vibrationItem).toBeDefined()
      expect(vibrationItem?.subtitle).toContain('mm/s')
    })

    it('filters telemetry metrics by keyword query in telemetry workspace', async () => {
      const search = useHeimdallSearch({
        config: {
          instanceId: 'telemetry'
        }
      })

      await search.executeSearch('hydraulic')

      const hydraulic = search.results.value.find(i => i.name.toLowerCase().includes('hydraulic'))
      expect(hydraulic).toBeDefined()
      expect(hydraulic?.sourceTable).toBe('telemetry')
      expect(hydraulic?.subtitle).toContain('bar')
    })

    it('generates direct navigation link to live telemetry metric stream', () => {
      const template = resolveSearchTemplate('telemetry')
      const item = template.extractSearchItem({
        id: 'spindleSpeedRpm',
        name: 'Spindle Rotational Speed',
        category: 'Machining',
        unit: 'RPM'
      })

      expect(item.link).toBe('/dashboard/telemetry?tab=live&metric=spindleSpeedRpm')
      expect(item.typeLabel).toBe('Machining')
    })
  })
})
