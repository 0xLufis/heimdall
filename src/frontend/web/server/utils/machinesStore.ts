import { randomUUID } from 'node:crypto'
import { partsInventoryStore } from './partsInventoryStore'

export type MachineOperationalStatus = 'active' | 'inactive' | 'maintenance' | 'decommissioned' | 'dismantled'

export interface MachineEntity {
  id: string
  name: string
  displayName?: string
  customIdentifier: string
  machineType?: string
  groupId?: string
  status: MachineOperationalStatus
  decommissionReason?: string
  decommissionDate?: string
  decommissionDisposition?: 'salvage_donor' | 'scrap' | 'retain'
  controllers: Array<{ id: string; hostname: string; ipAddress?: string; isOnline?: boolean }>
  responsibleTeams?: Array<{ name: string }>
  preferredTechnicianId?: string
  preferredTechnicianName?: string
  pinnedObjectHandle?: string
  technologies?: string[]
  lineGroupId?: string
  subLineGroupId?: string
  projectGroupId?: string
  businessUnitGroupId?: string
  // SPOC & Owner Governance
  spoc?: string
  spocUserId?: string
  owner?: string
  ownerUserId?: string
  // Sub-Machine Architecture
  parentMachineId?: string
  subMachineIndex?: number
  cellType?: 'Standard' | 'MultiMachineCell' | 'Roundtable' | string
  isSubMachine?: boolean
  isCellOrRoundtable?: boolean
  subMachines?: Array<{
    id: string
    name: string
    displayName?: string
    customIdentifier: string
    subMachineIndex?: number
    cellType?: string
    status?: string
  }>
  systemTags?: string[]
  userTags?: string[]
  components?: Array<{
    id: string
    name: string
    functionalSlotIdentifier: string
    category: string
    componentRecipeCode?: string
    currentPartId?: string
    tags: string[]
  }>
}

