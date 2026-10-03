import { defineEventHandler, getQuery } from 'h3'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  if (query.itemId) {
    return partsInventoryStore.getItemTags(String(query.itemId))
  }
  return partsInventoryStore.getKnownTags()
})
