import { getCachedJson, setCachedJson } from './redis'
import { featureFlags } from './featureFlags'
export interface CachedTelemetryDatapoint {
  timestamp: string
  value: number
  isAnomaly: boolean
  zScore: number
}

export interface TelemetryMetricDefinition {
  id: string
  key: string
  name: string
  description?: string
  unit: string
  category: string
  dataType?: 'Boolean' | 'Int' | 'Float' | 'String'
  updateFrequencyMs?: number
  sourceType: string
  pathOrSymbol?: string
  activeControllers?: string[]
  nominalValue: number
  upperTolerance: number
  lowerTolerance: number
  isUserDefined: boolean
  createdAt: string
  updatedAt?: string
}

export interface TelemetryDataPointCatalogItem {
  id: string
  key: string
  name: string
  description: string
  category: string
  dataType: 'Boolean' | 'Int' | 'Float' | 'String'
  unit: string
  updateFrequencyMs: number
  sourceType: string
  pathOrSymbol: string
  activeControllers: string[]
  nominalValue: number
  upperTolerance: number
  lowerTolerance: number
}

export const BUILTIN_METRICS: TelemetryMetricDefinition[] = [
  {
    id: 'met-oee',
    key: 'oee_percentage',
    name: 'Overall Equipment Effectiveness (OEE)',
    description: 'Composite manufacturing index combining Availability, Performance, and Quality',
    unit: '%',
    category: 'cycle',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'SystemCim',
    pathOrSymbol: 'Plant.Station.KPI.OeePercentage',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op50-ads'],
    nominalValue: 88.5,
    upperTolerance: 98.0,
    lowerTolerance: 75.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-yield',
    key: 'production_yield_pct',
    name: 'First Pass Quality Yield',
    description: 'Percentage of conforming parts produced on first pass without rework or scrap',
    unit: '%',
    category: 'cycle',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'BeckhoffEtherCat',
    pathOrSymbol: 'Plant.Station.KPI.FirstPassYield',
    activeControllers: ['ctrl-op20-ads'],
    nominalValue: 99.2,
    upperTolerance: 100.0,
    lowerTolerance: 95.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-parts-hour',
    key: 'parts_per_hour',
    name: 'Production Throughput (Parts / Hour)',
    description: 'Real-time station output cadence expressed in parts completed per operating hour',
    unit: 'pcs/h',
    category: 'cycle',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'Plant.Station.Throughput.PartsPerHour',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op50-ads'],
    nominalValue: 145.0,
    upperTolerance: 180.0,
    lowerTolerance: 110.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-avail',
    key: 'station_availability_pct',
    name: 'Operational Availability',
    description: 'Ratio of actual operating time to scheduled production time',
    unit: '%',
    category: 'cycle',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'SystemCim',
    pathOrSymbol: 'Plant.Station.KPI.Availability',
    activeControllers: ['ctrl-op20-ads'],
    nominalValue: 96.5,
    upperTolerance: 99.5,
    lowerTolerance: 85.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-cycle-time',
    key: 'cycle_time',
    name: 'Cycle Time Deviation',
    description: 'Actual operating station cycle duration compared to engineered tact time',
    unit: 'ms',
    category: 'cycle',
    dataType: 'Float',
    updateFrequencyMs: 50,
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'MAIN.fbStation.nActualCycleTime',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op50-ads'],
    nominalValue: 1200.0,
    upperTolerance: 1280.0,
    lowerTolerance: 1120.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-jitter',
    key: 'cycle_jitter_us',
    name: 'Real-Time Soft-PLC Task Jitter',
    description: 'Cyclic task scheduling jitter measured across TwinCAT real-time kernel cores',
    unit: 'μs',
    category: 'jitter',
    dataType: 'Float',
    updateFrequencyMs: 10,
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'TwinCAT_System.TaskInfo.CycleJitter',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op10-ads'],
    nominalValue: 14.5,
    upperTolerance: 45.0,
    lowerTolerance: 2.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-temp',
    key: 'motor_temp_c',
    name: 'Spindle / Motor Temperature Drift',
    description: 'Core winding and bearing thermal feedback from integrated PT100/KTY sensors',
    unit: '°C',
    category: 'thermal',
    dataType: 'Float',
    updateFrequencyMs: 500,
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'Axis1.stDriveState.fMotorTemp',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op30-ads'],
    nominalValue: 48.5,
    upperTolerance: 65.0,
    lowerTolerance: 25.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-vib',
    key: 'vibration_rms_mms',
    name: 'Spindle Vibration Velocity RMS',
    description: 'Mechanical vibration velocity RMS indicating kinematic wear or unbalance',
    unit: 'mm/s',
    category: 'vibration',
    dataType: 'Float',
    updateFrequencyMs: 100,
    sourceType: 'BeckhoffEtherCat',
    pathOrSymbol: 'IO.Terminal_EL3632.VibRMS',
    activeControllers: ['ctrl-op20-ads'],
    nominalValue: 1.45,
    upperTolerance: 2.80,
    lowerTolerance: 0.50,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-spindle-rpm',
    key: 'spindle_rpm',
    name: 'Spindle Rotational Speed',
    description: 'Actual drive spindle rotary feedback in revolutions per minute',
    unit: 'RPM',
    category: 'motion',
    dataType: 'Float',
    updateFrequencyMs: 100,
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'Axis1.stAxisStatus.fActVelo',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op50-ads'],
    nominalValue: 3200.0,
    upperTolerance: 4500.0,
    lowerTolerance: 1000.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-hydraulic',
    key: 'hydraulic_pressure_bar',
    name: 'Hydraulic Clamp Fluid Pressure',
    description: 'Proportional hydraulic clamp circuit pressure transducer reading',
    unit: 'bar',
    category: 'hydraulic',
    dataType: 'Float',
    updateFrequencyMs: 200,
    sourceType: 'OpcUaSubscription',
    pathOrSymbol: 'ns=2;s=Hydraulics.Circuit1.PressureBar',
    activeControllers: ['ctrl-op50-ads'],
    nominalValue: 160.0,
    upperTolerance: 210.0,
    lowerTolerance: 130.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-tool-clamped',
    key: 'tool_clamped_state',
    name: 'Tool Clamping Interlock Confirmed',
    description: 'Binary inductive prox switch confirmation of pneumatic tool clamping chuck',
    unit: '',
    category: 'discrete',
    dataType: 'Boolean',
    updateFrequencyMs: 50,
    sourceType: 'BeckhoffEtherCat',
    pathOrSymbol: 'IO.Terminal_EL1008.Channel1.Input',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op50-ads'],
    nominalValue: 1.0,
    upperTolerance: 1.0,
    lowerTolerance: 1.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-cpu',
    key: 'cpu_usage_pct',
    name: 'IPC Host CPU Utilization',
    description: 'Aggregated core processing workload across all physical cores',
    unit: '%',
    category: 'resources',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'SystemCim',
    pathOrSymbol: 'Win32_PerfFormattedData_PerfOS_Processor.PercentProcessorTime',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op10-ads', 'ctrl-op50-ads'],
    nominalValue: 42.0,
    upperTolerance: 85.0,
    lowerTolerance: 10.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-ram',
    key: 'ram_usage_pct',
    name: 'IPC System Memory Load',
    description: 'Physical RAM active allocation percentage',
    unit: '%',
    category: 'resources',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'SystemCim',
    pathOrSymbol: 'Win32_OperatingSystem.FreePhysicalMemory',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op10-ads', 'ctrl-op50-ads'],
    nominalValue: 58.4,
    upperTolerance: 90.0,
    lowerTolerance: 20.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-pneu',
    key: 'pneumatic_pressure_bar',
    name: 'Pneumatic Header Line Pressure',
    description: 'Factory floor main pneumatic supply pressure monitored at valve terminal',
    unit: 'bar',
    category: 'pneumatics',
    dataType: 'Float',
    updateFrequencyMs: 250,
    sourceType: 'OpcUaSubscription',
    pathOrSymbol: 'ns=2;s=Pneumatics.PressureSensor.ActualBar',
    activeControllers: ['ctrl-op10-ads', 'ctrl-op30-ads'],
    nominalValue: 6.3,
    upperTolerance: 7.5,
    lowerTolerance: 5.4,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  },
  {
    id: 'met-fb',
    key: 'fieldbus_error_rate',
    name: 'Fieldbus Frame Loss & CRC Errors',
    description: 'EtherCAT or PROFINET frame corruption and cyclic packet drop frequency',
    unit: 'err/min',
    category: 'fieldbus',
    dataType: 'Float',
    updateFrequencyMs: 1000,
    sourceType: 'BeckhoffEtherCat',
    pathOrSymbol: 'Master0.Port0.CrcErrorsPerMin',
    activeControllers: ['ctrl-op20-ads', 'ctrl-op50-ads'],
    nominalValue: 0.2,
    upperTolerance: 2.0,
    lowerTolerance: 0.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  }
]

