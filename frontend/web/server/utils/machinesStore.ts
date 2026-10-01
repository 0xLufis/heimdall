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
}

const SEED_MACHINES: MachineEntity[] = [
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
