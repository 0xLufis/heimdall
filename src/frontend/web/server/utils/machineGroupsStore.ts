import { randomUUID } from 'node:crypto'
import { getPlantMachineGroups } from './datasetLoader'
import { featureFlags } from './featureFlags'

export type MachineGroupType =
  | 'BusinessUnit'
  | 'Project'
  | 'Line'
  | 'SubLine'
  | 'Machine'
  | 'Technological'
  | 'Customer'
  | 'Manual'

export interface MachineGroupEntry {
  id: string
  name: string
  description?: string
  parentId?: string | null
  machineIds: string[]
  machineTypes?: string[]
  color?: string
  icon?: string
  leadEngineerId?: string
  leadEngineerName?: string
  groupType?: MachineGroupType
  interfaceContract?: string
  technologyKey?: string
}

const FALLBACK_GROUPS: MachineGroupEntry[] = [
  // 1. Business Unit level
  {
    id: 'grp-bu-emobility',
    name: 'E-Mobility Systems Business Unit',
    description: 'High-voltage traction battery packs and e-drive assembly unit',
    parentId: null,
    machineIds: [],
    machineTypes: [],
    groupType: 'BusinessUnit',
    color: 'blue'
  },
  // 2. Project level
  {
    id: 'grp-prj-audi',
    name: 'PPE Battery Architecture Project',
    description: 'AUDI Premium Platform Electric contract tooling and assembly',
    parentId: 'grp-bu-emobility',
    machineIds: [],
    machineTypes: [],
    groupType: 'Project',
    color: 'sky'
  },
  // 3. Line level
  {
    id: 'grp-line06',
    name: 'Line 06 — Module Assembly (AUDI)',
    description: 'Automated battery module line',
    parentId: 'grp-prj-audi',
    machineIds: ['STATION-OP10-01', 'STATION-SC-L06-03', 'STATION-AOI-L06-02', 'ROUNDTABLE-CELL-01'],
    machineTypes: ['Automatic Optical Inspection', 'Gap Filler', 'Screwing Station', 'Pressing', 'Welding'],
    groupType: 'Line',
    color: 'indigo',
    leadEngineerId: 'usr-orwell',
    leadEngineerName: 'Engineer Orwell'
  },
  // 4. Sub-Line levels
  {
    id: 'grp-subline-dispense',
    name: 'Sub-Line A — Dispensing & Fastening',
    description: 'Thermal interface dispensing and automatic screw fastening sub-line',
    parentId: 'grp-line06',
    machineIds: ['STATION-SC-L06-03', 'ROUNDTABLE-CELL-01'],
    machineTypes: ['Gap Filler', 'Screwing Station'],
    groupType: 'SubLine',
    color: 'purple',
    interfaceContract: 'SubLineInterface'
  },
  {
    id: 'grp-subline-welding',
    name: 'Sub-Line B — Laser & Busbar Welding',
    description: 'Optical inspection and laser busbar joining sub-line',
    parentId: 'grp-line06',
    machineIds: ['STATION-AOI-L06-02', '22222222-2222-2222-2222-222222222222'],
    machineTypes: ['Welding', 'Automatic Optical Inspection'],
    groupType: 'SubLine',
    color: 'amber',
    interfaceContract: 'SubLineInterface',
    technologyKey: 'Laser'
  },
  // Backward compatibility legacy entries
  {
    id: 'grp-plant',
    name: 'Battery Assembly Plant 01',
    description: 'Main production facility',
    parentId: 'grp-bu-emobility',
    machineIds: [],
    machineTypes: [],
    groupType: 'BusinessUnit',
    color: 'blue'
  },
  {
    id: 'grp-cell-a',
    name: 'Cell A — Dispensing & Fastening',
    description: 'Dispensing and screwing sub-cell',
    parentId: 'grp-subline-dispense',
    machineIds: ['STATION-SC-L06-03'],
    machineTypes: ['Gap Filler', 'Screwing Station'],
    groupType: 'SubLine',
    color: 'purple'
  },
  {
    id: 'grp-line09',
    name: 'Line 09 — Pack Assembly & End of Line',
    description: 'Battery pack finishing and test cell',
    parentId: 'grp-prj-audi',
    machineIds: ['IPC-L09-01'],
    machineTypes: ['Tester Cell', 'Pressing', 'Manipulator'],
    groupType: 'Line',
    color: 'emerald',
    leadEngineerId: 'usr-sally',
    leadEngineerName: 'Engineer Sally'
  }
]

function loadInitialGroups(): MachineGroupEntry[] {
  const result: MachineGroupEntry[] = featureFlags.enableDevFeatures ? [...FALLBACK_GROUPS] : []
  try {
    const datasetGroups = getPlantMachineGroups()
    if (datasetGroups && datasetGroups.length > 0) {
      for (const g of datasetGroups) {
        if (!result.some(existing => existing.id === g.id)) {
          result.push({
            id: g.id,
            name: g.name,
            description: g.description,
            parentId: g.parentId ?? null,
            machineIds: [...g.machineIds],
            machineTypes: g.machineTypes ? [...g.machineTypes] : [],
            color: g.color
          })
        }
      }
    }
  } catch {
    // Dataset not available, use fallback
  }
  return result
}

let groups: MachineGroupEntry[] = loadInitialGroups()

export function getAllGroups(): MachineGroupEntry[] {
  return groups
}

export function getGroupById(id: string): MachineGroupEntry | undefined {
  return groups.find(g => g.id === id)
}

export function createGroup(data: Omit<MachineGroupEntry, 'id'>): MachineGroupEntry {
  const newGroup: MachineGroupEntry = {
    id: `grp-${randomUUID().slice(0, 8)}`,
    name: data.name,
    description: data.description,
    parentId: data.parentId || null,
    machineIds: data.machineIds || [],
    machineTypes: data.machineTypes || [],
    color: data.color || 'indigo',
    icon: data.icon,
    leadEngineerId: data.leadEngineerId,
    leadEngineerName: data.leadEngineerName,
    groupType: data.groupType || 'Manual',
    interfaceContract: data.interfaceContract,
    technologyKey: data.technologyKey
  }
  groups.push(newGroup)
  return newGroup
}

export function updateGroup(id: string, updates: Partial<MachineGroupEntry>): MachineGroupEntry | null {
  const group = groups.find(g => g.id === id)
  if (!group) return null
  Object.assign(group, updates)
  return group
}

export function deleteGroup(id: string): boolean {
  const idx = groups.findIndex(g => g.id === id)
  if (idx === -1) return false
  const parentId = groups[idx].parentId || null
  for (const g of groups) {
    if (g.parentId === id) {
      g.parentId = parentId
    }
  }
  groups.splice(idx, 1)
  return true
}

/** Recursively collect all machine IDs belonging to group and all its descendant groups */
export function getAllMachineIdsInGroup(groupId: string): string[] {
  const set = new Set<string>()
  function collect(gid: string) {
    const g = groups.find(item => item.id === gid)
    if (!g) return
    for (const m of g.machineIds) set.add(m)
    const children = groups.filter(item => item.parentId === gid)
    for (const child of children) collect(child.id)
  }
  collect(groupId)
  return Array.from(set)
}
