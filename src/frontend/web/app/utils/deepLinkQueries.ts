/**
 * What the dashboard list pages do with the query parameters they are sent (Vue-free, unit-tested).
 *
 *   /dashboard/machines  ?search=<text>  ?id=<uuid>  ?tree=<uuid>  ?decommission=<uuid|identifier>
 *   /dashboard/tickets   ?query=<text> (alias ?search=)  ?stationId=<machine uuid>  ?handle=<CAD handle>
 *                        ?team=  ?technician=  ?ticketId=  ?create=true
 *   /dashboard/map       ?stationId=<handle>
 *   /dashboard/clients   ?selected=<id>  ?hostname=<h>
 *
 * Stations are known by several identifiers (machine uuid, custom identifier, name, floor-plan
 * handle) and tickets store whichever one their creator had, so station matching always goes
 * through the full identifier set of the station.
 */

const lower = (value: unknown): string => (typeof value === 'string' ? value.trim().toLowerCase() : '')
const same = (a: unknown, b: unknown): boolean => {
  const left = lower(a)
  return left !== '' && left === lower(b)
}

/** First non-empty string of a route query value (repeated parameters arrive as arrays). */
export function queryText(value: unknown): string {
  if (Array.isArray(value)) {
    for (const entry of value) {
      const found = queryText(entry)
      if (found) return found
    }
    return ''
  }
  return typeof value === 'string' ? value.trim() : ''
}

type QueryRecord = Record<string, unknown> | null | undefined

// ── Machines ────────────────────────────────────────────────────────────────

export interface MachineRecordLike {
  id?: string | null
  name?: string | null
  displayName?: string | null
  customIdentifier?: string | null
  pinnedObjectHandle?: string | null
  groupId?: string | null
  lineGroupName?: string | null
  machineType?: string | null
  status?: string | null
  controllers?: Array<{ hostname?: string | null } | null> | null
}

export interface MachinesDeepLink {
  /** Free text for the directory search box. */
  search: string
  /** Station to narrow the directory to. */
  id: string
  /** Station whose workbench (component tree) opens. */
  tree: string
  /** Station whose decommission dialog opens. */
  decommission: string
}

export function parseMachinesQuery(query: QueryRecord): MachinesDeepLink {
  return {
    search: queryText(query?.search) || queryText(query?.query),
    id: queryText(query?.id),
    tree: queryText(query?.tree),
    decommission: queryText(query?.decommission)
  }
}

/** A station by any of its identifiers: the uuid first, then identifier, name and handle. */
export function findMachineByRef<T extends MachineRecordLike>(machines: readonly T[] | null | undefined, ref: string | null | undefined): T | null {
  const wanted = lower(ref)
  if (!wanted || !machines) return null
  return (
    machines.find(m => lower(m.id) === wanted) ??
    machines.find(m => lower(m.customIdentifier) === wanted) ??
    machines.find(m => lower(m.name) === wanted) ??
    machines.find(m => lower(m.pinnedObjectHandle) === wanted) ??
    null
  )
}

/** Directory text search: names, identifiers, line and controller hostnames; a full id also matches. */
export function machineMatchesText(machine: MachineRecordLike, text: string | null | undefined): boolean {
  const q = lower(text)
  if (!q) return true
  if (lower(machine.id) === q) return true
  const fields = [machine.name, machine.displayName, machine.customIdentifier, machine.pinnedObjectHandle, machine.groupId, machine.lineGroupName]
  if (fields.some(field => lower(field).includes(q))) return true
  return (machine.controllers || []).some(c => lower(c?.hostname).includes(q))
}

export function machineMatchesStatus(machine: MachineRecordLike, statusFilter: string | null | undefined): boolean {
  switch (statusFilter) {
    case 'active_floor':
      return machine.status === 'active' || machine.status === 'maintenance'
    case 'maintenance':
      return machine.status === 'maintenance'
    case 'decommissioned':
      return machine.status === 'decommissioned' || machine.status === 'dismantled'
    default:
      return true
  }
}

export interface MachineDirectoryFilter {
  statusFilter?: string | null
  search?: string | null
  /** Id of the station a `?id=` deep link resolved to; it is shown whatever the other filters say. */
  focusId?: string | null
  isTypeVisible?: (machineType: string | null | undefined) => boolean
}

export function filterMachineDirectory<T extends MachineRecordLike>(machines: readonly T[] | null | undefined, filter: MachineDirectoryFilter = {}): T[] {
  const list = machines || []
  if (filter.focusId) {
    const focused = list.filter(m => m.id === filter.focusId)
    if (focused.length > 0) return focused
  }
  return list.filter(m =>
    machineMatchesStatus(m, filter.statusFilter) &&
    (!filter.isTypeVisible || filter.isTypeVisible(m.machineType)) &&
    machineMatchesText(m, filter.search)
  )
}

// ── Tickets ─────────────────────────────────────────────────────────────────

