import { defineEventHandler, getQuery } from 'h3'

// Mock base assets for tree aggregation offloading
const mockMachines = [
  {
    id: 'mach-op10',
    name: 'OP10 Machining Cell',
    hostname: 'OP10-CELL-01',
    customIdentifier: 'STATION-OP10-01',
    displayName: 'Main CNC Milling Station',
    organizationId: 'Org-Alpha',
    responsibleTeams: [{ id: 'team-mech', name: 'Mechanical Maintenance' }],
    controllers: [
      { id: 'ctrl-101', name: 'Siemens S7-1500 PLC', hostname: 'plc-op10.local' }
    ],
    children: [
      {
        id: 'comp-101',
        name: 'Spindle Motor Assembly',
        serialNumber: 'SN-SPINDLE-994',
        itemType: 'hardware',
        costInHUF: 1850000,
        purchaseDate: '2023-04-12T00:00:00Z',
        manufacturer: { name: 'Siemens Industrial' },
        metadata: { Power: '15kW', MaxRPM: '12000' }
      },
      {
        id: 'comp-102',
        name: 'Coolant Flow Sensor Array',
        serialNumber: 'SN-SENSOR-441',
        itemType: 'hardware',
        costInHUF: 320000,
        purchaseDate: '2024-01-15T00:00:00Z',
        manufacturer: { name: 'IFM Electronic' },
        metadata: { PressureRange: '0-10Bar', Protocol: 'IO-Link' }
      }
    ],
    inventoryItems: [
      {
        id: 'soft-101',
        name: 'Sinumerik CNC Runtime v4.9',
        serialNumber: 'LIC-CNC-4921',
        itemType: 'software',
        costInHUF: 650000,
        version: '4.9.2',
        metadata: { LicenseType: 'Floating', Seats: '5' }
      }
    ]
  },
  {
    id: 'mach-op20',
    name: 'OP20 Robotic Welding Station',
    hostname: 'OP20-WELD-02',
    customIdentifier: 'STATION-OP20-02',
    displayName: 'KUKA Robotic Cell',
    organizationId: 'Org-Alpha',
    responsibleTeams: [{ id: 'team-elec', name: 'Electrical Engineering' }],
    controllers: [
      { id: 'ctrl-201', name: 'KUKA KRC4 Controller', hostname: 'kuka-op20.local' }
    ],
    children: [
      {
        id: 'comp-201',
        name: 'Servo Driver Module 30A',
        serialNumber: 'SN-SERVO-881',
        itemType: 'hardware',
        costInHUF: 940000,
        purchaseDate: '2023-09-01T00:00:00Z',
        manufacturer: { name: 'KUKA' },
        metadata: { Voltage: '400V', Current: '30A' }
      }
    ],
    inventoryItems: []
  },
  {
    id: 'mach-op30',
    name: 'OP30 Automated Quality Inspector',
    hostname: 'OP30-INSPECT-03',
    customIdentifier: 'STATION-OP30-03',
    displayName: 'Cognex Vision Inspection',
    organizationId: 'Org-Beta',
    responsibleTeams: [{ id: 'team-quality', name: 'Quality Automation' }],
    controllers: [],
    children: [
      {
        id: 'comp-301',
        name: 'Cognex In-Sight 9000 Camera',
        serialNumber: 'SN-CAM-9081',
        itemType: 'hardware',
        costInHUF: 2100000,
        purchaseDate: '2024-02-10T00:00:00Z',
        manufacturer: { name: 'Cognex' },
        metadata: { Resolution: '12MP', FPS: '60' }
      }
    ],
    inventoryItems: [
      {
        id: 'soft-301',
        name: 'VisionPro Deep Learning Suite',
        serialNumber: 'LIC-VPRO-99',
        itemType: 'software',
        costInHUF: 1450000,
        version: '3.2.0',
        metadata: { Module: 'OCR & Defect Detection' }
      }
    ]
  }
]

