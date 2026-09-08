import { defineEventHandler, readBody, getQuery, getMethod, getHeader } from 'h3'

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
    metadata: { Resolution: '12MP', FPS: '60', Interface: 'GigE', Station: 'OP30' }
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
    metadata: { Build: '19044.2965', Architecture: 'x64' }
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
  let payload: Record<string, any> = {}

  if (method === 'GET') {
    payload = getQuery(event) || {}
  } else {
    try {
      payload = await readBody(event) || {}
    } catch {
      payload = {}
    }
  }

  const {
    query = '',
    type = 'all',
    sortBy = 'name',
    sortOrder = 'asc',
    manufacturerId = '',
    teamId = ''
  } = payload

  let allItems: any[] = []

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

  // Deduplicate items by ID
  const uniqueItemsMap = new Map<string, any>()
  for (const item of allItems) {
    if (item.id && !uniqueItemsMap.has(item.id)) {
      uniqueItemsMap.set(item.id, item)
    }
  }
  let items = Array.from(uniqueItemsMap.values())

  // Compute global inventory totals before type filtering
  const totalGlobalCount = items.length
  const totalGlobalHardware = items.filter(i => i.itemType === 'hardware').length
  const totalGlobalSoftware = items.filter(i => i.itemType === 'software').length
  const totalGlobalCost = items.reduce((sum, item) => sum + (item.costInHUF || 0), 0)

  // Filter by Type
  if (type && type !== 'all' && type !== 'hierarchy') {
    items = items.filter(item => {
      const itemType = (item.itemType || '').toLowerCase()
      return itemType === type.toLowerCase()
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
        if (tagKey === 'type' && !item.itemType?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'manufacturer' && !item.manufacturer?.name?.toLowerCase().includes(tagVal)) return false
        if (tagKey === 'station' && !item.metadata?.Station?.toLowerCase().includes(tagVal) && !item.customIdentifier?.toLowerCase().includes(tagVal)) return false
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
      totalGlobalCost
    }
  }
})