export interface StationRecordLike {
  id?: string | null
  name?: string | null
  displayName?: string | null
  customIdentifier?: string | null
  pinnedObjectHandle?: string | null
  responsibleTeams?: Array<{ name?: string | null } | null> | null
}

export interface TicketRecordLike {
  id?: string | null
  ticketNumber?: string | null
  title?: string | null
  description?: string | null
  stationId?: string | null
  /** Not part of the ticket contract, but present on some payloads. */
  machineId?: string | null
  stationName?: string | null
  controllerName?: string | null
  assignedTechnicianName?: string | null
  responsibleDepartment?: string | null
  errorCode?: string | null
  tags?: Array<string | null> | null
}

export interface TicketsDeepLink {
  /** Free text (`?query=`, alias `?search=`). */
  text: string
  stationId: string
  handle: string
  team: string
  technician: string
  ticketId: string
  create: boolean
}

export function parseTicketsQuery(query: QueryRecord): TicketsDeepLink {
  return {
    text: queryText(query?.query) || queryText(query?.search),
    stationId: queryText(query?.stationId),
    handle: queryText(query?.handle),
    team: queryText(query?.team),
    technician: queryText(query?.technician),
    ticketId: queryText(query?.ticketId),
    create: queryText(query?.create) === 'true'
  }
}

/** Every identifier a station is known by (lower-cased). */
export function stationIdentifiers(station: StationRecordLike | null | undefined): string[] {
  if (!station) return []
  return [station.id, station.customIdentifier, station.pinnedObjectHandle, station.name].map(lower).filter(Boolean)
}

/**
 * Identifiers a ticket of the requested station can carry: the given values plus, when the
 * station directory knows a station by one of them, all identifiers of that station.
 */
export function stationAliases(refs: ReadonlyArray<string | null | undefined>, stations: readonly StationRecordLike[] | null | undefined = []): Set<string> {
  const aliases = new Set<string>()
  for (const ref of refs) {
    const wanted = lower(ref)
    if (!wanted) continue
    aliases.add(wanted)
    for (const station of stations || []) {
      const ids = stationIdentifiers(station)
      if (ids.includes(wanted)) ids.forEach(id => aliases.add(id))
    }
  }
  return aliases
}

