import { defineEventHandler, getQuery } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)

  const result = partsInventoryStore.getAllParts({
    category: query.category as string,
    trackingType: query.trackingType as string,
    condition: query.condition as string,
    operationalState: query.operationalState as string,
    stockAlert: query.stockAlert as string,
    search: query.search as string,
    currency: (query.currency as string) || 'EUR'
  })

  return result
})
