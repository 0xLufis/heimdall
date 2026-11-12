import { describe, it, expect, vi, beforeEach } from 'vitest'

const backendFetch = vi.hoisted(() => vi.fn())
vi.mock('../../../src/frontend/web/server/utils/backendFetch', () => ({ backendFetch }))

import {
  toInventoryPart,
  summarizeParts,
  filterParts,
  toBackendCreatePayload,
  toBackendUpdatePayload,
  requireCostCenter,
  stockAfterUse,
  loadBackendParts,
  loadBackendPart,
  type BackendInventoryItem
} from '../../../src/frontend/web/server/utils/inventoryBackend'

const stockItem: BackendInventoryItem = {
  id: 'c1b15015-cff1-5825-8ed4-6dfe1627ab63',
  name: 'STK-ALUM-4040-1M',
  displayName: 'Strut Profile 1000mm',
  itemType: 'HardwareComponent',
  serialNumber: 'LOT-14',
  storageLocation: 'Rack STRUT-A1',
  equipmentStatus: 'InStorage',
  isStockItem: true,
  stockQuantity: 4,
  minStockThreshold: 10,
  costInHUF: 8000,
  technology: 'Assembly',
  organizationId: 'org-maintenance',
  manufacturer: { id: 'm-1', name: 'Bosch Rexroth' },
  supplier: { id: 's-1', name: 'RS Components' },
  metadata: { consumableClass: 'STRUT' }
}

const installedCamera: BackendInventoryItem = {
  id: 'dabfa5b8-1a5a-5d66-8571-4365a123039d',
  name: 'COMP-CAM-9912-L01-OP090-1',
  displayName: 'Cognex 9912 Camera',
  itemType: 'HardwareComponent',
  equipmentStatus: 'InMachine',
  isStockItem: false,
  machineId: '7406a390-67bc-5335-89b5-2acf43d2ff94',
  machine: { customIdentifier: 'LINE-01-OP090' },
  manufacturer: { name: 'Cognex' }
}

describe('Inventory backend mapping', () => {
  it('maps a backend stock item to a bulk part with stock alert and EUR price', () => {
    const part = toInventoryPart(stockItem, 'HUF')
    expect(part).toMatchObject({
      id: stockItem.id,
      customIdentifier: 'STK-ALUM-4040-1M',
      name: 'Strut Profile 1000mm',
      category: 'Hardware',
      subCategory: 'STRUT',
      trackingType: 'bulk',
      condition: 'new',
      quantity: 4,
      minQuantity: 10,
      stockAlertStatus: 'low_stock',
      priceEur: 20,
      priceCustomCurrency: 8000,
      location: 'Rack STRUT-A1',
      manufacturer: { name: 'Bosch Rexroth' },
      supplier: { name: 'RS Components' }
    })
    expect(toInventoryPart({ ...stockItem, stockQuantity: 0 }).stockAlertStatus).toBe('out_of_stock')
    expect(toInventoryPart({ ...stockItem, stockQuantity: 11 }).stockAlertStatus).toBe('optimal')
  })

  it('maps an installed component to a serialized, machine-linked unit without a stock alert', () => {
    const part = toInventoryPart(installedCamera)
    expect(part).toMatchObject({
      trackingType: 'serialized',
      quantity: 1,
      minQuantity: 0,
      stockAlertStatus: 'optimal',
      condition: 'used',
      isMachineLinked: true,
      linkedMachineName: 'LINE-01-OP090',
      location: 'LINE-01-OP090'
    })
    expect(toInventoryPart({ ...installedCamera, equipmentStatus: 'UnderRepair' }).operationalState).toBe('in_service')
    expect(toInventoryPart({ id: 'x', itemType: 'SoftwareAsset' }).category).toBe('Software')
  })

  it('summarizes and filters the mapped parts', () => {
    const parts = [toInventoryPart(stockItem), toInventoryPart(installedCamera)]
    expect(summarizeParts(parts)).toMatchObject({ totalPartsCount: 2, bulkCount: 1, serializedCount: 1, lowStockAlertCount: 1, totalWarehouseValuationEur: 80 })
    expect(filterParts(parts, { trackingType: 'bulk' }).map(p => p.id)).toEqual([stockItem.id])
    expect(filterParts(parts, { search: 'cognex' }).map(p => p.id)).toEqual([installedCamera.id])
    expect(filterParts(parts, { stockAlert: 'low_stock' })).toHaveLength(1)
  })

  it('reads parts and stock, without stations and without duplicates', async () => {
    backendFetch.mockReset()
    backendFetch.mockImplementation(async (_event: unknown, path: string) =>
      path === 'inventory/stock'
        ? [stockItem]
        : [installedCamera, stockItem, { id: 'm-1', name: 'L01-OP090', itemType: 'Machine' }])
    const rows = await loadBackendParts({} as any)
    expect(backendFetch.mock.calls.map(c => c[1]).sort()).toEqual(['inventory/parts', 'inventory/stock'])
    expect(rows.map(r => r.id)).toEqual([stockItem.id, installedCamera.id])
  })

  it('treats a non-backend id (old fixture ids such as prt-ser-101) as not found without calling the backend', async () => {
    backendFetch.mockReset()
    await expect(loadBackendPart({} as any, 'prt-ser-101')).rejects.toMatchObject({ statusCode: 404 })
    expect(backendFetch).not.toHaveBeenCalled()
  })
})

describe('Inventory backend writes', () => {
  beforeEach(() => backendFetch.mockReset())

  it('builds a create payload the backend accepts', () => {
    const payload = toBackendCreatePayload(
      { name: 'Proximity sensor', customIdentifier: 'PRX-01', category: 'Hardware', trackingType: 'bulk', quantity: 12, minQuantity: 3, priceEur: 25, location: 'Crib A' },
      { manufacturerId: 'm-1' }
    )
    expect(payload).toMatchObject({
      itemType: 'HardwareComponent',
      name: 'PRX-01',
      displayName: 'Proximity sensor',
      isStockItem: true,
      stockQuantity: 12,
      minStockThreshold: 3,
      costInHUF: 10000,
      storageLocation: 'Crib A',
      manufacturerId: 'm-1'
    })
    expect(toBackendCreatePayload({ name: 'HMI runtime', category: 'Software' }).itemType).toBe('SoftwareAsset')
  })

  it('maps page fields onto the backend item: identifier vs. display name, stock only for bulk', () => {
    expect(toBackendUpdatePayload({ customIdentifier: 'NEW-ID', name: 'New name', location: 'Bin 4', quantity: 7 }, true))
      .toEqual({ name: 'NEW-ID', displayName: 'New name', storageLocation: 'Bin 4', stockQuantity: 7 })
    expect(toBackendUpdatePayload({ name: 'Camera', quantity: 3 }, false)).toEqual({ displayName: 'Camera' })
  })

  it('requires a cost center and enough stock to use a part', () => {
    expect(() => requireCostCenter({ technician: 'Ann' })).toThrow(/cost center/i)
    expect(requireCostCenter({ project: 'PRJ-1' })).toEqual({ project: 'PRJ-1' })
    expect(stockAfterUse(stockItem, 3)).toBe(1)
    expect(() => stockAfterUse(stockItem, 5)).toThrow(/Insufficient stock/)
    expect(() => stockAfterUse(stockItem, 0)).toThrow(/positive/)
    expect(() => stockAfterUse(installedCamera, 1)).toThrow(/Serialized/)
  })
})
