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
  sourceType: RecipeSourceType
  pathOrSymbol?: string
  nominalValue: number
  upperTolerance: number
  lowerTolerance: number
  isUserDefined: boolean
  createdAt: string
  updatedAt?: string
}

export interface CreateTelemetryMetricInput {
  key: string
  name: string
  description?: string
  unit: string
  category: MetricCategory
  sourceType?: RecipeSourceType
  pathOrSymbol?: string
  nominalValue: number
  upperTolerance: number
  lowerTolerance: number
}
