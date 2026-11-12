/**
 * Shape adapters between the UI's machine-group model (`machineIds` / `machineTypes` arrays) and
 * the backend `MachineGroup` entity, which persists them as JSON strings.
 */
function parseJsonArray(value: unknown): string[] {
  if (Array.isArray(value)) return value.map(String)
  if (typeof value !== 'string' || !value) return []
  try {
    const parsed = JSON.parse(value)
    return Array.isArray(parsed) ? parsed.map(String) : []
  } catch {
    return []
  }
}

/** Wire values of the backend `MachineGroupType` enum (serialized as integers). */
const GROUP_TYPE_NAMES: Record<number, string> = {
  1: 'BusinessUnit', 2: 'Project', 3: 'Line', 4: 'SubLine', 5: 'Machine', 6: 'Technological', 7: 'Customer', 8: 'Manual'
}

const groupTypeName = (value: unknown): string | undefined =>
  typeof value === 'number' ? GROUP_TYPE_NAMES[value] : (typeof value === 'string' && value ? value : undefined)

const groupTypeValue = (value: unknown): number | undefined => {
  if (typeof value === 'number') return GROUP_TYPE_NAMES[value] ? value : undefined
  if (typeof value !== 'string') return undefined
  const entry = Object.entries(GROUP_TYPE_NAMES).find(([, name]) => name.toLowerCase() === value.toLowerCase())
  return entry ? Number(entry[0]) : undefined
}

export function fromBackendGroup(g: any) {
  if (!g || typeof g !== 'object') return g
  return {
    ...g,
    // The UI model uses the enum name; the backend sends its integer.
    groupType: groupTypeName(g.groupType),
    machineIds: Array.isArray(g.machineIds) ? g.machineIds : parseJsonArray(g.machineIdsJson),
    machineTypes: Array.isArray(g.machineTypes) ? g.machineTypes : parseJsonArray(g.machineTypesJson),
    // Aliases read by the delegation / preferred-technician dialogs.
    parentGroupId: g.parentId ?? null,
    leadEngineer: g.leadEngineerName ?? null
  }
}

export function toBackendGroup(body: any, defaults: { machineIdsJson?: string; machineTypesJson?: string | null } = {}) {
  // Server-managed fields are never taken from the client. `organizationId` is forwarded: the
  // backend (MachineGroupController) decides who owns a group - it honours a posted organization
  // only from platform administrators and otherwise uses the caller's active organization / keeps
  // the group where it is. Writes are never sent anywhere but through the backend.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { machineIds, machineTypes, id, createdAt, updatedAt, memberships, machines, children, parent, parentGroupId, leadEngineer, groupType, ...rest } = body || {}
  // `parentGroupId` / `leadEngineer` are the dialog aliases of `parentId` / `leadEngineerName`;
  // without this mapping the backend ignored them (new groups lost their parent, lead edits were dropped).
  if (rest.parentId === undefined && parentGroupId !== undefined) rest.parentId = parentGroupId
  if (rest.leadEngineerName === undefined && leadEngineer !== undefined) rest.leadEngineerName = leadEngineer
  // An empty selection means "no parent"; the backend expects null, not "".
  if (rest.parentId === '') rest.parentId = null
  const typeValue = groupTypeValue(groupType)
  return {
    ...rest,
    ...(typeValue !== undefined ? { groupType: typeValue } : {}),
    machineIdsJson: Array.isArray(machineIds) ? JSON.stringify(machineIds) : (rest.machineIdsJson ?? defaults.machineIdsJson),
    machineTypesJson: Array.isArray(machineTypes) ? JSON.stringify(machineTypes) : (rest.machineTypesJson ?? defaults.machineTypesJson)
  }
}
