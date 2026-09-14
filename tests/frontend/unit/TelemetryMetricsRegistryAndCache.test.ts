import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useTelemetryMetrics } from '../../../frontend/web/app/composables/useTelemetryMetrics'
import {
  getAllTelemetryMetrics,
  findTelemetryMetricByKey,
  saveCustomTelemetryMetric,
  deleteCustomTelemetryMetric,
  getCachedTelemetryDatapoints,
  recordCachedTelemetryDatapoint,
  BUILTIN_METRICS
} from '../../../frontend/web/server/utils/telemetryCacheStore'
import AlertRulesManager from '../../../frontend/web/app/components/analytics/AlertRulesManager.vue'
import TelemetryTrendVisualizer from '../../../frontend/web/app/components/analytics/TelemetryTrendVisualizer.vue'

// Mock $fetch globally for composable and component requests
globalThis.$fetch = vi.fn(async (url: string, opts?: any) => {
  if (url === '/api/telemetry/metrics') {
    if (opts?.method === 'POST') {
      const body = opts.body
      const saved = await saveCustomTelemetryMetric({
        id: `met-custom-${Date.now()}`,
        key: body.key,
        name: body.name,
        description: body.description,
        unit: body.unit,
        category: body.category,
        sourceType: body.sourceType || 'BeckhoffAds',
        pathOrSymbol: body.pathOrSymbol,
        nominalValue: body.nominalValue,
        upperTolerance: body.upperTolerance,
        lowerTolerance: body.lowerTolerance,
        isUserDefined: true,
        createdAt: new Date().toISOString()
      })
      return { success: true, metric: saved }
    }
    if (opts?.method === 'DELETE') {
      const key = opts.query?.key
      if (key) {
        await deleteCustomTelemetryMetric(key)
      }
      return { success: true }
    }
    const all = await getAllTelemetryMetrics()
    return all
  }

  if (url === '/api/analytics/trends' || url === '/api/proxy/v1/analytics/trends') {
    const metricKey = opts?.params?.metric || 'cycle_time'
    const machineId = opts?.params?.machineId || 'm-op20'
    const range = opts?.params?.range || '8h'
    const metricDef = (await findTelemetryMetricByKey(metricKey)) || BUILTIN_METRICS[0]
    const points = await getCachedTelemetryDatapoints(machineId, metricKey, range, metricDef)

    return {
      machineId,
      metricName: metricDef.name,
      metricKey,
      unit: metricDef.unit,
      nominalValue: metricDef.nominalValue,
      upperTolerance: metricDef.upperTolerance,
      lowerTolerance: metricDef.lowerTolerance,
      points,
      detectedAnomalies: points.filter(p => p.isAnomaly).map(p => ({
        timestamp: p.timestamp,
        metric: metricDef.name,
        value: p.value,
        expectedValue: metricDef.nominalValue,
        zScore: p.zScore,
        severity: 'Warning',
        description: `${metricDef.name} anomaly detected`
      })),
      source: 'telemetry_cache',
      isUserDefined: metricDef.isUserDefined
    }
  }

  return {}
}) as any

