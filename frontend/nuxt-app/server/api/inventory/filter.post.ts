import { defineEventHandler, readBody } from 'h3'

export default defineEventHandler(async (event) => {
  const body = await readBody(event) || {}
  const {
    query = '',
    type = 'all',
    sortBy = 'name',
    sortOrder = 'asc',
    manufacturerId = '',
    teamId = ''
  } = body

  try {
    const raw = await $fetch<any[]>('http://localhost:5099/api/inventory', {
      headers: event.headers as any
    })

    let items = (raw || []).map((item: any) => ({
      id: item.id || item.Id,
      name: item.name || item.Name || '',
      displayName: item.displayName || item.DisplayName,
      serialNumber: item.serialNumber || item.SerialNumber,
      itemType: item.itemType || (item.customIdentifier ? 'machine' : 'hardware'),
      customIdentifier: item.customIdentifier || item.CustomIdentifier,
      costInHUF: item.costInHUF || item.CostInHUF || 0,
      purchaseDate: item.purchaseDate || item.PurchaseDate,
      manufacturer: item.manufacturer || item.Manufacturer,
      responsibleTeams: item.responsibleTeams || item.ResponsibleTeams || [],
      metadata: item.metadata || item.Metadata || {}
    }))

    if (type && type !== 'all') {
      items = items.filter(item => {
        const itemType = (item.itemType || '').toLowerCase()
        return itemType.includes(type.toLowerCase())
      })
    }

    if (query) {
      const q = query.toLowerCase().trim()
      items = items.filter(item => {
        const nameMatch = (item.name || '').toLowerCase().includes(q)
        const serialMatch = (item.serialNumber || '').toLowerCase().includes(q)
        const mfrMatch = (item.manufacturer?.name || '').toLowerCase().includes(q)
        const identMatch = (item.customIdentifier || '').toLowerCase().includes(q)
        return nameMatch || serialMatch || mfrMatch || identMatch
      })
    }

    if (manufacturerId) {
      items = items.filter(item => item.manufacturer?.id === manufacturerId)
    }

    if (teamId) {
      items = items.filter(item => item.responsibleTeams?.some((t: any) => t.id === teamId))
    }

    items.sort((a: any, b: any) => {
      let valA = a[sortBy] ?? a.metadata?.[sortBy] ?? ''
      let valB = b[sortBy] ?? b.metadata?.[sortBy] ?? ''

      if (typeof valA === 'string') valA = valA.toLowerCase()
      if (typeof valB === 'string') valB = valB.toLowerCase()

      if (valA < valB) return sortOrder === 'asc' ? -1 : 1
      if (valA > valB) return sortOrder === 'asc' ? 1 : -1
      return 0
    })

    const totalCost = items.reduce((sum, item) => sum + (item.costInHUF || 0), 0)

    return {
      items,
      totalCount: items.length,
      totalCostHuf: totalCost
    }
  } catch {
    return {
      items: [],
      totalCount: 0,
      totalCostHuf: 0
    }
  }
})
