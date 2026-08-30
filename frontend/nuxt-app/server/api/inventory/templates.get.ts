import { defineEventHandler, getQuery } from 'h3'
import { DEFAULT_ASSET_TEMPLATES } from '~/utils/defaultAssetTemplates'

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const category = (query.category as string || '').toLowerCase()
  const search = (query.search as string || '').toLowerCase()

  let templates = [...DEFAULT_ASSET_TEMPLATES]

  if (category && category !== 'all') {
    templates = templates.filter(t => t.category.toLowerCase() === category)
  }

  if (search) {
    templates = templates.filter(t => 
      t.name.toLowerCase().includes(search) || 
      t.description.toLowerCase().includes(search) ||
      t.tags?.some(tag => tag.toLowerCase().includes(search))
    )
  }

  return {
    templates,
    totalCount: templates.length
  }
})
