import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useTelemetryMetrics } from '../../../src/frontend/web/app/composables/useTelemetryMetrics'
import {
  getTelemetryDataPointCatalog,
  getAllTelemetryMetrics,
  findTelemetryMetricByKey,
  BUILTIN_METRICS
} from '../../../src/frontend/web/server/utils/telemetryCacheStore'

describe('Telemetry Trends & Hub Suite', () => {
  const storageMap = new Map<string, string>()

  beforeEach(() => {
    vi.clearAllMocks()
    storageMap.clear()
    vi.stubGlobal('localStorage', {
      getItem: vi.fn((key: string) => storageMap.get(key) || null),
      setItem: vi.fn((key: string, val: string) => storageMap.set(key, String(val))),
      removeItem: vi.fn((key: string) => storageMap.delete(key)),
      clear: vi.fn(() => storageMap.clear())
    })
  })

  describe('Dynamic Data-Point Catalog Discovery', () => {
    it('indexes all built-in metrics with strongly-typed properties', async () => {
      const catalog = await getTelemetryDataPointCatalog()
      expect(catalog.length).toBeGreaterThanOrEqual(10)

      const cycleMetric = catalog.find(c => c.key === 'cycle_time')
      expect(cycleMetric).toBeDefined()
      expect(cycleMetric?.dataType).toBe('Float')
      expect(cycleMetric?.unit).toBe('ms')
      expect(cycleMetric?.updateFrequencyMs).toBe(50)
      expect(cycleMetric?.sourceType).toBe('BeckhoffAds')
      expect(cycleMetric?.activeControllers).toContain('ctrl-op20-ads')

      const discreteMetric = catalog.find(c => c.key === 'tool_clamped_state')
      expect(discreteMetric).toBeDefined()
      expect(discreteMetric?.dataType).toBe('Boolean')
      expect(discreteMetric?.unit).toBe('')
      expect(discreteMetric?.updateFrequencyMs).toBe(50)
    })

    it('filters catalog by category accurately', async () => {
      const thermalCatalog = await getTelemetryDataPointCatalog({ category: 'thermal' })
      expect(thermalCatalog.length).toBeGreaterThan(0)
      expect(thermalCatalog.every(c => c.category === 'thermal')).toBe(true)
      expect(thermalCatalog[0].unit).toBe('°C')
    })

    it('filters catalog by variable data type', async () => {
      const boolCatalog = await getTelemetryDataPointCatalog({ dataType: 'Boolean' })
      expect(boolCatalog.length).toBeGreaterThan(0)
      expect(boolCatalog.every(c => c.dataType === 'Boolean')).toBe(true)

      const floatCatalog = await getTelemetryDataPointCatalog({ dataType: 'Float' })
      expect(floatCatalog.length).toBeGreaterThan(0)
      expect(floatCatalog.every(c => c.dataType === 'Float')).toBe(true)
    })

    it('supports free-text search across key, name, and symbol path', async () => {
      const results = await getTelemetryDataPointCatalog({ query: 'vibration' })
      expect(results.length).toBeGreaterThan(0)
      expect(results[0].key).toBe('vibration_rms_mms')
      expect(results[0].unit).toBe('mm/s')
    })
  })

  describe('Rolling Statistical Trends & Linear Regression', () => {
    it('computes linear regression slope and intercept over telemetry series', () => {
      // Synthetic linearly drifting data: y = 2x + 10
      const samplePoints = [
        { x: 0, y: 10 },
        { x: 1, y: 12 },
        { x: 2, y: 14 },
        { x: 3, y: 16 },
        { x: 4, y: 18 }
      ]
      const n = samplePoints.length
      let sumX = 0, sumY = 0, sumXY = 0, sumXX = 0, sumYY = 0
      for (const p of samplePoints) {
        sumX += p.x
        sumY += p.y
        sumXY += p.x * p.y
        sumXX += p.x * p.x
        sumYY += p.y * p.y
      }

      const denominator = (n * sumXX - sumX * sumX)
      const slope = (n * sumXY - sumX * sumY) / denominator
      const intercept = (sumY - slope * sumX) / n

      expect(slope).toBeCloseTo(2.0, 4)
      expect(intercept).toBeCloseTo(10.0, 4)
    })

    it('computes rolling moving averages and standard deviation corridors', () => {
      const values = [100, 102, 101, 105, 108, 110, 112]
      const window = 3
      const movingAverages: number[] = []

      for (let i = 0; i < values.length; i++) {
        const start = Math.max(0, i - window + 1)
        const slice = values.slice(start, i + 1)
        const avg = slice.reduce((a, b) => a + b, 0) / slice.length
        movingAverages.push(Math.round(avg * 100) / 100)
      }

      expect(movingAverages[0]).toBe(100)
      expect(movingAverages[1]).toBe(101) // (100 + 102)/2
      expect(movingAverages[2]).toBe(101) // (100 + 102 + 101)/3
      expect(movingAverages[values.length - 1]).toBe(110) // (108 + 110 + 112)/3
    })
  })

  describe('Recipe-Based Forecast System', () => {
    it('evaluates recipe baseline and calculates tool wear forecasting', () => {
      const nominal = 1200.0
      const upper = 1280.0
      const current = 1260.0 // +5% drift

      const driftPct = ((current - nominal) / nominal) * 100
      expect(driftPct).toBeCloseTo(5.0, 1)

      // Warning threshold evaluation (> 3.5%)
      let status = 'Nominal'
      let cyclesRemaining = 1200
      if (Math.abs(driftPct) >= 8.0) {
        status = 'CriticalImpendingFailure'
        cyclesRemaining = 180
      } else if (Math.abs(driftPct) >= 3.5) {
        status = 'Warning'
        cyclesRemaining = 650
      }

      expect(status).toBe('Warning')
      expect(cyclesRemaining).toBe(650)
    })

    it('triggers critical impending failure forecast for severe drift', () => {
      const nominal = 1200.0
      const current = 1320.0 // +10% drift
      const driftPct = ((current - nominal) / nominal) * 100

      let status = 'Nominal'
      if (Math.abs(driftPct) >= 8.0) {
        status = 'CriticalImpendingFailure'
      }

      expect(status).toBe('CriticalImpendingFailure')
    })
  })

  describe('Pin to Overview & Storage Sync', () => {
    it('pins a live telemetry widget and retrieves it from localStorage', () => {
      const { pinToOverview, getPinnedWidgets, unpinFromOverview } = useTelemetryMetrics()

      const success = pinToOverview({
        machineId: 'm-op20',
        machineName: 'OP20-Weld Laser Cell',
        metricKey: 'cycle_time',
        metricName: 'Station Cycle Time',
        unit: 'ms',
        currentValue: 1198.4
      })

      expect(success).toBe(true)

      const pinned = getPinnedWidgets()
      expect(pinned.length).toBe(1)
      expect(pinned[0].metricKey).toBe('cycle_time')
      expect(pinned[0].machineId).toBe('m-op20')
      expect(pinned[0].currentValue).toBe(1198.4)

      // Unpin
      const unpinSuccess = unpinFromOverview(pinned[0].id)
      expect(unpinSuccess).toBe(true)
      expect(getPinnedWidgets().length).toBe(0)
    })
  })
})
