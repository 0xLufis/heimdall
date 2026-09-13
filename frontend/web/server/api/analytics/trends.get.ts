import { defineEventHandler, getQuery } from 'h3'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const machineId = (query.machineId as string) || 'm-op20'
  const metric = (query.metric as string) || 'cycle_time'
  const range = (query.range as string) || '8h'

  let unit = 'ms'
  let nominal = 1200.0
  let upper = 1280.0
  let lower = 1120.0
  let metricName = 'Cycle Time Deviation'

  if (metric.toLowerCase().includes('temp')) {
    metricName = 'Temperature Drift'
    unit = '°C'
    nominal = 48.5
    upper = 65.0
    lower = 25.0
  } else if (metric.toLowerCase().includes('vib')) {
    metricName = 'Spindle Vibration Index'
    unit = 'mm/s'
    nominal = 1.45
    upper = 2.80
    lower = 0.50
  } else if (metric.toLowerCase().includes('err')) {
    metricName = 'Micro-Fault Frequency'
    unit = 'faults/hr'
    nominal = 0.4
    upper = 2.0
    lower = 0.0
  }

  const pointCount = range === '1h' ? 30 : (range === '24h' || range === '7d' ? 60 : 48)
  const intervalMs = range === '1h' ? 120000 : (range === '24h' ? 1440000 : (range === '7d' ? 10080000 : 600000))
  const now = Date.now()

  const points = []
  let sum = 0

  for (let i = pointCount; i >= 0; i--) {
    const timestamp = new Date(now - i * intervalMs).toISOString()
    const progress = 1.0 - i / pointCount
    const drift = progress * 0.15 * (upper - nominal)
    const noise = (Math.sin(i * 0.8) * 0.5) * ((upper - nominal) * 0.3)
    let val = nominal + drift + noise

    if (i === 4) {
      val = upper + (upper - nominal) * 0.8 // Spiked outlier
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

  const mean = sum / points.length
  let sumSquares = 0
  for (const p of points) {
    sumSquares += Math.pow(p.value - mean, 2)
  }
  const stdDev = Math.sqrt(sumSquares / points.length)

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
    unit,
    nominalValue: nominal,
    upperTolerance: upper,
    lowerTolerance: lower,
    points,
    detectedAnomalies
  }
})
