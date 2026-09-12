import { defineEventHandler, readBody, getQuery, getMethod, getHeader } from 'h3'
import { getCachedJson, setCachedJson } from '../../utils/redis'

// Resilient default seed inventory if backend is offline or starting up
const SEED_INVENTORY = [
  {
    id: 'comp-101',
    name: 'Spindle Motor Assembly 15kW',
    displayName: 'Main CNC Spindle Motor',
    serialNumber: 'SN-SPINDLE-994',
    itemType: 'hardware',
    customIdentifier: 'MTR-OP10-01',
    costInHUF: 1850000,
    purchaseDate: '2023-04-12T00:00:00Z',
    manufacturer: { id: 'mfr-siemens', name: 'Siemens' },
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'OP10 Main Spindle Mount',
    technology: 'Assembly',
    metadata: { Power: '15kW', MaxRPM: '12000', Voltage: '400V', Station: 'OP10' }
  },
  {
    id: 'comp-102',
    name: 'Coolant Flow Sensor Array',
    displayName: 'Digital Flow Meter',
    serialNumber: 'SN-SENSOR-441',
    itemType: 'hardware',
    customIdentifier: 'SNS-OP10-02',
    costInHUF: 320000,
    purchaseDate: '2024-01-15T00:00:00Z',
    manufacturer: { id: 'mfr-ifm', name: 'IFM Electronic' },
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'OP10 Feed Line',
    technology: 'Assembly',
    metadata: { PressureRange: '0-10Bar', Protocol: 'IO-Link', Station: 'OP10' }
  },
  {
    id: 'comp-201',
    name: 'KUKA Servo Driver Module 30A',
    displayName: 'Primary Axis Servo Drive',
    serialNumber: 'SN-SERVO-881',
    itemType: 'hardware',
    customIdentifier: 'SRV-OP20-01',
    costInHUF: 940000,
    purchaseDate: '2023-09-01T00:00:00Z',
    manufacturer: { id: 'mfr-kuka', name: 'KUKA' },
    responsibleTeams: [{ id: 'team-elec', name: 'Electrical Engineering' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'OP20 Axis Drive',
    technology: 'Welding',
    metadata: { Voltage: '400V', Current: '30A', Station: 'OP20' }
  },
  {
    id: 'comp-301',
    name: 'Cognex In-Sight 9000 Camera',
    displayName: 'High-Res Optical Inspector',
    serialNumber: 'SN-CAM-9081',
    itemType: 'hardware',
    customIdentifier: 'CAM-OP30-01',
    costInHUF: 2100000,
    purchaseDate: '2024-02-10T00:00:00Z',
    manufacturer: { id: 'mfr-cognex', name: 'Cognex' },
    responsibleTeams: [{ id: 'team-quality', name: 'Quality Automation' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'OP30 Camera Mount',
    technology: 'Test',
    metadata: { Resolution: '12MP', FPS: '60', Interface: 'GigE', Station: 'OP30' }
  },
  {
    id: 'comp-spare-1',
    name: 'Backup Servo Driver Module 30A',
    displayName: 'Spare Servo Drive',
    serialNumber: 'SN-SERVO-SPARE-01',
    itemType: 'hardware',
    customIdentifier: 'SPARE-SRV-01',
    costInHUF: 940000,
    purchaseDate: '2024-01-10T00:00:00Z',
    manufacturer: { id: 'mfr-kuka', name: 'KUKA' },
    responsibleTeams: [{ id: 'team-elec', name: 'Electrical Engineering' }],
    isStockItem: false,
    equipmentStatus: 'InStorage',
    storageLocation: 'Shelf A3-4',
    technology: 'Welding',
    metadata: { Status: 'InStorage', Shelf: 'A3-4' }
  },
  {
    id: 'comp-spare-2',
    name: 'Cognex In-Sight 9000 Camera (Spare Unit)',
    displayName: 'Spare Vision Inspector',
    serialNumber: 'SN-CAM-SPARE-02',
    itemType: 'hardware',
    customIdentifier: 'SPARE-CAM-02',
    costInHUF: 2100000,
    purchaseDate: '2024-03-01T00:00:00Z',
    manufacturer: { id: 'mfr-cognex', name: 'Cognex' },
    responsibleTeams: [{ id: 'team-quality', name: 'Quality Automation' }],
    isStockItem: false,
    equipmentStatus: 'InStorage',
    storageLocation: 'Shelf B1-2',
    technology: 'Test',
    metadata: { Status: 'InStorage', Shelf: 'B1-2' }
  },
  {
    id: 'stock-101',
    name: 'M8 High-Tensile Hex Cap Fastener Screws',
    displayName: 'M8x25mm Assembly Bolts',
    serialNumber: null,
    itemType: 'hardware',
    customIdentifier: 'STK-SCRW-M8',
    costInHUF: 45000,
    purchaseDate: '2024-02-01T00:00:00Z',
    manufacturer: { id: 'mfr-wuerth', name: 'Würth' },
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    isStockItem: true,
    stockQuantity: 9,
    minStockThreshold: 3,
    storageLocation: 'Bin 42-B',
    technology: 'Fastening',
    metadata: { Quantity: '9', Batch: 'LOT-2026-X', Bin: '42-B' }
  },
  {
    id: 'stock-102',
    name: 'Festo QS-1/4-8 Pneumatic Push-In Fittings',
    displayName: 'Pneumatic Quick Couplers',
    serialNumber: null,
    itemType: 'hardware',
    customIdentifier: 'STK-PNEU-QS',
    costInHUF: 82000,
    purchaseDate: '2024-02-15T00:00:00Z',
    manufacturer: { id: 'mfr-festo', name: 'Festo' },
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    isStockItem: true,
    stockQuantity: 24,
    minStockThreshold: 5,
    storageLocation: 'Bin 18-A',
    technology: 'Assembly',
    metadata: { Quantity: '24', Batch: 'LOT-2026-F', Bin: '18-A' }
  },
  {
    id: 'stock-103',
    name: 'Industrial Dispenser Nozzle Tips 0.25mm',
    displayName: 'Precision Glue Dispenser Nozzles',
    serialNumber: null,
    itemType: 'hardware',
    customIdentifier: 'STK-DISP-NZ',
    costInHUF: 135000,
    purchaseDate: '2024-03-01T00:00:00Z',
    manufacturer: { id: 'mfr-nordson', name: 'Nordson' },
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    isStockItem: true,
    stockQuantity: 15,
    minStockThreshold: 4,
    storageLocation: 'Bin 09-C',
    technology: 'Dispensing',
    metadata: { Quantity: '15', Batch: 'LOT-2026-D', Bin: '09-C' }
  },
  {
    id: 'comp-pc-1',
    name: 'Advantech Industrial PC Chassis',
    displayName: 'Line 1 Workstation IPC',
    serialNumber: 'SN-ADV-7721',
    itemType: 'hardware',
    customIdentifier: 'IPC-L1-01',
    costInHUF: 780000,
    purchaseDate: '2022-11-20T00:00:00Z',
    manufacturer: { id: 'mfr-advantech', name: 'Advantech' },
    responsibleTeams: [{ id: 'team-it', name: 'Industrial IT' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'Line 1 OP10 Control Cabinet',
    technology: 'Assembly',
    metadata: { CPU: 'Core i7-11700E', RAM: '32GB DDR4', OS: 'Windows 10 IoT' }
  },
  {
    id: 'comp-pc-2',
    name: 'Beckhoff Industrial Panel PC',
    displayName: 'Quality Cell HMI Terminal',
    serialNumber: 'SN-BECK-3341',
    itemType: 'hardware',
    customIdentifier: 'IPC-L2-02',
    costInHUF: 1250000,
    purchaseDate: '2023-06-18T00:00:00Z',
    manufacturer: { id: 'mfr-beckhoff', name: 'Beckhoff' },
    responsibleTeams: [{ id: 'team-quality', name: 'Quality Automation' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'Line 2 OP30 HMI Panel',
    technology: 'Test',
    metadata: { Display: '21.5-inch Touch', IP: 'IP65 Front', CPU: 'Intel Xeon' }
  },
  {
    id: 'soft-101',
    name: 'Sinumerik CNC Runtime v4.9',
    displayName: 'Siemens Sinumerik CNC Core',
    serialNumber: 'LIC-CNC-4921',
    itemType: 'software',
    customIdentifier: 'LIC-SINU-01',
    costInHUF: 650000,
    purchaseDate: '2023-04-12T00:00:00Z',
    manufacturer: { id: 'mfr-siemens', name: 'Siemens' },
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    isStockItem: false,
    metadata: { Version: '4.9.2', LicenseType: 'Floating', Seats: '5' }
  },
  {
    id: 'soft-301',
    name: 'VisionPro Deep Learning Suite',
    displayName: 'Cognex VisionPro OCR AI',
    serialNumber: 'LIC-VPRO-99',
    itemType: 'software',
    customIdentifier: 'LIC-VPRO-01',
    costInHUF: 1450000,
    purchaseDate: '2024-02-10T00:00:00Z',
    manufacturer: { id: 'mfr-cognex', name: 'Cognex' },
    responsibleTeams: [{ id: 'team-quality', name: 'Quality Automation' }],
    isStockItem: false,
    metadata: { Version: '3.2.0', Module: 'OCR & Defect Detection' }
  },
  {
    id: 'soft-pc-1',
    name: 'Windows 10 IoT Enterprise LTSC',
    displayName: 'Windows 10 IoT Operating System',
    serialNumber: 'LIC-WIN-IOT-11',
    itemType: 'software',
    customIdentifier: 'LIC-WIN-01',
    costInHUF: 120000,
    purchaseDate: '2022-11-20T00:00:00Z',
    manufacturer: { id: 'mfr-microsoft', name: 'Microsoft' },
    responsibleTeams: [{ id: 'team-it', name: 'Industrial IT' }],
    isStockItem: false,
    metadata: { Build: '19044.2965', Architecture: 'x64' }
  },
  {
    id: 'mch-l01-op010',
    name: 'L01-OP010',
    displayName: 'L01-OP010 – Hyper-Conveyor Pallet Infeed 9000',
    serialNumber: 'SN-SYNTH-MCH-01-OP010',
    itemType: 'machine',
    customIdentifier: 'LINE-01-OP010',
    costInHUF: 12500000,
    purchaseDate: '2024-01-15T00:00:00Z',
    manufacturer: { id: 'mfr-bosch', name: 'Bosch Rexroth' },
    responsibleTeams: [{ id: 'team-maint', name: 'Plant Maintenance (Synthetic AI Guild)' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'Line 01 – Cell OP010',
    technology: 'Assembly',
    metadata: { MachineType: 'ConveyorTransfer', CycleTimeTarget: '16s', PinnedObjectHandle: 'L01-OP010' }
  },
  {
    id: 'mch-l01-op030',
    name: 'L01-OP030',
    displayName: 'L01-OP030 – Giga-Gluer 2K Thermal Dispenser Bot',
    serialNumber: 'SN-SYNTH-MCH-01-OP030',
    itemType: 'machine',
    customIdentifier: 'LINE-01-OP030',
    costInHUF: 18900000,
    purchaseDate: '2024-01-20T00:00:00Z',
    manufacturer: { id: 'mfr-nordson', name: 'Nordson' },
    responsibleTeams: [{ id: 'team-smt', name: 'SMT & Microchips (Synthetic AI Guild)' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'Line 01 – Cell OP030',
    technology: 'Dispensing',
    metadata: { MachineType: 'Dispenser', CycleTimeTarget: '16s', PinnedObjectHandle: 'L01-OP030' }
  },
  {
    id: 'mch-l01-op040',
    name: 'L01-OP040',
    displayName: 'L01-OP040 – Auto-Bolt Torquinator 3000',
    serialNumber: 'SN-SYNTH-MCH-01-OP040',
    itemType: 'machine',
    customIdentifier: 'LINE-01-OP040',
    costInHUF: 14200000,
    purchaseDate: '2024-02-01T00:00:00Z',
    manufacturer: { id: 'mfr-atlas', name: 'Atlas Copco' },
    responsibleTeams: [{ id: 'team-robotics', name: 'Robotics & Cybernetics (Synthetic AI Guild)' }],
    isStockItem: false,
    equipmentStatus: 'InMachine',
    storageLocation: 'Line 01 – Cell OP040',
    technology: 'Fastening',
    metadata: { MachineType: 'FasteningStation', CycleTimeTarget: '53s', PinnedObjectHandle: 'L01-OP040' }
  },
  {
    id: 'stock-prof-4040',
    name: 'Bosch Rexroth 40x40 Modular Aluminum Strut Profile',
    displayName: 'Strut Profile 40x40 2000mm',
    serialNumber: null,
    itemType: 'hardware',
    customIdentifier: 'STK-STRUT-4040',
    costInHUF: 75000,
    purchaseDate: '2024-03-01T00:00:00Z',
    manufacturer: { id: 'mfr-bosch', name: 'Bosch Rexroth' },
    responsibleTeams: [{ id: 'team-maint', name: 'Plant Maintenance (Synthetic AI Guild)' }],
    isStockItem: true,
    stockQuantity: 120,
    minStockThreshold: 20,
    storageLocation: 'Warehouse Bay 04 - Strut Racks',
    technology: 'Assembly',
    metadata: { Material: 'AlMgSi0.5', Length: '2000mm', Slot: '10mm' }
  },
  {
    id: 'stock-phx-term',
    name: 'Phoenix Contact Push-In Terminal Blocks UT 2.5',
    displayName: 'DIN-Rail Terminal Blocks 2.5mm²',
    serialNumber: null,
    itemType: 'hardware',
    customIdentifier: 'STK-PHX-UT25',
    costInHUF: 48000,
    purchaseDate: '2024-03-05T00:00:00Z',
    manufacturer: { id: 'mfr-phoenix', name: 'Phoenix Contact' },
    responsibleTeams: [{ id: 'team-controls', name: 'Controls Engineering (Synthetic AI Guild)' }],
    isStockItem: true,
    stockQuantity: 500,
    minStockThreshold: 100,
    storageLocation: 'Warehouse Bin 12-E',
    technology: 'Controls',
    metadata: { VoltageRating: '800V', CurrentRating: '24A', WireGauge: '2.5mm²' }
  },
  {
    id: 'comp-spare-ax5000',
    name: 'Beckhoff AX5000 Dual-Axis Servo Drive (Spare)',
    displayName: 'Spare AX5206 Servo Drive',
    serialNumber: 'SN-SYNTH-AX5-SPARE-01',
    itemType: 'hardware',
    customIdentifier: 'SPARE-AX5-01',
    costInHUF: 1850000,
    purchaseDate: '2024-02-15T00:00:00Z',
    manufacturer: { id: 'mfr-beckhoff', name: 'Beckhoff' },
    responsibleTeams: [{ id: 'team-controls', name: 'Controls Engineering (Synthetic AI Guild)' }],
    isStockItem: false,
    equipmentStatus: 'InStorage',
    storageLocation: 'Warehouse A - Shelf FAKE-B2-01',
    technology: 'Controls',
    metadata: { Channels: '2', RatedCurrent: '6A', Protocol: 'EtherCAT' }
  }
]

// Recursive flattening helper
function flattenTreeNodes(nodes: any[]): any[] {
  const flattened: any[] = []
  for (const node of nodes) {
    if (!node) continue
    
    // Normalize item type
    let normType = 'hardware'
    const rawType = (node.itemType || node.ItemType || node.$type || '').toLowerCase()
    if (rawType.includes('soft')) {
      normType = 'software'
    } else if (rawType.includes('mach')) {
      normType = 'machine'
    }

    const item = {
      id: node.id || node.Id,
      name: node.name || node.Name || '',
      displayName: node.displayName || node.DisplayName,
      serialNumber: node.serialNumber || node.SerialNumber,
      itemType: normType,
      customIdentifier: node.customIdentifier || node.CustomIdentifier,
      costInHUF: node.costInHUF || node.CostInHUF || 0,
      purchaseDate: node.purchaseDate || node.PurchaseDate,
      manufacturer: node.manufacturer || node.Manufacturer,
      responsibleTeams: node.responsibleTeams || node.ResponsibleTeams || [],
      isStockItem: Boolean(node.isStockItem ?? node.IsStockItem ?? false),
      equipmentStatus: node.equipmentStatus ?? node.EquipmentStatus ?? (node.machineId ? 'InMachine' : 'InStorage'),
      storageLocation: node.storageLocation ?? node.StorageLocation ?? 'Warehouse Shelf',
      stockQuantity: node.stockQuantity ?? node.StockQuantity ?? 1,
      minStockThreshold: node.minStockThreshold ?? node.MinStockThreshold ?? 1,
      technology: node.technology ?? node.Technology ?? 'Assembly',
      metadata: node.metadata || node.Metadata || {},
      children: node.children || []
    }
    flattened.push(item)

    if (node.children && Array.isArray(node.children) && node.children.length > 0) {
      flattened.push(...flattenTreeNodes(node.children))
    }
    if (node.inventoryItems && Array.isArray(node.inventoryItems) && node.inventoryItems.length > 0) {
      flattened.push(...flattenTreeNodes(node.inventoryItems))
    }
  }
  return flattened
}

export default defineEventHandler(async (event) => {
  const method = getMethod(event)
  let payload: Record<string, any> = (event as any)._query || (event as any).query || (event as any)._body || (event as any).body || {}

  if (Object.keys(payload).length === 0) {
    if (method === 'GET') {
      try {
        payload = getQuery(event) || {}
      } catch {
        payload = {}
      }
    } else {
      try {
        payload = await readBody(event) || {}
      } catch {
        payload = {}
      }
    }
  }

  const {
    query = '',
    type = 'all',
    classification = 'all',
    tracking = 'all',
    sortBy = 'name',
    sortOrder = 'asc',
    manufacturerId = '',
    teamId = ''
  } = payload

  let allItems: any[] = []
  const cacheKey = 'heimdall:nitro:inventory_all'

  // 1. Try Redis distributed cache first
  try {
    const cached = await getCachedJson<any[]>(cacheKey)
    if (cached && Array.isArray(cached) && cached.length > 0) {
      allItems = cached
    }
  } catch {
    // Graceful fallback to backend fetch
  }

  if (allItems.length === 0) {
    const backendBase = process.env.BACKEND_API_URL || 'http://localhost:5099'
    const forwardHeaders: Record<string, string> = {}
    const cookie = getHeader(event, 'cookie')
    if (cookie) forwardHeaders.cookie = cookie
    const authorization = getHeader(event, 'authorization')
    if (authorization) forwardHeaders.authorization = authorization

    try {
      const raw = await $fetch<any[]>(`${backendBase}/api/v1/inventory`, {
        headers: forwardHeaders
      })
      if (raw && Array.isArray(raw) && raw.length > 0) {
        allItems = flattenTreeNodes(raw)
      }
    } catch {
      // Graceful fallback
    }

  // Also query Client PCs to dynamically ingest live agent telemetry & reported PC hardware
  try {
    const pcs = await $fetch<any[]>(`${backendBase}/api/v1/ClientPc`, {
      headers: forwardHeaders
    })
    if (pcs && Array.isArray(pcs) && pcs.length > 0) {
      for (const pc of pcs) {
        const isOnline = pc.lastSeen ? (Date.now() - new Date(pc.lastSeen).getTime() < 5 * 60 * 1000) : false
        // Add the PC controller itself as an asset if not present
        allItems.push({
          id: pc.id,
          name: pc.hostname || pc.name,
          displayName: pc.displayName || pc.hostname,
          serialNumber: pc.machineIdentifier || pc.macAddress,
          itemType: 'hardware',
          customIdentifier: pc.hostname,
          costInHUF: 850000,
          purchaseDate: pc.lastSeen || new Date().toISOString(),
          manufacturer: { name: 'Advantech / Industrial IPC' },
          responsibleTeams: pc.responsibleTeams || [],
          isStockItem: false,
          equipmentStatus: 'InMachine',
          storageLocation: 'Production Line Control Cabinet',
          stockQuantity: 1,
          minStockThreshold: 1,
          technology: 'Assembly',
          telemetry: {
            cpuUsagePercent: pc.resourceAverages?.cpuUsageAverage ?? (isOnline ? 18.5 : 0),
            ramUsagePercent: pc.resourceAverages?.ramUsageAverage ?? (isOnline ? 42.1 : 0),
            freeDiskSpace: pc.freeDiskSpace,
            isOnline,
            lastSeen: pc.lastSeen
          },
          metadata: {
            MAC: pc.macAddress,
            IP: pc.ipAddress || (pc.systemMetadata as any)?.IPAddress,
            Host: pc.hostname,
            ...(pc.systemMetadata || {})
          }
        })

        // Add each reported hardware component (CPU, RAM, Disk, etc.)
        if (pc.inventoryItems && Array.isArray(pc.inventoryItems)) {
          for (const hw of pc.inventoryItems) {
            allItems.push({
              id: hw.id || `${pc.id}-${hw.name}`,
              name: `${pc.hostname} - ${hw.name}`,
              displayName: hw.displayName || hw.name,
              serialNumber: hw.serialNumber || `${pc.macAddress}-${hw.name}`,
              itemType: 'hardware',
              customIdentifier: `${pc.hostname}/${hw.name}`,
              costInHUF: 150000,
              purchaseDate: pc.lastSeen || new Date().toISOString(),
              manufacturer: { name: hw.manufacturer?.name || 'OEM' },
              responsibleTeams: pc.responsibleTeams || [],
              isStockItem: false,
              equipmentStatus: 'InMachine',
              storageLocation: `${pc.hostname} Internal Chassis`,
              stockQuantity: 1,
              minStockThreshold: 1,
              technology: 'Assembly',
              telemetry: {
                isOnline,
                host: pc.hostname
              },
              metadata: {
                HostPC: pc.hostname,
                Type: hw.type || hw.itemType,
                ...(hw.metadata || {})
              }
            })
          }
        }
      }
    }
  } catch {
    // Ignore if ClientPc endpoint not available
  }

    if (allItems.length === 0) {
      allItems = [...SEED_INVENTORY]
    }
  }

  // Deduplicate items by ID and guarantee normalization
  const uniqueItemsMap = new Map<string, any>()
  for (const item of allItems) {
    if (item.id && !uniqueItemsMap.has(item.id)) {
      item.isStockItem = Boolean(item.isStockItem)
      uniqueItemsMap.set(item.id, item)
    }
  }
  let items = Array.from(uniqueItemsMap.values())

  // Save to Redis cache for fast subsequent requests (avoiding DB hits)
  try {
    await setCachedJson(cacheKey, items, 60)
  } catch {
    // Ignore cache set failure
  }

  // Compute global inventory totals before filtering
  const totalGlobalCount = items.length
  const totalGlobalHardware = items.filter(i => i.itemType === 'hardware').length
  const totalGlobalSoftware = items.filter(i => i.itemType === 'software').length
  const totalGlobalParts = items.filter(i => !i.isStockItem && i.itemType !== 'software').length
  const totalGlobalStock = items.filter(i => i.isStockItem === true).length
  const totalGlobalCost = items.reduce((sum, item) => sum + (item.costInHUF || 0), 0)

  // Resolve classification (Hardware vs Software vs All) and tracking (Serialized vs Bulk Stock vs All)
  let effectiveClass = (classification || 'all').toLowerCase()
  let effectiveTracking = (tracking || 'all').toLowerCase()

  // Backwards compatibility if legacy 'type' parameter is passed without classification/tracking
  if (type && type !== 'all' && type !== 'hierarchy') {
    const t = type.toLowerCase()
    if (t === 'hardware' || t === 'software') {
      if (effectiveClass === 'all') effectiveClass = t
    } else if (t === 'parts' || t === 'serialized') {
      if (effectiveTracking === 'all') effectiveTracking = 'serialized'
    } else if (t === 'stock') {
      if (effectiveTracking === 'all') effectiveTracking = 'stock'
    }
  }

  // Filter by Classification (Nature)
  if (effectiveClass !== 'all') {
    items = items.filter(item => {
      const itemType = (item.itemType || '').toLowerCase()
      return itemType === effectiveClass
    })
  }

  // Filter by Tracking (Management Mode)
  if (effectiveTracking !== 'all') {
    items = items.filter(item => {
      if (effectiveTracking === 'serialized' || effectiveTracking === 'parts') {
        return item.isStockItem !== true
      }
      if (effectiveTracking === 'stock') {
        return item.isStockItem === true
      }
      return true
    })
  }

  // Filter by Query (OmniSearch syntax + free text)
  if (query) {
    const q = query.toLowerCase().trim()
    
    // Parse key:value pairs if present in query
    const tagMatches: Record<string, string> = {}
    const tagRegex = /(\w+):"([^"]+)"|(\w+):(\S+)/g
    let match
    while ((match = tagRegex.exec(q)) !== null) {
      const key = match[1] || match[3]
      const val = match[2] || match[4]
      tagMatches[key.toLowerCase()] = val.toLowerCase()
    }
    const cleanText = q.replace(tagRegex, '').trim()

    items = items.filter(item => {
      // Check explicit tag matches
      for (const [tagKey, tagVal] of Object.entries(tagMatches)) {
        if (tagKey === 'type' || tagKey === 'class') {
          if (tagVal === 'hardware' && item.itemType?.toLowerCase() !== 'hardware') return false
          if (tagVal === 'software' && item.itemType?.toLowerCase() !== 'software') return false
          if ((tagVal === 'parts' || tagVal === 'serialized') && item.isStockItem) return false
          if (tagVal === 'stock' && !item.isStockItem) return false
        }
        if (tagKey === 'tracking') {
          if ((tagVal === 'parts' || tagVal === 'serialized') && item.isStockItem) return false
          if (tagVal === 'stock' && !item.isStockItem) return false
        }
        if ((tagKey === 'manufacturer' || tagKey === 'mfr') && !item.manufacturer?.name?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'station' && !item.metadata?.Station?.toLowerCase().includes(tagVal) && !item.customIdentifier?.toLowerCase().includes(tagVal) && !item.storageLocation?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'tech' && !item.technology?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'status' && !item.equipmentStatus?.toLowerCase().includes(tagVal) && !item.telemetry?.isOnline?.toString().includes(tagVal)) return false
        if (tagKey === 'serial' && !item.serialNumber?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'location' && !item.storageLocation?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'isstock' && String(item.isStockItem).toLowerCase() !== tagVal) return false
        if (tagKey === 'cost') {
          if (tagVal.startsWith('>')) {
            const min = parseFloat(tagVal.slice(1))
            if ((item.costInHUF || 0) <= min) return false
          } else if (tagVal.startsWith('<')) {
            const max = parseFloat(tagVal.slice(1))
            if ((item.costInHUF || 0) >= max) return false
          }
        }
      }

      // Check free text matching across all fields
      if (cleanText) {
        const nameMatch = (item.name || '').toLowerCase().includes(cleanText)
        const dispMatch = (item.displayName || '').toLowerCase().includes(cleanText)
        const serialMatch = (item.serialNumber || '').toLowerCase().includes(cleanText)
        const mfrMatch = (item.manufacturer?.name || '').toLowerCase().includes(cleanText)
        const identMatch = (item.customIdentifier || '').toLowerCase().includes(cleanText)
        const metaMatch = Object.values(item.metadata || {}).some((v: any) => String(v).toLowerCase().includes(cleanText))
        return nameMatch || dispMatch || serialMatch || mfrMatch || identMatch || metaMatch
      }

      return true
    })
  }

  if (manufacturerId) {
    items = items.filter(item => item.manufacturer?.id === manufacturerId || item.manufacturer?.name?.toLowerCase() === manufacturerId.toLowerCase())
  }

  if (teamId) {
    items = items.filter(item => item.responsibleTeams?.some((t: any) => t.id === teamId || t.name?.toLowerCase() === teamId.toLowerCase()))
  }

  // Sorting
  items.sort((a: any, b: any) => {
    let valA = a[sortBy] ?? a.metadata?.[sortBy] ?? ''
    let valB = b[sortBy] ?? b.metadata?.[sortBy] ?? ''

    if (typeof valA === 'string') valA = valA.toLowerCase()
    if (typeof valB === 'string') valB = valB.toLowerCase()

    if (valA < valB) return sortOrder === 'asc' ? -1 : 1
    if (valA > valB) return sortOrder === 'asc' ? 1 : -1
    return 0
  })

  const totalCostHuf = items.reduce((sum, item) => sum + (item.costInHUF || 0), 0)
  const totalCount = items.length

  let pageNum = parseInt(payload.page, 10)
  let pageSizeNum = parseInt(payload.pageSize, 10)

  let pagedItems = items
  if (pageSizeNum > 0) {
    if (!pageNum || pageNum < 1) pageNum = 1
    const offset = (pageNum - 1) * pageSizeNum
    pagedItems = items.slice(offset, offset + pageSizeNum)
  }

  return {
    items: pagedItems,
    totalCount,
    totalPages: pageSizeNum > 0 ? Math.ceil(totalCount / pageSizeNum) : 1,
    page: pageNum || 1,
    pageSize: pageSizeNum || totalCount,
    totalCostHuf,
    kpis: {
      totalGlobalCount,
      totalGlobalHardware,
      totalGlobalSoftware,
      totalGlobalParts,
      totalGlobalStock,
      totalGlobalCost
    }
  }
})
