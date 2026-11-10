import { defineEventHandler, getQuery } from 'h3'
import { getTelemetryDataPointCatalog } from '../../utils/telemetryCacheStore'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const q = query.q as string | undefined
  const category = query.category as string | undefined
  const dataType = query.dataType as string | undefined
  const controllerId = query.controllerId as string | undefined

  const items = await getTelemetryDataPointCatalog({
    query: q,
    category,
    dataType,
    controllerId
  })

  return {
    total: items.length,
    items
  }
})
