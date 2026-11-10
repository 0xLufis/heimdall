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

export function fromBackendGroup(g: any) {
  if (!g || typeof g !== 'object') return g
  return {
    ...g,
    machineIds: Array.isArray(g.machineIds) ? g.machineIds : parseJsonArray(g.machineIdsJson),
    machineTypes: Array.isArray(g.machineTypes) ? g.machineTypes : parseJsonArray(g.machineTypesJson)
  }
}

export function toBackendGroup(body: any, defaults: { machineIdsJson?: string; machineTypesJson?: string | null } = {}) {
  // Server-managed fields are never taken from the client.
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  const { machineIds, machineTypes, id, organizationId, createdAt, updatedAt, memberships, machines, children, parent, ...rest } = body || {}
  return {
    ...rest,
    machineIdsJson: Array.isArray(machineIds) ? JSON.stringify(machineIds) : (rest.machineIdsJson ?? defaults.machineIdsJson),
    machineTypesJson: Array.isArray(machineTypes) ? JSON.stringify(machineTypes) : (rest.machineTypesJson ?? defaults.machineTypesJson)
  }
}
