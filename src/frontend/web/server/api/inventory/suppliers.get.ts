import { defineEventHandler } from 'h3'
import { partsInventoryStore } from '../../utils/partsInventoryStore'

export default defineEventHandler(() => {
  return partsInventoryStore.getSuppliers()
})
