import type { H3Event } from 'h3'
import { createError } from 'h3'
import type { InventoryPart, PartUsageCostCenter } from '../../app/types/inventory'
import { backendFetch } from './backendFetch'

/**
 * Thin mapping between the backend inventory (`/api/v1/inventory/...`, `BaseInventoryItem`
 * JSON) and the inventory page's `InventoryPart`. The registry reads and writes the backend
 * only; nothing is cached or stored here.
 *
 * Backend fields used: id, name (identifier), displayName, itemType, serialNumber,
 * storageLocation, equipmentStatus, isStockItem, stockQuantity, minStockThreshold, costInHUF,
 * technology, manufacturer, supplier, organizationId, machineId / machine, metadata.
 * The backend has no lifecycle condition, wear / resell price, audit history or timestamps;
 * those fields are derived (condition from equipmentStatus) or left empty.
 */

export interface BackendInventoryItem {
  id: string
  name?: string | null
  displayName?: string | null
  itemType?: string | null
  serialNumber?: string | null
  storageLocation?: string | null
  equipmentStatus?: string | null
  isStockItem?: boolean | null
  stockQuantity?: number | null
  minStockThreshold?: number | null
  costInHUF?: number | null
  purchaseDate?: string | null
  technology?: string | null
  organizationId?: string | null
  manufacturer?: { id?: string; name?: string } | null
  supplier?: { id?: string; name?: string } | null
  machineId?: string | null
  machine?: { id?: string; name?: string; customIdentifier?: string | null; groupId?: string | null } | null
  metadata?: Record<string, unknown> | null
}

/** Same rates the page uses for display (EUR base). */
export const EUR_RATES: Record<string, number> = { EUR: 1, USD: 1.08, GBP: 0.86, HUF: 400 }

const SOFTWARE_TYPES = new Set(['softwareasset', 'softwarecomponent'])
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export const isBackendId = (id: string) => GUID.test(id)

const text = (value: unknown) => (typeof value === 'string' && value.trim() ? value.trim() : undefined)

/** Stations are inventory rows in the backend too, but they belong to the machines page. */
export const isRegistryItem = (raw: BackendInventoryItem) => String(raw?.itemType || '').toLowerCase() !== 'machine'

export function toInventoryPart(raw: BackendInventoryItem, currency = 'EUR'): InventoryPart {
  const isStock = !!raw.isStockItem
  const status = String(raw.equipmentStatus || '')
  const metadata = (raw.metadata || {}) as Record<string, unknown>
  const quantity = isStock ? Number(raw.stockQuantity ?? 0) : 1
  const minQuantity = isStock ? Number(raw.minStockThreshold ?? 0) : 0
  const priceEur = raw.costInHUF ? Math.round(raw.costInHUF / EUR_RATES.HUF) : 0
  const rate = EUR_RATES[currency] ?? 1
  const machineLabel = text(raw.machine?.customIdentifier) || text(raw.machine?.name)

  return {
    id: raw.id,
    customIdentifier: text(raw.name) || raw.id,
    name: text(raw.displayName) || text(raw.name) || raw.id,
    displayName: text(raw.displayName),
    category: SOFTWARE_TYPES.has(String(raw.itemType || '').toLowerCase()) ? 'Software' : 'Hardware',
    subCategory: text(metadata.partClass) || text(metadata.consumableClass) || text(raw.technology) || text(raw.itemType),
    trackingType: isStock ? 'bulk' : 'serialized',
    // The backend tracks where a part is, not its wear: a part that has been in a machine is "used".
    condition: status === 'InStorage' || !status ? 'new' : 'used',
    operationalState: status === 'UnderRepair' ? 'in_service' : status === 'Defective' || status === 'Broken' ? 'broken' : 'working',
    manufacturer: { id: raw.manufacturer?.id, name: text(raw.manufacturer?.name) || 'Unknown' },
    supplier: raw.supplier ? { id: raw.supplier.id, name: text(raw.supplier.name) || 'Unknown' } : undefined,
    owner: { type: 'organization', id: raw.organizationId || '', name: raw.organizationId || 'Plant' },
    location: text(raw.storageLocation) || machineLabel || '',
    priceEur,
    customCurrency: currency,
    priceCustomCurrency: Math.round(priceEur * rate * 100) / 100,
    estimatedResellPriceEur: priceEur,
    quantity,
    minQuantity,
    effectiveMinQuantity: minQuantity,
    stockAlertStatus: !isStock ? 'optimal' : quantity <= 0 ? 'out_of_stock' : quantity <= minQuantity ? 'low_stock' : 'optimal',
    isMachineLinked: !!raw.machineId,
    linkedMachineId: raw.machineId || undefined,
    linkedMachineName: machineLabel,
    serialNumber: text(raw.serialNumber),
    tags: [text(raw.technology), text(raw.itemType)].filter((t): t is string => !!t),
    createdAt: raw.purchaseDate || '',
    updatedAt: ''
  }
}

