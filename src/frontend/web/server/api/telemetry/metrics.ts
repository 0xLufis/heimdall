import { defineEventHandler, getMethod, readBody, getQuery, createError } from 'h3'
import {
  getAllTelemetryMetrics,
  findTelemetryMetricByKey,
  saveCustomTelemetryMetric,
  deleteCustomTelemetryMetric,
  type TelemetryMetricDefinition
} from '../../utils/telemetryCacheStore'

export default defineEventHandler(async (event) => {
  const method = getMethod(event)

  // GET: Fetch all metrics (built-in + user-defined)
  if (method === 'GET') {
    const query = getQuery(event)
    const key = query.key as string | undefined

    if (key) {
      const found = await findTelemetryMetricByKey(key)
      if (!found) {
        throw createError({ statusCode: 404, statusMessage: `Telemetry metric "${key}" not found.` })
      }
      return found
    }

    const all = await getAllTelemetryMetrics()
    return all
  }

  // POST: Create or update a user-defined metric
  if (method === 'POST') {
    const body = await readBody(event)

    if (!body || !body.key || !body.name || !body.unit) {
      throw createError({
        statusCode: 400,
        statusMessage: 'Metric "key", "name", and "unit" are required to define a telemetry metric.'
      })
    }

    const sanitizedKey = String(body.key)
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9_]/g, '_')

    const newMetric: TelemetryMetricDefinition = {
      id: body.id || `custom-met-${Date.now()}`,
      key: sanitizedKey,
      name: String(body.name).trim(),
      description: body.description ? String(body.description).trim() : undefined,
      unit: String(body.unit).trim(),
      category: body.category || 'custom',
      sourceType: body.sourceType || 'SystemCim',
      pathOrSymbol: body.pathOrSymbol || undefined,
      nominalValue: Number(body.nominalValue ?? 50.0),
      upperTolerance: Number(body.upperTolerance ?? 80.0),
      lowerTolerance: Number(body.lowerTolerance ?? 20.0),
      isUserDefined: true,
      createdAt: new Date().toISOString()
    }

    const saved = await saveCustomTelemetryMetric(newMetric)
    return {
      success: true,
      metric: saved
    }
  }

  // DELETE: Delete a user-defined metric
  if (method === 'DELETE') {
    const query = getQuery(event)
    const key = query.key as string

    if (!key) {
      throw createError({ statusCode: 400, statusMessage: 'Parameter "key" is required.' })
    }

    const found = await findTelemetryMetricByKey(key)
    if (found && !found.isUserDefined) {
      throw createError({
        statusCode: 403,
        statusMessage: 'Cannot delete system built-in telemetry metric.'
      })
    }

    const success = await deleteCustomTelemetryMetric(key)
    return { success }
  }

  throw createError({ statusCode: 405, statusMessage: 'Method not allowed' })
})
