import { defineEventHandler } from 'h3'
import { partsInventoryStore } from '../../../utils/partsInventoryStore'

export default defineEventHandler(() => {
  const templates = partsInventoryStore.getTemplates()
  return {
    success: true,
    totalCount: templates.length,
    templates
  }
})