const mockClients = [
  {
    id: 'pc-workstation-01',
    name: 'IPC-AssemblyLine-Line1',
    hostname: 'ipc-line1.factory.local',
    customIdentifier: 'IPC-L1-01',
    displayName: 'Line 1 Master Workstation',
    organizationId: 'Org-Alpha',
    lastOnline: new Date(Date.now() - 2 * 60 * 1000).toISOString(),
    responsibleTeams: [{ id: 'team-it', name: 'Industrial IT' }],
    controlledMachines: [
      { id: 'mach-op10', customIdentifier: 'STATION-OP10-01', name: 'OP10 Machining Cell' },
      { id: 'mach-op20', customIdentifier: 'STATION-OP20-02', name: 'OP20 Robotic Welding Station' }
    ],
    children: [
      {
        id: 'comp-pc-1',
        name: 'Advantech Industrial PC Chassis',
        serialNumber: 'SN-ADV-7721',
        itemType: 'hardware',
        costInHUF: 780000,
        purchaseDate: '2022-11-20T00:00:00Z',
        manufacturer: { name: 'Advantech' },
        metadata: { CPU: 'Core i7-11700E', RAM: '32GB DDR4' }
      }
    ],
    inventoryItems: [
      {
        id: 'soft-pc-1',
        name: 'Windows 10 IoT Enterprise LTSC',
        serialNumber: 'LIC-WIN-IOT-11',
        itemType: 'software',
        costInHUF: 120000,
        version: '21H2',
        metadata: { Build: '19044.2965' }
      }
    ]
  },
  {
    id: 'pc-workstation-02',
    name: 'IPC-InspectionLine-Line2',
    hostname: 'ipc-line2.factory.local',
    customIdentifier: 'IPC-L2-02',
    displayName: 'Quality Inspection HMI Node',
    organizationId: 'Org-Beta',
    lastOnline: new Date(Date.now() - 45 * 60 * 1000).toISOString(),
    responsibleTeams: [{ id: 'team-quality', name: 'Quality Automation' }],
    controlledMachines: [
      { id: 'mach-op30', customIdentifier: 'STATION-OP30-03', name: 'OP30 Automated Quality Inspector' }
    ],
    children: [
      {
        id: 'comp-pc-2',
        name: 'Beckhoff Industrial Panel PC',
        serialNumber: 'SN-BECK-3341',
        itemType: 'hardware',
        costInHUF: 1250000,
        purchaseDate: '2023-06-18T00:00:00Z',
        manufacturer: { name: 'Beckhoff' },
        metadata: { Display: '21.5-inch Touch', IP: 'IP65 Front' }
      }
    ],
    inventoryItems: []
  }
]

// Recursive node calculation helper for tree aggregation
function computeNodeMetrics(node: any) {
  let hardwareCount = 0
  let softwareCount = 0
  let costHuf = 0

  const allItems = [...(node.children || []), ...(node.inventoryItems || [])]

  for (const item of allItems) {
    if (item.itemType === 'hardware') hardwareCount++
    if (item.itemType === 'software') softwareCount++
    if (typeof item.costInHUF === 'number') costHuf += item.costInHUF
  }

  return {
    ...node,
    aggregatedMetrics: {
      hardwareCount,
      softwareCount,
      totalCostHuf,
      totalChildItems: allItems.length
    }
  }
}

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const primaryKey = (query.primaryKey as string) === 'client' ? 'client' : 'machine'
  const searchQuery = (query.query as string || '').toLowerCase().trim()
  const responsibility = (query.responsibility as string || 'all').trim()
  const sortBy = (query.sortBy as string || 'name').trim()
  const sortOrder = (query.sortOrder as string || 'asc').trim()

  const rawList = primaryKey === 'client' ? mockClients : mockMachines

  let filtered = rawList.map(computeNodeMetrics)

  // Search filtering
  if (searchQuery) {
    filtered = filtered.filter(item => {
      const name = (item.name || item.hostname || item.customIdentifier || '').toLowerCase()
      const disp = (item.displayName || '').toLowerCase()
      const org = (item.organizationId || '').toLowerCase()
      return name.includes(searchQuery) || disp.includes(searchQuery) || org.includes(searchQuery) || item.id.toLowerCase().includes(searchQuery)
    })
  }

  // Team responsibility filtering
  if (responsibility !== 'all') {
    filtered = filtered.filter(item =>
      item.responsibleTeams?.some((t: any) => t.id === responsibility)
    )
  }

  // Sorting
  filtered.sort((a, b) => {
    let valA = a[sortBy] ?? a.aggregatedMetrics[sortBy] ?? a.name ?? ''
    let valB = b[sortBy] ?? b.aggregatedMetrics[sortBy] ?? b.name ?? ''

    if (typeof valA === 'string') valA = valA.toLowerCase()
    if (typeof valB === 'string') valB = valB.toLowerCase()

    if (valA < valB) return sortOrder === 'asc' ? -1 : 1
    if (valA > valB) return sortOrder === 'asc' ? 1 : -1
    return 0
  })

  // Offloaded tree aggregations summary
  const totalNodes = filtered.length
  const totalCost = filtered.reduce((acc, curr) => acc + curr.aggregatedMetrics.totalCostHuf, 0)
  const totalHardware = filtered.reduce((acc, curr) => acc + curr.aggregatedMetrics.hardwareCount, 0)
  const totalSoftware = filtered.reduce((acc, curr) => acc + curr.aggregatedMetrics.softwareCount, 0)

  return {
    primaryKey,
    tree: filtered,
    summary: {
      totalNodes,
      totalCost,
      totalHardware,
      totalSoftware
    }
  }
})