describe('Telemetry Metrics Registry & Cached Telemetry Store', () => {
  describe('Composable: useTelemetryMetrics', () => {
    it('initializes with standard built-in metrics catalog', () => {
      const { metrics, builtinMetrics } = useTelemetryMetrics()

      expect(metrics.value.length).toBeGreaterThanOrEqual(7)
      expect(builtinMetrics.value.length).toBeGreaterThanOrEqual(7)

      const cycleTime = metrics.value.find(m => m.key === 'cycle_time')
      expect(cycleTime).toBeDefined()
      expect(cycleTime?.nominalValue).toBe(1200.0)
      expect(cycleTime?.unit).toBe('ms')
      expect(cycleTime?.isUserDefined).toBe(false)

      const motorTemp = metrics.value.find(m => m.key === 'motor_temp_c')
      expect(motorTemp).toBeDefined()
      expect(motorTemp?.unit).toBe('°C')
    })

    it('allows operators to create a user-defined telemetry metric', async () => {
      const { createCustomMetric, userDefinedMetrics, getMetricByKey } = useTelemetryMetrics()

      const newMetric = await createCustomMetric({
        key: 'coolant_return_temp_c',
        name: 'Chiller Coolant Return Temperature',
        description: 'Monitors thermal heat exchanger return line temperature',
        unit: '°C',
        category: 'thermal',
        sourceType: 'BeckhoffAds',
        pathOrSymbol: 'MAIN.fbCoolant.fReturnTemp',
        nominalValue: 21.5,
        upperTolerance: 28.0,
        lowerTolerance: 16.0
      })

      expect(newMetric).toBeDefined()
      expect(newMetric.key).toBe('coolant_return_temp_c')
      expect(newMetric.isUserDefined).toBe(true)

      const found = getMetricByKey('coolant_return_temp_c')
      expect(found).toBeDefined()
      expect(found?.nominalValue).toBe(21.5)
      expect(userDefinedMetrics.value.some(m => m.key === 'coolant_return_temp_c')).toBe(true)
    })

    it('deletes user-defined metric without affecting built-in metrics', async () => {
      const { deleteCustomMetric, getMetricByKey, builtinMetrics } = useTelemetryMetrics()

      const deleted = await deleteCustomMetric('coolant_return_temp_c')
      expect(deleted).toBe(true)
      expect(getMetricByKey('coolant_return_temp_c')).toBeUndefined()

      // Built-in metrics still exist
      expect(getMetricByKey('cycle_time')).toBeDefined()
      expect(builtinMetrics.value.length).toBeGreaterThanOrEqual(7)
    })
  })

  describe('Server Cache Store: telemetryCacheStore', () => {
    it('saves and retrieves user-defined metrics alongside built-in metrics', async () => {
      const metric = await saveCustomTelemetryMetric({
        id: 'met-cust-pneumatic-01',
        key: 'nozzle_backpressure_bar',
        name: 'Dispenser Nozzle Backpressure',
        description: 'Adhesive dispenser needle backpressure transducer',
        unit: 'bar',
        category: 'pneumatics',
        sourceType: 'BeckhoffEtherCat',
        pathOrSymbol: 'IO.Terminal_EL3064.Backpressure',
        nominalValue: 4.8,
        upperTolerance: 5.8,
        lowerTolerance: 3.8,
        isUserDefined: true,
        createdAt: new Date().toISOString()
      })

      expect(metric.key).toBe('nozzle_backpressure_bar')

      const found = await findTelemetryMetricByKey('nozzle_backpressure_bar')
      expect(found).toBeDefined()
      expect(found?.nominalValue).toBe(4.8)
      expect(found?.isUserDefined).toBe(true)

      const all = await getAllTelemetryMetrics()
      expect(all.some(m => m.key === 'nozzle_backpressure_bar')).toBe(true)
    })

    it('generates and retrieves cached telemetry datapoints matching metric parameters', async () => {
      const metricDef = await findTelemetryMetricByKey('nozzle_backpressure_bar')
      expect(metricDef).toBeDefined()

      const points = await getCachedTelemetryDatapoints('m-op10', 'nozzle_backpressure_bar', '8h', metricDef)

      expect(points.length).toBeGreaterThan(0)
      for (const p of points) {
        expect(p.timestamp).toBeDefined()
        expect(typeof p.value).toBe('number')
        expect(typeof p.isAnomaly).toBe('boolean')
        expect(typeof p.zScore).toBe('number')
      }

      // Check values oscillate around nominal value 4.8
      const avg = points.reduce((acc, p) => acc + p.value, 0) / points.length
      expect(avg).toBeGreaterThan(3.5)
      expect(avg).toBeLessThan(6.0)
    })

    it('records a live telemetry datapoint and retains it in cache', async () => {
      await recordCachedTelemetryDatapoint('m-op10', 'nozzle_backpressure_bar', 7.42, true)

      const cachedPoints = await getCachedTelemetryDatapoints('m-op10', 'nozzle_backpressure_bar', '8h')
      const latest = cachedPoints[cachedPoints.length - 1]
      expect(latest).toBeDefined()
      expect(latest.value).toBe(7.42)
      expect(latest.isAnomaly).toBe(true)
    })

    it('deletes custom metric from server store', async () => {
      const res = await deleteCustomTelemetryMetric('nozzle_backpressure_bar')
      expect(res).toBe(true)

      const all = await getAllTelemetryMetrics()
      expect(all.some(m => m.key === 'nozzle_backpressure_bar')).toBe(false)
    })
  })

  describe('UI Component Integration', () => {
    const commonStubs = {
      Dialog: { template: '<div><slot /></div>' },
      DialogContent: { template: '<div><slot /></div>' },
      DialogHeader: { template: '<div><slot /></div>' },
      DialogTitle: { template: '<div><slot /></div>' },
      DialogDescription: { template: '<div><slot /></div>' },
      Card: { template: '<div><slot /></div>' },
      Badge: { template: '<span><slot /></span>' },
      Button: { template: '<button><slot /></button>' }
    }

    it('renders Telemetry Metrics Registry sub-tab in AlertRulesManager', async () => {
      const wrapper = mount(AlertRulesManager, {
        global: {
          stubs: commonStubs
        }
      })

      expect(wrapper.text()).toContain('Telemetry Metrics')

      // Switch to Telemetry Metrics tab
      const metricsTab = wrapper.findAll('button').find(b => b.text().includes('Telemetry Metrics'))
      expect(metricsTab).toBeDefined()
      await metricsTab?.trigger('click')

      // Check header and registry elements
      expect(wrapper.text()).toContain('Industrial Telemetry Metrics Registry')
      expect(wrapper.text()).toContain('Register Custom Metric')
      expect(wrapper.text()).toContain('Cycle Time Deviation')
      expect(wrapper.text()).toContain('Spindle / Motor Temperature Drift')
    })

    it('renders dynamic telemetry metric tabs in TelemetryTrendVisualizer', async () => {
      const wrapper = mount(TelemetryTrendVisualizer, {
        props: {
          initialMachineId: 'm-op20'
        },
        global: {
          stubs: commonStubs
        }
      })

      // Wait for async onMounted to complete
      await new Promise(r => setTimeout(r, 50))

      expect(wrapper.text()).toContain('Predictive Telemetry Drift & Anomaly Tracker')
      expect(wrapper.text()).toContain('Cycle Time Deviation (ms)')
      expect(wrapper.text()).toContain('Nominal Target')
      expect(wrapper.text()).toContain('Period Mean (μ)')

      // Verify SVG canvas exists
      const svg = wrapper.find('svg')
      expect(svg.exists()).toBe(true)
    })
  })
})
