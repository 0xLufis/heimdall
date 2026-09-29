import { defineEventHandler, getQuery } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const currency = (query.currency as string) || 'EUR'

  const { reporting } = partsInventoryStore.getMachineSpareParts(currency)
  return {
    success: true,
    reporting
  }
})
