import { randomUUID } from 'node:crypto'

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
}

let groups: MachineGroupEntry[] = [
  {
    id: 'grp-plant',
    name: 'Battery Assembly Plant 01',
    description: 'Main production facility',
    parentId: null,
    machineIds: [],
    machineTypes: [],
    color: 'blue'
  },
  {
    id: 'grp-line06',
    name: 'Line 06 — Module Assembly (AUDI)',
    description: 'Automated battery module line',
    parentId: 'grp-plant',
    machineIds: ['STATION-OP10-01', 'STATION-SC-L06-03', 'STATION-AOI-L06-02'],
    machineTypes: ['Automatic Optical Inspection', 'Gap Filler', 'Screwing Station', 'Pressing'],
    color: 'indigo',
    leadEngineerId: 'usr-orwell',
    leadEngineerName: 'Engineer Orwell'
  },
  {
    id: 'grp-cell-a',
    name: 'Cell A — Dispensing & Fastening',
    description: 'Dispensing and screwing sub-cell',
    parentId: 'grp-line06',
    machineIds: ['STATION-SC-L06-03'],
    machineTypes: ['Gap Filler', 'Screwing Station'],
    color: 'purple'
  },
  {
    id: 'grp-line09',
    name: 'Line 09 — Pack Assembly & End of Line',
    description: 'Battery pack finishing and test cell',
    parentId: 'grp-plant',
    machineIds: ['IPC-L09-01'],
    machineTypes: ['Tester Cell', 'Pressing', 'Manipulator'],
    color: 'emerald',
    leadEngineerId: 'usr-sally',
    leadEngineerName: 'Engineer Sally'
  }
]

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
    leadEngineerName: data.leadEngineerName
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
