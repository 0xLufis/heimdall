import { buildEntityLink, resolveEntityKind } from './search/searchNavigation'
import type {
  ContextEntityType,
  ContextIdentity,
  ContextMenuAction,
  ContextMenuActionOptions,
  ContextMenuContextData,
  MenuPoint,
  MenuSize
} from '~/types/contextMenu'

/**
 * Rules of the global right-click menu (Vue-free, unit-tested).
 *
 * `buildContextMenuActions` decides which entries an entity gets and where each one leads. An
 * entry is only offered when the context carries the data its destination needs, so a ticket
 * row never offers "Decommission Machine" and a controller id is never sent as a station id.
 *
 * Deep links follow what the destination pages read:
 *   machines   ?id=<uuid> (focus in directory) | ?search=<name>&tree=<uuid> (station workbench)
 *              ?decommission=<uuid>
 *   clients    ?selected=<id>&hostname=<h>
 *   tickets    ?stationId=<machine uuid>&handle=<CAD handle> | ?team= | ?technician=
 *   tickets/new ?stationId=&stationName=&controllerId=<hostname>
 *   telemetry  ?station=<machine uuid | handle> | ?controllerId=<id>
 *   map        ?stationId=<handle>
 *   detail     /dashboard/tickets/<id>, /dashboard/inventory/<id>, /dashboard/users/<id>
 */

const text = (value: unknown): string => (typeof value === 'string' ? value.trim() : typeof value === 'number' ? String(value) : '')

/** Ids produced for records without one (`machine-unknown`) never deep-link. */
const usableId = (value: unknown): string => {
  const id = text(value)
  return !id || /-unknown$/.test(id) || id === 'undefined' || id === 'null' ? '' : id
}

const withQuery = (path: string, params: Record<string, string | undefined>): string => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  const qs = search.toString()
  return qs ? `${path}?${qs}` : path
}

/** The ids a context really carries: the entity's own id only counts for its own kind. */
export function resolveContextIdentity(data: ContextMenuContextData | null | undefined): ContextIdentity {
  const type: ContextEntityType = data?.entityType || 'general'
  const entityId = usableId(data?.entityId)
  const entityName = text(data?.entityName)
  return {
    type,
    entityId,
    entityName,
    machineId: usableId(data?.machineId) || (type === 'machine' ? entityId : ''),
    machineName: text(data?.machineName) || (type === 'machine' ? entityName : ''),
    controllerId: usableId(data?.controllerId) || (type === 'controller' ? entityId : ''),
    controllerHostname: text(data?.controllerHostname) || (type === 'controller' ? entityName : ''),
    handle: text(data?.handle)
  }
}

/** Machine directory narrowed to one station (`?id=`), or filtered by name when no id is known. */
export function machineDirectoryLink(machineId?: string | null, machineName?: string | null): string {
  const id = usableId(machineId)
  if (id) return withQuery('/dashboard/machines', { id })
  return withQuery('/dashboard/machines', { search: text(machineName) })
}

/** Ticket list of one station: by machine id and / or floor-plan handle. */
export function ticketsOfStationLink(machineId?: string | null, handle?: string | null): string {
  return withQuery('/dashboard/tickets', { stationId: usableId(machineId), handle: text(handle) })
}

/** New-incident form pre-filled with the station (and its controller hostname when known). */
export function createTicketLink(id: Pick<ContextIdentity, 'machineId' | 'machineName' | 'handle' | 'controllerHostname'>): string {
  const stationId = id.machineId || id.handle
  return withQuery('/dashboard/tickets/new', {
    stationId,
    stationName: stationId ? id.machineName : '',
    controllerId: id.controllerHostname
  })
}

/** Telemetry hub on the station (machine id, else handle) or on the controller's first station. */
export function telemetryLink(id: Pick<ContextIdentity, 'type' | 'machineId' | 'handle' | 'controllerId'>): string | null {
  if (id.type === 'controller' && id.controllerId) return withQuery('/dashboard/telemetry', { controllerId: id.controllerId })
  if (id.machineId) return withQuery('/dashboard/telemetry', { station: id.machineId })
  // A floor-plan node that is not bound to a machine is still addressable by its handle.
  if (id.type === 'map-node' && id.handle) return withQuery('/dashboard/telemetry', { station: id.handle })
  if (id.controllerId) return withQuery('/dashboard/telemetry', { controllerId: id.controllerId })
  return null
}