const tagKey = (tag: unknown): string => lower(tag).replace(/^#/, '')

/** Exact identity match against the ticket's station id, machine id or tags (never a substring). */
export function ticketMatchesStation(ticket: TicketRecordLike, aliases: ReadonlySet<string>): boolean {
  if (aliases.size === 0) return false
  if (aliases.has(lower(ticket.stationId)) || aliases.has(lower(ticket.machineId))) return true
  return (ticket.tags || []).some(tag => aliases.has(lower(tag)) || aliases.has(tagKey(tag)))
}

/** Free text: every whitespace-separated term has to occur in one of the ticket's text fields. */
export function ticketMatchesText(ticket: TicketRecordLike, text: string | null | undefined): boolean {
  const terms = lower(text).split(/\s+/).filter(Boolean)
  if (terms.length === 0) return true
  const haystack = [
    ticket.title,
    ticket.ticketNumber,
    ticket.stationName,
    ticket.assignedTechnicianName,
    ticket.description,
    ticket.stationId,
    ticket.controllerName,
    ticket.errorCode,
    ...(ticket.tags || [])
  ].map(lower).filter(Boolean).join('\n')
  return terms.every(term => haystack.includes(term))
}

/**
 * Tickets of a team: tagged with it, raised for it as the responsible department, or reported on
 * a station the team is responsible for (`stationSet`, see `teamStationAliases`).
 */
export function ticketMatchesTeam(ticket: TicketRecordLike, team: string, stationSet: ReadonlySet<string> = new Set()): boolean {
  const wanted = lower(team)
  if (!wanted) return true
  if (lower(ticket.responsibleDepartment) === wanted) return true
  if ((ticket.tags || []).some(tag => lower(tag) === wanted || tagKey(tag) === wanted)) return true
  return ticketMatchesStation(ticket, stationSet)
}

/** Identifiers of all stations a team is responsible for. */
export function teamStationAliases(team: string, stations: readonly StationRecordLike[] | null | undefined = []): Set<string> {
  const aliases = new Set<string>()
  for (const station of stations || []) {
    if ((station.responsibleTeams || []).some(t => same(t?.name, team))) {
      stationIdentifiers(station).forEach(id => aliases.add(id))
    }
  }
  return aliases
}

/** Applies the URL filters of the tickets page; `stations` widens station and team matching. */
export function filterTicketsByDeepLink<T extends TicketRecordLike>(
  tickets: readonly T[] | null | undefined,
  link: Pick<TicketsDeepLink, 'text' | 'stationId' | 'handle' | 'team' | 'technician'>,
  stations: readonly StationRecordLike[] | null | undefined = []
): T[] {
  let list = [...(tickets || [])]

  if (link.stationId || link.handle) {
    const aliases = stationAliases([link.stationId, link.handle], stations)
    list = list.filter(t => ticketMatchesStation(t, aliases))
  }
  if (link.team) {
    const aliases = teamStationAliases(link.team, stations)
    list = list.filter(t => ticketMatchesTeam(t, link.team, aliases))
  }
  if (link.technician) {
    const technician = lower(link.technician)
    list = list.filter(t => lower(t.assignedTechnicianName).includes(technician))
  }
  if (link.text) {
    list = list.filter(t => ticketMatchesText(t, link.text))
  }
  return list
}

/**
 * Older links put a search term into `?stationId=`. Once the station directory is loaded, a value
 * that is no identifier of any station and no station id of any ticket is such a term: it is
 * treated as free text instead of filtering everything away.
 */
export function normalizeTicketsDeepLink<L extends Pick<TicketsDeepLink, 'text' | 'stationId' | 'handle'>>(
  link: L,
  tickets: readonly TicketRecordLike[] | null | undefined,
  stations: readonly StationRecordLike[] | null | undefined
): L {
  if (!link.stationId || link.handle || link.text) return link
  if (!stations || stations.length === 0) return link
  const wanted = lower(link.stationId)
  if (stations.some(s => stationIdentifiers(s).includes(wanted))) return link
  const identity = new Set([wanted])
  if ((tickets || []).some(t => ticketMatchesStation(t, identity))) return link
  return { ...link, stationId: '', text: link.stationId }
}

/** Label of the station a `?stationId=` / `?handle=` filter points at, for the filter chip. */
export function describeStationFilter(link: Pick<TicketsDeepLink, 'stationId' | 'handle'>, stations: readonly StationRecordLike[] | null | undefined = []): string {
  const refs = [link.stationId, link.handle].map(lower).filter(Boolean)
  const station = (stations || []).find(s => stationIdentifiers(s).some(id => refs.includes(id)))
  if (station) return station.displayName || station.name || station.customIdentifier || link.handle || link.stationId
  return link.handle || link.stationId
}

// ── Plant map ───────────────────────────────────────────────────────────────

export interface PinnedRecordLike {
  id?: string | null
  name?: string | null
  hostname?: string | null
  customIdentifier?: string | null
  pinnedObjectHandle?: string | null
}

export interface MapFocusTarget {
  /** Floor-plan handle to select. */
  handle: string
  /** Name of the station / controller pinned there, when the directories know it. */
  name: string
  /** False while no pinned station or controller is known for the request. */
  resolved: boolean
}

/**
 * Floor-plan handle for a `?stationId=` request. The value is normally a handle, but a machine
 * uuid, identifier, name or a controller id / hostname of a pinned asset is accepted as well.
 */
export function resolveMapFocus(
  ref: string | null | undefined,
  stations: readonly PinnedRecordLike[] | null | undefined = [],
  controllers: readonly PinnedRecordLike[] | null | undefined = []
): MapFocusTarget | null {
  const request = typeof ref === 'string' ? ref.trim() : ''
  if (!request) return null
  const pinned = (list: readonly PinnedRecordLike[] | null | undefined) => (list || []).filter(item => !!item?.pinnedObjectHandle)
  const label = (item: PinnedRecordLike) => item.hostname || item.customIdentifier || item.name || ''

  const hit =
    pinned(stations).find(s => same(s.pinnedObjectHandle, request)) ??
    pinned(controllers).find(c => same(c.pinnedObjectHandle, request)) ??
    pinned(stations).find(s => same(s.id, request) || same(s.customIdentifier, request) || same(s.name, request)) ??
    pinned(controllers).find(c => same(c.id, request) || same(c.hostname, request) || same(c.name, request))

  if (hit) return { handle: hit.pinnedObjectHandle as string, name: label(hit), resolved: true }
  return { handle: request, name: '', resolved: false }
}

// ── Controllers ─────────────────────────────────────────────────────────────

export interface ControllerRecordLike {
  id?: string | null
  hostname?: string | null
}

export interface ControllerSelection<T> {
  /** Controller the query names, when the fleet has it. */
  controller: T | null
  /** Text to filter the fleet by when no controller matched (a searched hostname fragment). */
  filterText: string
}

/** `?selected=<id>&hostname=<h>`: by id, by hostname, then by a hostname sent as the id. */
export function resolveControllerSelection<T extends ControllerRecordLike>(
  controllers: readonly T[] | null | undefined,
  selected: string | null | undefined,
  hostname: string | null | undefined
): ControllerSelection<T> {
  const list = controllers || []
  const id = typeof selected === 'string' ? selected.trim() : ''
  const host = typeof hostname === 'string' ? hostname.trim() : ''
  const controller =
    (id ? list.find(c => c.id === id) : undefined) ??
    (host ? list.find(c => same(c.hostname, host)) : undefined) ??
    (id ? list.find(c => same(c.hostname, id)) : undefined) ??
    null
  return { controller, filterText: controller ? '' : host }
}