// In-memory fallback stores
const inMemoryCustomMetrics: TelemetryMetricDefinition[] = []
const inMemoryTelemetryCache = new Map<string, CachedTelemetryDatapoint[]>()

const CUSTOM_METRICS_REDIS_KEY = 'heimdall:telemetry:custom_metrics'

export async function getAllTelemetryMetrics(): Promise<TelemetryMetricDefinition[]> {
  let customMetrics: TelemetryMetricDefinition[] = []

  try {
    const cached = await getCachedJson<TelemetryMetricDefinition[]>(CUSTOM_METRICS_REDIS_KEY)
    if (cached && Array.isArray(cached)) {
      customMetrics = cached
    } else {
      customMetrics = inMemoryCustomMetrics
    }
  } catch {
    customMetrics = inMemoryCustomMetrics
  }

  // Combine built-in with user-defined
  return [...BUILTIN_METRICS, ...customMetrics]
}

export async function findTelemetryMetricByKey(key: string): Promise<TelemetryMetricDefinition | undefined> {
  const all = await getAllTelemetryMetrics()
  const lowerKey = key.toLowerCase()
  return all.find(m => m.key.toLowerCase() === lowerKey || m.key.toLowerCase().includes(lowerKey))
}

export async function saveCustomTelemetryMetric(metric: TelemetryMetricDefinition): Promise<TelemetryMetricDefinition> {
  const allCustom = inMemoryCustomMetrics
  const existingIdx = allCustom.findIndex(m => m.key === metric.key)

  if (existingIdx !== -1) {
    allCustom[existingIdx] = { ...metric, updatedAt: new Date().toISOString() }
  } else {
    allCustom.push({ ...metric, isUserDefined: true, createdAt: new Date().toISOString() })
  }

  try {
    await setCachedJson(CUSTOM_METRICS_REDIS_KEY, allCustom, 86400 * 30) // 30 day TTL
  } catch {
    // Retain in memory
  }

  return metric
}