export function summarizeParts(parts: InventoryPart[], currency = 'EUR') {
  const total = parts.reduce((sum, p) => sum + p.priceEur * (p.trackingType === 'bulk' ? Math.max(p.quantity, 0) : 1), 0)
  const count = (pred: (p: InventoryPart) => boolean) => parts.filter(pred).length
  return {
    totalPartsCount: parts.length,
    bulkCount: count(p => p.trackingType === 'bulk'),
    serializedCount: count(p => p.trackingType === 'serialized'),
    workingCount: count(p => p.operationalState === 'working'),
    inServiceCount: count(p => p.operationalState === 'in_service'),
    evaluationCount: count(p => p.operationalState === 'evaluation'),
    brokenCount: count(p => p.operationalState === 'broken'),
    newConditionCount: count(p => p.condition === 'new'),
    usedConditionCount: count(p => p.condition === 'used'),
    donorConditionCount: count(p => p.condition === 'donor'),
    obsoleteConditionCount: count(p => p.condition === 'obsolete'),
    scrapConditionCount: count(p => p.condition === 'scrap'),
    lowStockAlertCount: count(p => p.stockAlertStatus === 'low_stock'),
    outOfStockAlertCount: count(p => p.stockAlertStatus === 'out_of_stock'),
    totalWarehouseValuationEur: total,
    totalWarehouseValuationConverted: Math.round(total * (EUR_RATES[currency] ?? 1))
  }
}

export interface PartListFilters {
  category?: string
  trackingType?: string
  condition?: string
  operationalState?: string
  stockAlert?: string
  search?: string
}

export function filterParts(parts: InventoryPart[], filters: PartListFilters): InventoryPart[] {
  const q = (filters.search || '').trim().toLowerCase()
  return parts.filter(p =>
    (!filters.category || p.category.toLowerCase() === filters.category.toLowerCase()) &&
    (!filters.trackingType || p.trackingType === filters.trackingType) &&
    (!filters.condition || p.condition === filters.condition) &&
    (!filters.operationalState || p.operationalState === filters.operationalState) &&
    (!filters.stockAlert || p.stockAlertStatus === filters.stockAlert) &&
    (!q || [p.name, p.customIdentifier, p.serialNumber, p.location, p.manufacturer.name].some(v => v && v.toLowerCase().includes(q)))
  )
}

/** Registry rows: serialized parts and software (`/inventory/parts`) plus bulk stock (`/inventory/stock`). */
export async function loadBackendParts(event: H3Event): Promise<BackendInventoryItem[]> {
  const [parts, stock] = await Promise.all([
    backendFetch<BackendInventoryItem[]>(event, 'inventory/parts'),
    backendFetch<BackendInventoryItem[]>(event, 'inventory/stock')
  ])
  const seen = new Set<string>()
  return [...(stock || []), ...(parts || [])].filter(item => {
    if (!item?.id || seen.has(item.id) || !isRegistryItem(item)) return false
    seen.add(item.id)
    return true
  })
}

