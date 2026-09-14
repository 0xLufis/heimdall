import { defineEventHandler, getQuery } from 'h3'
import {
  findTelemetryMetricByKey,
  getCachedTelemetryDatapoints,
  BUILTIN_METRICS
} from '../../utils/telemetryCacheStore'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const machineId = (query.machineId as string) || 'm-op20'
  const metricKey = (query.metric as string) || 'cycle_time'
  const range = (query.range as string) || '8h'

  // 1. Resolve metric definition (built-in or user-defined)
  const metricDef = (await findTelemetryMetricByKey(metricKey)) || BUILTIN_METRICS[0]

  const metricName = metricDef.name
  const unit = metricDef.unit
  const nominal = metricDef.nominalValue
  const upper = metricDef.upperTolerance
  const lower = metricDef.lowerTolerance

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
  const detectedAnomalies = []
  if (stdDev > 0.0001) {
    for (const p of points) {
      const z = (p.value - mean) / stdDev
      p.zScore = Math.round(z * 100) / 100
      if (Math.abs(z) > 2.5) {
        p.isAnomaly = true
        const severity = Math.abs(z) > 3.0 ? 'Critical' : 'Warning'
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
    source: 'telemetry_cache',
    isUserDefined: metricDef.isUserDefined
  }
})