export async function deleteCustomTelemetryMetric(key: string): Promise<boolean> {
  const idx = inMemoryCustomMetrics.findIndex(m => m.key === key)
  if (idx !== -1) {
    inMemoryCustomMetrics.splice(idx, 1)
  }

  try {
    await setCachedJson(CUSTOM_METRICS_REDIS_KEY, inMemoryCustomMetrics, 86400 * 30)
    return true
  } catch {
    return true
  }
}

/**
 * Retrieves cached telemetry datapoints for a given target and metric.
 * Falls back to Redis cache or in-memory ring buffer.
 * If no datapoints exist yet, seeds and caches them based on metric definition.
 */
export async function getCachedTelemetryDatapoints(
  targetId: string,
  metricKey: string,
  range: string = '8h',
  metricDef?: TelemetryMetricDefinition
): Promise<CachedTelemetryDatapoint[]> {
  const cacheKey = `heimdall:telemetry:series:${targetId}:${metricKey}:${range}`

  // 1. Try Redis cache
  try {
    const cached = await getCachedJson<CachedTelemetryDatapoint[]>(cacheKey)
    if (cached && Array.isArray(cached) && cached.length > 0) {
      return cached
    }
  } catch {
    // Proceed to in-memory check
  }

  // 2. Try in-memory ring buffer
  const memKey = `${targetId}:${metricKey}:${range}`
  const existingMem = inMemoryTelemetryCache.get(memKey)
  if (existingMem && existingMem.length > 0) {
    return existingMem
  }

  // If dev features are disabled, do not fabricate synthetic datapoints
  if (!featureFlags.enableDevFeatures) {
    return []
  }

  // 3. Generate baseline points matching nominal & tolerance parameters
  const def = metricDef || (await findTelemetryMetricByKey(metricKey)) || BUILTIN_METRICS[0]
  if (!def) return []
  const nominal = def.nominalValue
  const upper = def.upperTolerance
  const lower = def.lowerTolerance

  const pointCount = range === '1h' ? 30 : (range === '24h' || range === '7d' ? 60 : 48)
  const intervalMs = range === '1h' ? 120000 : (range === '24h' ? 1440000 : (range === '7d' ? 10080000 : 600000))
  const now = Date.now()

  const points: CachedTelemetryDatapoint[] = []
  let sum = 0

  for (let i = pointCount; i >= 0; i--) {
    const timestamp = new Date(now - i * intervalMs).toISOString()
    const progress = 1.0 - i / pointCount
    const drift = progress * 0.12 * (upper - nominal)
    const noise = (Math.sin(i * 0.8) * 0.4) * ((upper - nominal) * 0.25)
    let val = nominal + drift + noise

    // Seed one realistic outlier near the 4th recent sample
    if (i === 4) {
      val = upper + (upper - nominal) * 0.75
    }

    const roundedVal = Math.round(val * 100) / 100
    sum += roundedVal
    points.push({
      timestamp,
      value: roundedVal,
      isAnomaly: false,
      zScore: 0
    })
  }

  // Calculate Z-scores and mark anomalies
  const mean = sum / points.length
  let sumSquares = 0
  for (const p of points) {
    sumSquares += Math.pow(p.value - mean, 2)
  }
  const stdDev = Math.sqrt(sumSquares / points.length)

  if (stdDev > 0.0001) {
    for (const p of points) {
      const z = (p.value - mean) / stdDev
      p.zScore = Math.round(z * 100) / 100
      if (Math.abs(z) > 2.5) {
        p.isAnomaly = true
      }
    }
  }

  // Cache generated/seeded series in Redis and in-memory
  inMemoryTelemetryCache.set(memKey, points)
  try {
    await setCachedJson(cacheKey, points, 300) // 5 minute cache
  } catch {
    // Keep in-memory
  }

  return points
}

