import type { RecipeSourceType } from '~/types/telemetry'

export type MetricCategory =
  | 'thermal'
  | 'vibration'
  | 'jitter'
  | 'resources'
  | 'pneumatics'
  | 'fieldbus'
  | 'cycle'
  | 'custom'

export interface UserTelemetryMetric {
  id: string
  key: string
  name: string
  description?: string
  unit: string
  category: MetricCategory
  dataType?: 'Boolean' | 'Int' | 'Float' | 'String'
  updateFrequencyMs?: number
  sourceType: RecipeSourceType
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

export interface RollingTrends {
  linearRegression: {
    slope: number
    intercept: number
    r2: number
  }
  movingAverageWindow: number
  movingAverages: number[]
  upperStdDevCorridor: number[]
  lowerStdDevCorridor: number[]
  trendDirection: 'Stable' | 'DriftingUp' | 'DriftingDown' | 'Volatile'
}

export interface RecipeForecast {
  recipeId: string
  recipeName: string
  targetBaseline: number
  upperTolerance: number
  lowerTolerance: number
  driftPercentage: number
  forecastCyclesUntilFailure: number
  toolWearStatus: 'Nominal' | 'Warning' | 'CriticalImpendingFailure'
  recommendedAction: string
}

export interface RecipeItem {
  recipeId: string
  recipeName: string
  description: string
}

export interface PinnedTelemetryWidget {
  id: string
  machineId: string
  machineName: string
  metricKey: string
  metricName: string
  unit: string
  currentValue: number
  status: 'nominal' | 'warning' | 'critical'
  pinnedAt: string
}

export interface CreateTelemetryMetricInput {
  key: string
  name: string
  description?: string
  unit: string
  category: MetricCategory
  dataType?: 'Boolean' | 'Int' | 'Float' | 'String'
  updateFrequencyMs?: number
  sourceType?: RecipeSourceType
  pathOrSymbol?: string
  nominalValue: number
  upperTolerance: number
  lowerTolerance: number
}