const SEED_MACHINES: MachineEntity[] = [
  {
    id: '10101010-1010-1010-1010-101010101010',
    name: 'OP10-CNC',
    displayName: 'OP-10 CNC Machining Cell',
    customIdentifier: 'STATION-OP10-01',
    machineType: 'Milling',
    groupId: 'Line 06 — Module Assembly (AUDI)',
    status: 'active',
    spoc: 'István Kovács',
    spocUserId: 'usr-kovacs',
    owner: 'CNC Machining Operations BU',
    ownerUserId: 'usr-bu-lead',
    businessUnitGroupId: 'grp-bu-emobility',
    projectGroupId: 'grp-prj-audi',
    lineGroupId: 'grp-line06',
    subLineGroupId: 'grp-subline-dispense',
    controllers: [{ id: 'c-op10', hostname: 'IPC-EDGE-CONTROLLER', ipAddress: '192.168.1.101', isOnline: true }],
    responsibleTeams: [{ name: 'Mechanical Maintenance' }]
  },
  {
    id: '20202020-2020-2020-2020-202020202020',
    name: 'RT-CELL-01',
    displayName: 'Automated 4-Station Rotary Dial Table Cell',
    customIdentifier: 'LINE1-RT01',
    machineType: 'Roundtable',
    groupId: 'Line 06 — Module Assembly (AUDI)',
    status: 'active',
    spoc: 'Zoltán Németh',
    spocUserId: 'usr-nemeth',
    owner: 'E-Mobility Systems BU',
    businessUnitGroupId: 'grp-bu-emobility',
    projectGroupId: 'grp-prj-audi',
    lineGroupId: 'grp-line06',
    subLineGroupId: 'grp-subline-dispense',
    cellType: 'Roundtable',
    isCellOrRoundtable: true,
    controllers: [{ id: 'c-rt-master', hostname: 'IPC-RT01-MASTER', ipAddress: '192.168.1.180', isOnline: true }],
    responsibleTeams: [{ name: 'Assembly Automation' }],
    subMachines: [
      {
        id: '20202020-2020-2020-2020-202020200001',
        name: 'RT-Station-1-Infeed',
        displayName: 'Rotary Dial Station 1 (Part Infeed & Orientation)',
        customIdentifier: 'LINE1-RT01-S1',
        subMachineIndex: 1,
        cellType: 'Roundtable',
        status: 'active'
      },
      {
        id: '20202020-2020-2020-2020-202020200002',
        name: 'RT-Station-2-Dispense',
        displayName: 'Rotary Dial Station 2 (Gap Filler Dispense)',
        customIdentifier: 'LINE1-RT01-S2',
        subMachineIndex: 2,
        cellType: 'Roundtable',
        status: 'active'
      },
      {
        id: '20202020-2020-2020-2020-202020200003',
        name: 'RT-Station-3-Fasten',
        displayName: 'Rotary Dial Station 3 (Multi-Spindle Fastener)',
        customIdentifier: 'LINE1-RT01-S3',
        subMachineIndex: 3,
        cellType: 'Roundtable',
        status: 'active'
      },
      {
        id: '20202020-2020-2020-2020-202020200004',
        name: 'RT-Station-4-Outfeed',
        displayName: 'Rotary Dial Station 4 (Vision QC & Unload Robot)',
        customIdentifier: 'LINE1-RT01-S4',
        subMachineIndex: 4,
        cellType: 'Roundtable',
        status: 'active'
      }
    ]
  },
  {
    id: '20202020-2020-2020-2020-202020200001',
    name: 'RT-Station-1-Infeed',
    displayName: 'Rotary Dial Station 1 (Part Infeed & Orientation)',
    customIdentifier: 'LINE1-RT01-S1',
    machineType: 'Assembly',
    groupId: 'Line 06 — Module Assembly (AUDI)',
    status: 'active',
    spoc: 'Zoltán Németh',
    owner: 'E-Mobility Systems BU',
    businessUnitGroupId: 'grp-bu-emobility',
    projectGroupId: 'grp-prj-audi',
    lineGroupId: 'grp-line06',
    subLineGroupId: 'grp-subline-dispense',
    parentMachineId: '20202020-2020-2020-2020-202020202020',
    subMachineIndex: 1,
    cellType: 'Roundtable',
    isSubMachine: true,
    controllers: [{ id: 'c-rt-s1', hostname: 'IPC-RT01-S1', ipAddress: '192.168.1.181', isOnline: true }]
  },
  {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'OP10-Load',
    displayName: 'Automatic Raw Material Infeed',
    customIdentifier: 'LINE1-OP10',
    machineType: 'Assembly',
    groupId: 'Line 1',
    status: 'active',
    controllers: [{ id: 'c-1', hostname: 'IPC-L1-01', ipAddress: '192.168.1.110', isOnline: true }],
    responsibleTeams: [{ name: 'Mechanical Maintenance' }]
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    name: 'OP20-Weld',
    displayName: 'Laser Robotic Welding Cell',
    customIdentifier: 'LINE1-OP20',
    machineType: 'Welding',
    groupId: 'Line 1',
    status: 'active',
    controllers: [{ id: 'c-2', hostname: 'IPC-L1-02', ipAddress: '192.168.1.111', isOnline: true }],
    responsibleTeams: [{ name: 'Electrical Engineering' }]
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    name: 'OP30-Vision',
    displayName: 'Cognex AI Inspection Chamber',
    customIdentifier: 'LINE2-OP30',
    machineType: 'Test',
    groupId: 'Line 2',
    status: 'maintenance',
    controllers: [{ id: 'c-3', hostname: 'IPC-L2-03', ipAddress: '192.168.1.120', isOnline: true }],
    responsibleTeams: [{ name: 'Quality Automation' }]
  },
  {
    id: '44444444-4444-4444-4444-444444444444',
    name: 'OP40-Fasten',
    displayName: 'Multi-Spindle Screw Fastening Station',
    customIdentifier: 'LINE1-OP40',
    machineType: 'Fastening',
    groupId: 'Line 1',
    status: 'active',
    controllers: [{ id: 'c-4', hostname: 'IPC-L1-04', ipAddress: '192.168.1.114', isOnline: true }],
    responsibleTeams: [{ name: 'Assembly Engineering' }]
  },
  {
    id: '55555555-5555-5555-5555-555555555555',
    name: 'OP50-Dispense',
    displayName: 'Precision Adhesive Gasket Dispenser',
    customIdentifier: 'LINE2-OP50',
    machineType: 'Dispensing',
    groupId: 'Line 2',
    status: 'inactive',
    controllers: [{ id: 'c-5', hostname: 'IPC-L2-05', ipAddress: '192.168.1.125', isOnline: true }],
    responsibleTeams: [{ name: 'Chemical & Dispensing' }]
  },
  {
    id: '66666666-6666-6666-6666-666666666666',
    name: 'OP60-Robot',
    displayName: '6-Axis KUKA Palletizing Robot',
    customIdentifier: 'LINE2-OP60',
    machineType: 'Robotics',
    groupId: 'Line 2',
    status: 'active',
    controllers: [{ id: 'c-6', hostname: 'IPC-L2-06', ipAddress: '192.168.1.128', isOnline: true }],
    responsibleTeams: [{ name: 'Robotics Engineering' }]
  },
  {
    id: '77777777-7777-7777-7777-777777777777',
    name: 'OP70-LegacyPress',
    displayName: 'Legacy Hydraulic Stamping Press #3',
    customIdentifier: 'LINE1-OP70',
    machineType: 'Pressing',
    groupId: 'Line 1',
    status: 'decommissioned',
    decommissionReason: 'Replaced by High-Speed Servo Stamping Cell',
    decommissionDate: '2026-08-15T10:00:00.000Z',
    decommissionDisposition: 'retain',
    controllers: [],
    responsibleTeams: [{ name: 'Mechanical Maintenance' }]
  }
]