export function mapLink(handle?: string | null): string {
  return withQuery('/dashboard/map', { stationId: text(handle) })
}

/**
 * The entity's own page: the same deep link a search result of that entity opens
 * (`buildEntityLink`), or the explicit `link` the context carries.
 */
export function primaryLink(data: ContextMenuContextData | null | undefined): string | null {
  if (!data) return null
  const explicit = text(data.link)
  if (explicit.startsWith('/')) return explicit

  const id = resolveContextIdentity(data)
  switch (id.type) {
    case 'ticket':
      return id.entityId ? buildEntityLink('ticket', id.entityId) : null
    case 'inventory':
      return id.entityId ? buildEntityLink('inventory', id.entityId) : null
    case 'user':
      return id.entityId ? buildEntityLink('user', id.entityId) : null
    case 'controller':
      return id.controllerId || id.controllerHostname
        ? buildEntityLink('controller', id.controllerId, id.controllerHostname)
        : null
    case 'machine':
      return id.machineId || id.machineName ? buildEntityLink('machine', id.machineId, id.machineName) : null
    case 'map-node':
      if (id.machineId) return buildEntityLink('machine', id.machineId, id.machineName)
      if (id.controllerId) return buildEntityLink('controller', id.controllerId, id.controllerHostname)
      return id.handle ? mapLink(id.handle) : null
    default:
      return null
  }
}

const OPEN_LABEL: Partial<Record<ContextEntityType, string>> = {
  ticket: 'Open Ticket',
  inventory: 'Open Inventory Item',
  user: 'Open User Profile'
}

/** Entity kinds whose menu carries the station work actions (tickets, telemetry, plant map). */
const STATION_WORK_TYPES: ReadonlySet<ContextEntityType> = new Set<ContextEntityType>(['machine', 'map-node', 'controller', 'ticket'])

/**
 * Ordered navigation entries of the menu for one entity. Copy / export / native-menu utilities
 * need no data and are added by the component.
 */
export function buildContextMenuActions(
  data: ContextMenuContextData | null | undefined,
  options: ContextMenuActionOptions = {}
): ContextMenuAction[] {
  if (!data) return []
  const id = resolveContextIdentity(data)
  const actions: ContextMenuAction[] = []
  const primary = primaryLink(data)
  const onMap = (options.currentPath || '').startsWith('/dashboard/map')

  // 1. The entity's own page (tickets, parts and users have a detail route).
  const openLabel = OPEN_LABEL[id.type]
  if (openLabel && primary) {
    actions.push({ key: 'open', label: openLabel, group: 'navigate', route: primary })
  }

  // 2. The station the entity is, or belongs to.
  if (id.machineId || id.machineName) {
    actions.push({ key: 'machine', label: 'Go to Machine', group: 'navigate', route: machineDirectoryLink(id.machineId, id.machineName) })
  }

  // 3. The controller the entity is, or is bound to.
  if (id.controllerId || id.controllerHostname) {
    actions.push({
      key: 'controller',
      label: 'Go to Node / Controller',
      group: 'navigate',
      route: buildEntityLink('controller', id.controllerId, id.controllerHostname)
    })
  }

  if (primary) {
    actions.push({ key: 'new-tab', label: 'Open in New Tab', group: 'navigate', route: primary, newTab: true })
  }

  const teamName = text(data.ownerTeam?.name)
  if (teamName) {
    actions.push({ key: 'owner-team', label: `Owner Team: ${teamName}`, group: 'navigate', route: withQuery('/dashboard/tickets', { team: teamName }) })
  }

  const personName = text(data.ownerPerson?.name) || (id.type === 'user' ? id.entityName : '')
  if (personName) {
    const label = id.type === 'user'
      ? `Tickets Assigned to ${personName}`
      : id.type === 'ticket' ? `Assignee: ${personName}` : `Owner Person: ${personName}`
    actions.push({ key: 'owner-person', label, group: 'navigate', route: withQuery('/dashboard/tickets', { technician: personName }) })
  }

  // 4. Work on the station: only for kinds that live on the shopfloor.
  if (STATION_WORK_TYPES.has(id.type)) {
    if (id.handle && !onMap) {
      actions.push({ key: 'map', label: 'Show on Plant Map', group: 'work', route: mapLink(id.handle) })
    }

    if (id.machineId || id.handle) {
      const count = Array.isArray(data.tickets) ? data.tickets.length : 0
      actions.push({
        key: 'tickets',
        label: 'Go to Maintenance Tickets',
        group: 'work',
        route: ticketsOfStationLink(id.machineId, id.handle),
        ...(count > 0 ? { badge: count } : {})
      })
    }

    if (id.machineId || id.handle || id.controllerHostname) {
      actions.push({ key: 'create-ticket', label: 'Create Incident Ticket', group: 'work', route: createTicketLink(id) })
    }

    const telemetry = telemetryLink(id)
    if (telemetry) {
      actions.push({ key: 'telemetry', label: 'Live Telemetry & Diagnostics', group: 'work', route: telemetry })
    }
  }

  // 5. The station's hardware tree: also where an installed part lives.
  if ((STATION_WORK_TYPES.has(id.type) || id.type === 'inventory') && id.machineId) {
    actions.push({
      key: 'component-tree',
      label: 'Inspect Component Tree',
      group: 'work',
      route: buildEntityLink('machine', id.machineId, id.machineName)
    })
  }

  // 6. Destructive: only on the machine itself.
  if (id.type === 'machine' && id.machineId) {
    actions.push({
      key: 'decommission',
      label: 'Decommission Machine',
      group: 'danger',
      route: withQuery('/dashboard/machines', { decommission: id.machineId })
    })
  }

  return actions
}