/**
 * Records a new live telemetry datapoint into the cached timeseries.
 */
export async function recordCachedTelemetryDatapoint(
  targetId: string,
  metricKey: string,
  value: number,
  isAnomaly: boolean = false
): Promise<void> {
  const ranges = ['1h', '8h', '24h']
  for (const r of ranges) {
    const memKey = `${targetId}:${metricKey}:${r}`
    const list = inMemoryTelemetryCache.get(memKey) || []
    list.push({
      timestamp: new Date().toISOString(),
      value: Math.round(value * 100) / 100,
      isAnomaly,
      zScore: isAnomaly ? 3.2 : 0.4
    })
    // Limit buffer to 120 points
    if (list.length > 120) {
      list.shift()
    }
    inMemoryTelemetryCache.set(memKey, list)

    const cacheKey = `heimdall:telemetry:series:${targetId}:${metricKey}:${r}`
    try {
      await setCachedJson(cacheKey, list, 300)
    } catch { }
  }
}

/**
 * Queries and indexes available telemetry data-points across controllers, edge schemas, and custom metrics.
 */
export async function getTelemetryDataPointCatalog(params?: {
  query?: string
  category?: string
  dataType?: string
  controllerId?: string
}): Promise<TelemetryDataPointCatalogItem[]> {
  const metrics = await getAllTelemetryMetrics()
  let catalog: TelemetryDataPointCatalogItem[] = metrics.map(m => ({
    id: m.id,
    key: m.key,
    name: m.name,
    description: m.description || '',
    category: m.category,
    dataType: m.dataType || 'Float',
    unit: m.unit,
    updateFrequencyMs: m.updateFrequencyMs || 100,
    sourceType: m.sourceType,
    pathOrSymbol: m.pathOrSymbol || '',
    activeControllers: m.activeControllers || ['ctrl-op20-ads'],
    nominalValue: m.nominalValue,
    upperTolerance: m.upperTolerance,
    lowerTolerance: m.lowerTolerance
  }))

  if (params?.category && params.category !== 'all') {
    catalog = catalog.filter(c => c.category.toLowerCase() === params.category!.toLowerCase())
  }
  if (params?.dataType && params.dataType !== 'all') {
    catalog = catalog.filter(c => c.dataType.toLowerCase() === params.dataType!.toLowerCase())
  }
  if (params?.controllerId) {
    catalog = catalog.filter(c => c.activeControllers.includes(params.controllerId!))
  }
  if (params?.query) {
    const q = params.query.toLowerCase().trim()
    catalog = catalog.filter(c =>
      c.key.toLowerCase().includes(q) ||
      c.name.toLowerCase().includes(q) ||
      c.description.toLowerCase().includes(q) ||
      c.pathOrSymbol.toLowerCase().includes(q)
    )
  }

  return catalog
}