class MachinesStore {
  private machines = new Map<string, MachineEntity>()

  constructor() {
    this.reset()
  }

  public reset() {
    this.machines.clear()
    for (const m of SEED_MACHINES) {
      this.machines.set(m.id, { ...m })
    }
  }

  public getAllMachines(options: {
    status?: string
    includeDecommissioned?: boolean
    groupId?: string
  } = {}): MachineEntity[] {
    let list = Array.from(this.machines.values())

    if (options.status && options.status !== 'all') {
      list = list.filter(m => m.status === options.status)
    } else if (!options.includeDecommissioned) {
      // By default exclude decommissioned and dismantled machines from primary production view
      list = list.filter(m => m.status !== 'decommissioned' && m.status !== 'dismantled')
    }

    if (options.groupId && options.groupId !== 'all') {
      list = list.filter(m => m.groupId === options.groupId)
    }

    return list
  }

  public getMachineById(id: string): MachineEntity | undefined {
    return this.machines.get(id) || Array.from(this.machines.values()).find(m => m.customIdentifier === id)
  }

  public getSubMachines(id: string): MachineEntity[] {
    const parent = this.getMachineById(id)
    if (!parent) return []
    return Array.from(this.machines.values()).filter(m => m.parentMachineId === parent.id || m.parentMachineId === parent.customIdentifier)
  }

  public updateMachineStatus(
    id: string,
    status: MachineOperationalStatus,
    actor?: { id: string; name: string }
  ): MachineEntity {
    const machine = this.getMachineById(id)
    if (!machine) {
      throw new Error(`Machine with ID "${id}" was not found.`)
    }

    machine.status = status
    this.machines.set(machine.id, machine)
    return machine
  }

  public decommissionMachine(
    id: string,
    payload: {
      reason: string
      targetDisposition: 'decommissioned' | 'dismantled'
      partsHandling: 'salvage_donor' | 'scrap' | 'retain'
      decommissionDate?: string
    },
    actor: { id: string; name: string }
  ): {
    machine: MachineEntity
    salvagedPartsCount: number
    scrappedPartsCount: number
  } {
    const machine = this.getMachineById(id)
    if (!machine) {
      throw new Error(`Machine with ID "${id}" was not found.`)
    }

    if (!payload.reason || !payload.reason.trim()) {
      throw new Error('Mandatory justification reason required to decommission a production machine.')
    }

    machine.status = payload.targetDisposition === 'dismantled' ? 'dismantled' : 'decommissioned'
    machine.decommissionReason = payload.reason.trim()
    machine.decommissionDate = payload.decommissionDate || new Date().toISOString()
    machine.decommissionDisposition = payload.partsHandling

    this.machines.set(machine.id, machine)

    // Handle installed parts in warehouse inventory
    let salvagedPartsCount = 0
    let scrappedPartsCount = 0

    const { parts } = partsInventoryStore.getAllParts()
    const linkedParts = parts.filter(p => p.linkedMachineId === machine.id || p.linkedMachineId === machine.customIdentifier)

    for (const part of linkedParts) {
      if (payload.partsHandling === 'salvage_donor') {
        partsInventoryStore.transitionLifecycle(
          part.id,
          'donor',
          {
            notes: `Salvaged as donor part from decommissioned machine ${machine.name} (${machine.customIdentifier}): ${payload.reason}`
          },
          actor
        )
        salvagedPartsCount++
      } else if (payload.partsHandling === 'scrap') {
        partsInventoryStore.transitionLifecycle(
          part.id,
          'scrap',
          {
            notes: `Scrapped during decommissioning of machine ${machine.name} (${machine.customIdentifier}): ${payload.reason}`
          },
          actor
        )
        scrappedPartsCount++
      }
    }

    return {
      machine,
      salvagedPartsCount,
      scrappedPartsCount
    }
  }
}

export const machinesStore = new MachinesStore()