const BADGE: Record<ContextEntityType, string> = {
  'map-node': 'CAD NODE',
  machine: 'MACHINE',
  controller: 'CONTROLLER',
  ticket: 'TICKET',
  inventory: 'INVENTORY',
  user: 'USER',
  general: 'ASSET'
}

export function contextBadge(data: ContextMenuContextData | null | undefined): string {
  return BADGE[data?.entityType || 'general'] || 'ASSET'
}

export function contextDisplayName(data: ContextMenuContextData | null | undefined): string {
  if (!data) return 'Entity'
  return text(data.entityName) || text(data.machineName) || text(data.controllerHostname) || text(data.entityCode) || text(data.handle) || 'Selected Asset'
}

/** What "Copy Handle / ID" puts on the clipboard: the most recognisable identifier there is. */
export function contextCopyValue(data: ContextMenuContextData | null | undefined): string {
  if (!data) return ''
  return text(data.handle) || text(data.entityCode) || usableId(data.entityId) || text(data.entityName) || text(data.machineName) || text(data.controllerHostname)
}

/** Context of a search result row, so its menu opens the same pages the result itself does. */
export function searchResultContext(item: {
  id?: string
  name?: string
  displayName?: string
  itemType?: string
  sourceTable?: string
  link?: string
} | null | undefined): ContextMenuContextData | null {
  if (!item) return null
  const kind = resolveEntityKind({ itemType: item.itemType || '', sourceTable: item.sourceTable })
  const entityId = usableId(item.id) || undefined
  const name = text(item.name) || undefined
  const base: ContextMenuContextData = {
    entityId,
    entityName: text(item.displayName) || name,
    ...(text(item.link).startsWith('/') ? { link: text(item.link) } : {})
  }
  switch (kind) {
    case 'machine':
      // `name` is the label the machine directory filters by.
      return { ...base, entityType: 'machine', machineId: entityId, machineName: name }
    case 'controller':
      return { ...base, entityType: 'controller', controllerId: entityId, controllerHostname: name }
    case 'ticket':
      return { ...base, entityType: 'ticket' }
    case 'user':
      return { ...base, entityType: 'user' }
    case 'telemetry':
      return { ...base, entityType: 'general', link: base.link || buildEntityLink('telemetry', entityId, name) }
    default:
      return { ...base, entityType: 'inventory' }
  }
}

