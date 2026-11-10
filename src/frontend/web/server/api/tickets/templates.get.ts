import { defineEventHandler, getQuery } from 'h3'
import { getTemplatesStore } from '../../utils/ticketTemplatesStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const categoryFilter = (query.category as string || '').trim().toLowerCase()
  const machineTypeFilter = (query.machineType as string || '').trim().toLowerCase()
  const searchQuery = (query.search as string || query.q as string || '').trim().toLowerCase()

  let templates = getTemplatesStore()

  if (categoryFilter && categoryFilter !== 'all') {
    templates = templates.filter(t => t.category.toLowerCase() === categoryFilter)
  }

  if (machineTypeFilter && machineTypeFilter !== 'all') {
    templates = templates.filter(t =>
      t.affectedMachineTypes?.some(m => m.toLowerCase().includes(machineTypeFilter))
    )
  }

  if (searchQuery) {
    templates = templates.filter(t =>
      t.errorCode.toLowerCase().includes(searchQuery) ||
      t.shortDescription.toLowerCase().includes(searchQuery) ||
      t.detailedDescription.toLowerCase().includes(searchQuery) ||
      t.errorGroup.toLowerCase().includes(searchQuery) ||
      t.defaultTags.some(tag => tag.toLowerCase().includes(searchQuery)) ||
      t.affectedMachineTypes?.some(m => m.toLowerCase().includes(searchQuery))
    )
  }

  const allTemplates = getTemplatesStore()
  const categories = [...new Set(allTemplates.map(t => t.category))]
  const errorGroups = [...new Set(allTemplates.map(t => t.errorGroup))]

  return {
    templates,
    total: templates.length,
    categories,
    errorGroups
  }
})
