import { defineEventHandler, getQuery } from 'h3'
import {
  findTelemetryMetricByKey,
  getCachedTelemetryDatapoints,
  BUILTIN_METRICS
} from '../../utils/telemetryCacheStore'

const AVAILABLE_RECIPES = [
  { recipeId: 'REC-ALU-01', recipeName: 'Aluminum Lightweight Weld 1.5mm', description: 'High-speed laser stitch welding on 6061-T6 aluminum sheet' },
  { recipeId: 'REC-STL-02', recipeName: 'Structural Steel Bracket Fastening', description: 'Multi-spindle M8 class 10.9 torque-controlled torque-angle fastening' },
  { recipeId: 'REC-COMP-03', recipeName: 'Carbon Composite Adhesive Dispense', description: '2K structural polyurethane high-precision micro-bead bonding' }
]

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const machineId = (query.machineId as string) || 'm-op20'
  const metricKey = (query.metric as string) || 'cycle_time'
  const range = (query.range as string) || '8h'
  const recipeId = (query.recipeId as string) || 'REC-ALU-01'

  // 1. Resolve metric definition (built-in or user-defined)
  const metricDef = (await findTelemetryMetricByKey(metricKey)) || BUILTIN_METRICS[0]

  const metricName = metricDef?.name || metricKey
  const unit = metricDef?.unit || ''
  let nominal = metricDef?.nominalValue ?? 0
  let upper = metricDef?.upperTolerance ?? nominal
  let lower = metricDef?.lowerTolerance ?? nominal

  const selectedRecipe = AVAILABLE_RECIPES.find(r => r.recipeId.toLowerCase() === recipeId.toLowerCase()) || AVAILABLE_RECIPES[0]

  // Recipe baseline adjustments if selected
  if (selectedRecipe.recipeId === 'REC-ALU-01' && metricKey.includes('cycle')) {
    nominal = 1160.0
    upper = 1240.0
    lower = 1080.0
  } else if (selectedRecipe.recipeId === 'REC-STL-02' && metricKey.includes('cycle')) {
    nominal = 1290.0
    upper = 1360.0
    lower = 1210.0
  }

  // 2. Fetch datapoints from the cached telemetry store (Redis / in-memory ring buffer)
  const points = await getCachedTelemetryDatapoints(machineId, metricKey, range, metricDef)

  // 3. Compute statistical properties across the cached series
  let sum = 0
  for (const p of points) {
    sum += p.value
  }
  const mean = points.length > 0 ? sum / points.length : nominal

  let sumSquares = 0
  for (const p of points) {
    sumSquares += Math.pow(p.value - mean, 2)
  }
  const stdDev = points.length > 0 ? Math.sqrt(sumSquares / points.length) : 0.001

  // 4. Extract detected statistical anomalies from cached points
  const threshold = query.anomalyThreshold ? Number(query.anomalyThreshold) : 2.5
  const detectedAnomalies = []
  if (stdDev > 0.0001) {
    for (const p of points) {
      const z = (p.value - mean) / stdDev
      p.zScore = Math.round(z * 100) / 100
      if (Math.abs(z) > threshold) {
        p.isAnomaly = true
        const severity = Math.abs(z) > (threshold + 0.5) ? 'Critical' : 'Warning'
        detectedAnomalies.push({
          timestamp: p.timestamp,
          metric: metricName,
          value: p.value,
          expectedValue: Math.round(mean * 100) / 100,
          zScore: p.zScore,
          severity,
          description: `${metricName} deviated by ${z.toFixed(1)} standard deviations from nominal operating mean.`
        })
      }
    }
  }

  // 5. Rolling Statistical Trends (Linear Regression, Moving Averages, Corridors)
  const n = points.length
  let sumX = 0, sumY = 0, sumXY = 0, sumXX = 0, sumYY = 0
  for (let i = 0; i < n; i++) {
    const x = i
    const y = points[i].value
    sumX += x
    sumY += y
    sumXY += x * y
    sumXX += x * x
    sumYY += y * y
  }

  const denominator = (n * sumXX - sumX * sumX)
  const slope = denominator !== 0 ? (n * sumXY - sumX * sumY) / denominator : 0
  const intercept = n > 0 ? (sumY - slope * sumX) / n : nominal

  const totalSumSquares = sumYY - (sumY * sumY) / (n > 0 ? n : 1)
  const regressionSumSquares = slope * (sumXY - (sumX * sumY) / (n > 0 ? n : 1))
  const r2 = totalSumSquares > 0.0001 ? Math.min(1.0, Math.max(0.0, regressionSumSquares / totalSumSquares)) : 0

  const window = 5
  const movingAverages: number[] = []
  const upperStdDevCorridor: number[] = []
  const lowerStdDevCorridor: number[] = []

  for (let i = 0; i < n; i++) {
    const startIdx = Math.max(0, i - window + 1)
    const count = i - startIdx + 1
    let windowSum = 0
    for (let w = startIdx; w <= i; w++) {
      windowSum += points[w].value
    }
    const ma = Math.round((windowSum / count) * 100) / 100
    movingAverages.push(ma)
    upperStdDevCorridor.push(Math.round((ma + 1.5 * stdDev) * 100) / 100)
    lowerStdDevCorridor.push(Math.round((ma - 1.5 * stdDev) * 100) / 100)
  }

  let trendDirection: 'Stable' | 'DriftingUp' | 'DriftingDown' | 'Volatile' = 'Stable'
  const normalizedSlope = slope / (nominal !== 0 ? nominal : 1.0)
  if (normalizedSlope > 0.002) {
    trendDirection = 'DriftingUp'
  } else if (normalizedSlope < -0.002) {
    trendDirection = 'DriftingDown'
  } else if (stdDev / (mean !== 0 ? mean : 1.0) > 0.12) {
    trendDirection = 'Volatile'
  }

  // 6. Recipe-Based Forecast System
  const latestVal = points.length > 0 ? points[points.length - 1].value : nominal
  const driftPercentage = nominal !== 0 ? Math.round(((latestVal - nominal) / nominal) * 10000) / 100 : 0
  let forecastCyclesUntilFailure = 1200
  let toolWearStatus: 'Nominal' | 'Warning' | 'CriticalImpendingFailure' = 'Nominal'
  let recommendedAction = 'Operating within normal recipe tolerance envelope.'

  if (Math.abs(driftPercentage) >= 8.0 || Math.abs(latestVal - nominal) > (upper - nominal) * 0.75) {
    forecastCyclesUntilFailure = Math.max(45, Math.floor(450 - Math.abs(driftPercentage) * 18))
    toolWearStatus = 'CriticalImpendingFailure'
    recommendedAction = 'Impending process failure detected: Schedule electrode/tool tip replacement before next shift.'
  } else if (Math.abs(driftPercentage) >= 3.5 || Math.abs(latestVal - nominal) > (upper - nominal) * 0.45) {
    forecastCyclesUntilFailure = Math.max(250, Math.floor(850 - Math.abs(driftPercentage) * 40))
    toolWearStatus = 'Warning'
    recommendedAction = 'Accelerated tool degradation: Inspect calibration and lubrication parameters during scheduled break.'
  }

  return {
    machineId,
    metricName,
    metricKey,
    unit,
    nominalValue: nominal,
    upperTolerance: upper,
    lowerTolerance: lower,
    points,
    detectedAnomalies,
    statisticalTrends: {
      linearRegression: {
        slope: Math.round(slope * 10000) / 10000,
        intercept: Math.round(intercept * 100) / 100,
        r2: Math.round(r2 * 1000) / 1000
      },
      movingAverageWindow: window,
      movingAverages,
      upperStdDevCorridor,
      lowerStdDevCorridor,
      trendDirection
    },
    recipeForecast: {
      recipeId: selectedRecipe.recipeId,
      recipeName: selectedRecipe.recipeName,
      targetBaseline: nominal,
      upperTolerance: upper,
      lowerTolerance: lower,
      driftPercentage,
      forecastCyclesUntilFailure,
      toolWearStatus,
      recommendedAction
    },
    availableRecipes: AVAILABLE_RECIPES,
    source: 'telemetry_cache',
    isUserDefined: metricDef.isUserDefined
  }
})