export async function loadBackendPart(event: H3Event, id: string): Promise<BackendInventoryItem> {
  if (!isBackendId(id)) throw createError({ statusCode: 404, statusMessage: `Part "${id}" not found.` })
  return backendFetch<BackendInventoryItem>(event, `inventory/${encodeURIComponent(id)}`)
}

/** Body for `POST /api/v1/inventory` from an intake form. Manufacturer / supplier ids are resolved by the caller. */
export function toBackendCreatePayload(part: Partial<InventoryPart> & Record<string, any>, refs: { manufacturerId?: string; supplierId?: string } = {}) {
  const isStock = part.trackingType === 'bulk' || part.isStockItem === true
  const priceEur = Number(part.priceEur)
  return {
    itemType: part.category === 'Software' ? 'SoftwareAsset' : 'HardwareComponent',
    name: text(part.customIdentifier) || text(part.name) || `PART-${Date.now()}`,
    displayName: text(part.displayName) || text(part.name),
    serialNumber: text(part.serialNumber),
    storageLocation: text(part.location),
    technology: text(part.subCategory) || text(part.technology),
    isStockItem: isStock,
    stockQuantity: isStock ? Math.max(0, Math.round(Number(part.quantity ?? 0))) : null,
    minStockThreshold: isStock ? Math.max(0, Math.round(Number(part.minQuantity ?? 0))) : null,
    costInHUF: Number.isFinite(priceEur) && priceEur > 0 ? Math.round(priceEur * EUR_RATES.HUF) : null,
    manufacturerId: refs.manufacturerId,
    supplierId: refs.supplierId,
    equipmentStatus: 'InStorage'
  }
}

/** Body for `PUT /api/v1/inventory/{id}`: only the fields the backend can update. */
export function toBackendUpdatePayload(updates: Partial<InventoryPart> & Record<string, any>, isStock: boolean) {
  const body: Record<string, unknown> = {}
  if (text(updates.customIdentifier)) body.name = text(updates.customIdentifier)
  if (updates.name !== undefined || updates.displayName !== undefined) body.displayName = text(updates.displayName) || text(updates.name) || null
  if (updates.location !== undefined) body.storageLocation = text(updates.location) || null
  if (updates.serialNumber !== undefined) body.serialNumber = text(updates.serialNumber) || null
  if (updates.subCategory !== undefined) body.technology = text(updates.subCategory) || null
  if (isStock && updates.quantity !== undefined && Number.isFinite(Number(updates.quantity))) body.stockQuantity = Math.max(0, Math.round(Number(updates.quantity)))
  return body
}

/** Using a part needs at least one of production line, project or department. */
export function requireCostCenter(costCenter: PartUsageCostCenter | undefined | null): PartUsageCostCenter {
  const cc = costCenter || {}
  if (![cc.prodLine, cc.project, cc.department].some(v => v && String(v).trim())) {
    throw createError({
      statusCode: 400,
      statusMessage: 'Mandatory cost center tagging required: specify at least one of Production Line (prodLine), Project (project), or Department (department).'
    })
  }
  return cc
}

/** New stock level after taking `quantity` out; rejects serialized parts and over-consumption. */
export function stockAfterUse(raw: BackendInventoryItem, quantity: number): number {
  if (!raw.isStockItem) {
    throw createError({ statusCode: 409, statusMessage: 'Serialized parts are not consumed from stock: install them on the station instead.' })
  }
  const amount = Math.round(Number(quantity))
  if (!Number.isFinite(amount) || amount <= 0) throw createError({ statusCode: 400, statusMessage: 'Quantity must be a positive whole number.' })
  const available = Number(raw.stockQuantity ?? 0)
  if (amount > available) {
    throw createError({ statusCode: 400, statusMessage: `Insufficient stock: ${available} available, ${amount} requested.` })
  }
  return available - amount
}
