import { defineEventHandler, getQuery } from 'h3'
import { filterParts, loadBackendParts, summarizeParts, toInventoryPart } from '../../../utils/inventoryBackend'

/** Parts registry: backend parts and stock mapped to the page's InventoryPart, with KPIs. */
export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const currency = (query.currency as string) || 'EUR'
  const parts = (await loadBackendParts(event)).map(raw => toInventoryPart(raw, currency))
  const filtered = filterParts(parts, {
    category: query.category as string,
    trackingType: query.trackingType as string,
    condition: query.condition as string,
    operationalState: query.operationalState as string,
    stockAlert: query.stockAlert as string,
    search: query.search as string
  })
  return { parts: filtered, kpis: summarizeParts(parts, currency) }
})
