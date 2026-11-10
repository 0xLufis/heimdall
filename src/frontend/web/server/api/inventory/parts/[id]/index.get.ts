import { defineEventHandler, getRouterParam, getQuery, createError } from 'h3'
import { partsInventoryStore } from '../../../../utils/partsInventoryStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Part ID parameter is required.' })
  }

  const query = getQuery(event)
  const currency = (query.currency as string) || 'EUR'

  const part = partsInventoryStore.getPartByIdOrIdentifier(id, currency)
  if (part) {
    return {
      success: true,
      part
    }
  }

  // Fallback: Check backend inventory API
  const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
  try {
    const raw = await $fetch<any>(`${backendBase}/api/v1/inventory/${id}`, {
      headers: event.headers as any
    })
    if (raw && (raw.id || raw.name)) {
      const isStock = raw.isStockItem || false
      const formattedPart = {
        id: raw.id,
        name: raw.name,
        displayName: raw.displayName || raw.name,
        customIdentifier: raw.name || raw.id,
        serialNumber: raw.serialNumber,
        category: raw.itemType === 'SoftwareAsset' || raw.itemType === 'SoftwareComponent' ? 'Software' : 'Hardware',
        subCategory: raw.technology || 'Industrial Automation',
        trackingType: isStock ? 'bulk' : 'serialized',
        condition: 'new',
        operationalState: raw.equipmentStatus === 'InMachine' || raw.equipmentStatus === 'InStorage' ? 'working' : 'degraded',
        stockAlertStatus: (raw.stockQuantity ?? 1) <= 2 ? 'low_stock' : 'optimal',
        manufacturer: raw.manufacturer || { name: 'Industrial OEM' },
        supplier: raw.supplier || { name: 'Direct Supplier' },
        owner: { type: 'organization', name: raw.organizationId || 'Plant Floor' },
        location: raw.storageLocation || 'Main Plant Floor',
        priceEur: raw.costInHUF ? Math.round(raw.costInHUF / 400) : 0,
        costInHUF: raw.costInHUF || 0,
        quantity: raw.stockQuantity ?? (isStock ? 10 : 1),
        minQuantity: raw.minStockThreshold ?? 1,
        technology: raw.technology || 'Assembly',
        isMachineLinked: !!raw.machineId,
        linkedMachineId: raw.machineId,
        metadata: raw.metadata || {}
      }
      return {
        success: true,
        part: formattedPart
      }
    }
  } catch {}

  throw createError({ statusCode: 404, message: `Part "${id}" not found.` })
})
