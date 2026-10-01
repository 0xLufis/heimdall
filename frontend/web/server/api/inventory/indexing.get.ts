import { defineEventHandler } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(async () => {
  const indexed = partsInventoryStore.getIndexedFields()
  return {
    success: true,
    ...indexed
  }
})