const named = (name: unknown): { name: string } | undefined => {
  const value = text(name)
  return value ? { name: value } : undefined
}

/**
 * Context of a list row from the record the row renders (`v-ctx:ticket="tkt"`). Only fields the
 * record really has are read; an unknown kind passes the value through as context data.
 *
 *   ticket      MaintenanceTicket          controller  IndustrialController
 *   inventory   InventoryPart / tree item  user        user record (id, name, email)
 *   machine     machine directory record   station     station KPI row (stationId, handle, name)
 *   search      search result item
 */
export function rowContext(kind: string | undefined, row: any): ContextMenuContextData | null {
  if (!row || typeof row !== 'object') return null
  const id = usableId(row.id) || undefined
  switch (kind) {
    case 'search':
      return searchResultContext(row)
    case 'ticket':
      return {
        entityType: 'ticket',
        entityId: id,
        entityName: text(row.title) || text(row.ticketNumber) || undefined,
        entityCode: text(row.ticketNumber) || undefined,
        machineId: usableId(row.stationId) || undefined,
        machineName: text(row.stationName) || undefined,
        controllerId: usableId(row.controllerId) || undefined,
        controllerHostname: text(row.controllerName) || undefined,
        ownerPerson: named(row.assignedTechnicianName)
      }
    case 'inventory':
      return {
        entityType: 'inventory',
        entityId: id,
        entityName: text(row.displayName) || text(row.name) || undefined,
        entityCode: text(row.customIdentifier) || text(row.serialNumber) || undefined,
        machineId: usableId(row.linkedMachineId) || usableId(row.machineId) || undefined,
        machineName: text(row.linkedMachineName) || undefined
      }
    case 'controller': {
      const station = Array.isArray(row.controlledMachines) ? row.controlledMachines[0] : undefined
      return {
        entityType: 'controller',
        entityId: id,
        entityName: text(row.hostname) || text(row.name) || undefined,
        handle: text(row.pinnedObjectHandle) || undefined,
        controllerId: id,
        controllerHostname: text(row.hostname) || undefined,
        machineId: usableId(station?.id) || undefined,
        machineName: text(station?.name) || undefined,
        ownerTeam: named(row.responsibleTeams?.[0]?.name)
      }
    }
    case 'user':
      return {
        entityType: 'user',
        entityId: id,
        entityName: text(row.name) || text(row.email) || undefined,
        entityCode: text(row.email) || undefined
      }
    case 'machine': {
      const controller = Array.isArray(row.controllers) ? row.controllers[0] : undefined
      return {
        entityType: 'machine',
        entityId: id,
        entityName: text(row.displayName) || text(row.name) || text(row.customIdentifier) || undefined,
        handle: text(row.pinnedObjectHandle) || undefined,
        machineId: id,
        machineName: text(row.name) || text(row.customIdentifier) || undefined,
        controllerId: usableId(controller?.controllerId) || usableId(controller?.id) || undefined,
        controllerHostname: text(controller?.hostname) || undefined,
        ownerTeam: named(row.responsibleTeams?.[0]?.name),
        ownerPerson: named(row.preferredTechnicianName)
      }
    }
    case 'station': {
      const stationId = usableId(row.stationId) || undefined
      return {
        entityType: 'machine',
        entityId: stationId,
        entityName: text(row.name) || text(row.handle) || undefined,
        handle: text(row.handle) || undefined,
        machineId: stationId,
        machineName: text(row.name) || undefined
      }
    }
    default:
      return row as ContextMenuContextData
  }
}

/** Keeps a menu of the given size inside the viewport on both axes (shifted, never cut off). */
export function clampMenuPosition(anchor: MenuPoint, menu: MenuSize, viewport: MenuSize, padding = 12): MenuPoint {
  const clampAxis = (value: number, size: number, limit: number) => {
    const max = limit - size - padding
    // A menu larger than the viewport is pinned to the leading edge and scrolls inside.
    if (max < padding) return padding
    return Math.min(Math.max(value, padding), max)
  }
  return {
    x: Math.round(clampAxis(anchor.x, menu.width, viewport.width)),
    y: Math.round(clampAxis(anchor.y, menu.height, viewport.height))
  }
}
