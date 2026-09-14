import { ref, computed } from 'vue'
import type { UserTelemetryMetric, CreateTelemetryMetricInput } from '~/types/telemetryMetrics'

// Default fallback catalog if network is unavailable
const defaultBuiltinMetrics: UserTelemetryMetric[] = [
  {
    id: 'met-cycle-time',
    key: 'cycle_time',
    name: 'Cycle Time Deviation',
    description: 'Actual operating station cycle duration compared to engineered tact time',
    unit: 'ms',
    category: 'cycle',
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'MAIN.fbStation.nActualCycleTime',
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
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'TwinCAT_System.TaskInfo.CycleJitter',
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
    sourceType: 'BeckhoffAds',
    pathOrSymbol: 'Axis1.stDriveState.fMotorTemp',
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
    sourceType: 'BeckhoffEtherCat',
    pathOrSymbol: 'IO.Terminal_EL3632.VibRMS',
    nominalValue: 1.45,
    upperTolerance: 2.80,
    lowerTolerance: 0.50,
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
    sourceType: 'SystemCim',
    pathOrSymbol: 'Win32_PerfFormattedData_PerfOS_Processor.PercentProcessorTime',
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
    sourceType: 'SystemCim',
    pathOrSymbol: 'Win32_OperatingSystem.FreePhysicalMemory',
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
    sourceType: 'OpcUaSubscription',
    pathOrSymbol: 'ns=2;s=Pneumatics.PressureSensor.ActualBar',
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
    sourceType: 'BeckhoffEtherCat',
    pathOrSymbol: 'Master0.Port0.CrcErrorsPerMin',
    nominalValue: 0.2,
    upperTolerance: 2.0,
    lowerTolerance: 0.0,
    isUserDefined: false,
    createdAt: '2026-01-01T00:00:00.000Z'
  }
]

const globalMetrics = ref<UserTelemetryMetric[]>(defaultBuiltinMetrics)
const isMetricsLoading = ref(false)

export const useTelemetryMetrics = () => {
  const metrics = globalMetrics
  const isLoading = isMetricsLoading

  const userDefinedMetrics = computed(() => {
    return metrics.value.filter(m => m.isUserDefined)
  })

  const builtinMetrics = computed(() => {
    return metrics.value.filter(m => !m.isUserDefined)
  })

  const fetchMetrics = async () => {
    isLoading.value = true
    try {
      const data = await $fetch<UserTelemetryMetric[]>('/api/telemetry/metrics')
      if (Array.isArray(data) && data.length > 0) {
        metrics.value = data
      }
    } catch {
      // Retain in-memory defaults if API unavailable
    } finally {
      isLoading.value = false
    }
  }

  const createCustomMetric = async (input: CreateTelemetryMetricInput): Promise<UserTelemetryMetric> => {
    const sanitizedKey = input.key.trim().toLowerCase().replace(/[^a-z0-9_]/g, '_')
    const payload: Partial<UserTelemetryMetric> = {
      ...input,
      key: sanitizedKey,
      sourceType: input.sourceType || 'SystemCim',
      isUserDefined: true
    }

    try {
      const res = await $fetch<{ success: boolean; metric: UserTelemetryMetric }>('/api/telemetry/metrics', {
        method: 'POST',
        body: payload
      })
      if (res && res.metric) {
        // Upsert into local list
        const idx = metrics.value.findIndex(m => m.key === res.metric.key)
        if (idx !== -1) {
          metrics.value[idx] = res.metric
        } else {
          metrics.value.push(res.metric)
        }
        return res.metric
      }
    } catch {
      // Fallback local addition if offline
    }

    const localNewMetric: UserTelemetryMetric = {
      id: `local-met-${Date.now()}`,
      key: sanitizedKey,
      name: input.name,
      description: input.description,
      unit: input.unit,
      category: input.category,
      sourceType: input.sourceType || 'SystemCim',
      pathOrSymbol: input.pathOrSymbol,
      nominalValue: input.nominalValue,
      upperTolerance: input.upperTolerance,
      lowerTolerance: input.lowerTolerance,
      isUserDefined: true,
      createdAt: new Date().toISOString()
    }

    const existingIdx = metrics.value.findIndex(m => m.key === sanitizedKey)
    if (existingIdx !== -1) {
      metrics.value[existingIdx] = localNewMetric
    } else {
      metrics.value.push(localNewMetric)
    }

    return localNewMetric
  }

  const deleteCustomMetric = async (key: string): Promise<boolean> => {
    try {
      await $fetch('/api/telemetry/metrics', {
        method: 'DELETE',
        query: { key }
      })
    } catch {
      // Fallback
    }

    metrics.value = metrics.value.filter(m => m.key !== key)
    return true
  }

  const getMetricByKey = (key: string): UserTelemetryMetric | undefined => {
    const lowerKey = key.toLowerCase()
    return metrics.value.find(m => m.key.toLowerCase() === lowerKey)
  }

  return {
    metrics,
    userDefinedMetrics,
    builtinMetrics,
    isLoading,
    fetchMetrics,
    createCustomMetric,
    deleteCustomMetric,
    getMetricByKey
  }
}
