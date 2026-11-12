import type { SearchResultItem } from '~/types/search'

/**
 * Navigation targets for search results and submitted queries.
 *
 * Every entity kind maps to the most specific deep link its page honours:
 *   machine    -> /dashboard/machines?search=<name>&tree=<id>   (opens the station workbench)
 *   controller -> /dashboard/clients?selected=<id>&hostname=<h> (selects the controller)
 *   ticket     -> /dashboard/tickets/<id>
 *   user       -> /dashboard/users/<id>
 *   inventory  -> /dashboard/inventory/<id>
 */

export type SearchEntityKind = 'machine' | 'controller' | 'ticket' | 'inventory' | 'user' | 'telemetry'

const KIND_BY_TYPE: Record<string, SearchEntityKind> = {
  machine: 'machine',
  machines: 'machine',
  station: 'machine',
  stations: 'machine',
  clientpc: 'controller',
  'client-pc': 'controller',
  endpoint: 'controller',
  endpoints: 'controller',
  controller: 'controller',
  controllers: 'controller',
  nodes: 'controller',
  node: 'controller',
  clients: 'controller',
  ticket: 'ticket',
  tickets: 'ticket',
  user: 'user',
  users: 'user',
  telemetry: 'telemetry'
}

/** Placeholder ids produced by templates for records without an id; they never deep-link. */
const isPlaceholderId = (id: string) => !id || /-unknown$/.test(id)

/** Entity kind of a result: the explicit item type wins over the table it came from. */
export function resolveEntityKind(item: Pick<SearchResultItem, 'itemType' | 'sourceTable'>): SearchEntityKind {
  const byType = KIND_BY_TYPE[String(item.itemType || '').toLowerCase()]
  if (byType) return byType
  return KIND_BY_TYPE[String(item.sourceTable || '').toLowerCase()] || 'inventory'
}

/** Deep link for one entity. `name` is the label the target page filters / selects by. */
export function buildEntityLink(kind: SearchEntityKind, id: string | undefined | null, name?: string | null): string {
  const safeId = id == null ? '' : String(id)
  const hasId = !isPlaceholderId(safeId)
  const label = name ? String(name) : ''

  switch (kind) {
    case 'machine': {
      const params = new URLSearchParams()
      if (label) params.set('search', label)
      if (hasId) params.set('tree', safeId)
      const qs = params.toString()
      return qs ? `/dashboard/machines?${qs}` : '/dashboard/machines'
    }
    case 'controller': {
      const params = new URLSearchParams()
      if (hasId) params.set('selected', safeId)
      if (label) params.set('hostname', label)
      const qs = params.toString()
      return qs ? `/dashboard/clients?${qs}` : '/dashboard/clients'
    }
    case 'ticket':
      return hasId ? `/dashboard/tickets/${encodeURIComponent(safeId)}` : '/dashboard/tickets'
    case 'user':
      return hasId ? `/dashboard/users/${encodeURIComponent(safeId)}` : '/dashboard/users'
    case 'telemetry':
      return '/dashboard/telemetry?tab=configurations'
    default:
      return hasId ? `/dashboard/inventory/${encodeURIComponent(safeId)}` : '/dashboard/inventory'
  }
}

/** Where selecting a result navigates to. An explicit `link` on the item is honoured. */
export function resolveResultLink(item: SearchResultItem): string {
  if (item.link) return item.link
  return buildEntityLink(resolveEntityKind(item), item.id, item.name)
}

const TAG_TOKEN = /(?:^|\s)([a-zA-Z0-9_-]+):(?:"([^"]*)"|(\S*))/g
const PREFIX_TOKEN = /(?:^|\s)([@#!*%])(?:"([^"]*)"|([a-zA-Z0-9_-]*))/g

const MACHINE_KEYS = new Set(['station', 'machine', 'line', 'tech'])
const CONTROLLER_KEYS = new Set(['host', 'ip', 'mac', 'client', 'node'])
const TICKET_KEYS = new Set(['ticket', 'code'])
const USER_KEYS = new Set(['user', 'person'])
/** Scope values that mean "everything of this kind" rather than a search term. */
const SCOPE_ALL = new Set(['all', 'machine', 'station', 'ticket', 'controller', 'ipc', 'part', 'inventory'])

interface ParsedSubmit {
  keys: Set<string>
  prefixes: Set<string>
  /** Tag values usable as a search term, in input order. */
  values: string[]
  freeText: string
}

function parseSubmittedQuery(query: string): ParsedSubmit {
  const keys = new Set<string>()
  const prefixes = new Set<string>()
  const values: string[] = []
  let rest = query

  for (const m of query.matchAll(TAG_TOKEN)) {
    const key = m[1].toLowerCase()
    const value = (m[2] ?? m[3] ?? '').trim()
    keys.add(key)
    if (value && !SCOPE_ALL.has(value.toLowerCase())) values.push(value)
    rest = rest.replace(m[0], ' ')
  }
  for (const m of rest.matchAll(PREFIX_TOKEN)) {
    const value = (m[2] ?? m[3] ?? '').trim()
    prefixes.add(m[1])
    if (value && !SCOPE_ALL.has(value.toLowerCase())) values.push(value)
    rest = rest.replace(m[0], ' ')
  }

  return { keys, prefixes, values, freeText: rest.replace(/\s+/g, ' ').trim() }
}

const hasAny = (set: Set<string>, candidates: Set<string>) => {
  for (const c of candidates) if (set.has(c)) return true
  return false
}

const withQuery = (path: string, param: string, term: string) =>
  term ? `${path}?${param}=${encodeURIComponent(term)}` : path

/**
 * Route for a submitted query (Enter without a highlighted result). The list page is picked
 * from explicit scope (`station:`, `host:`, `ticket:`, `@`, `!`, `#`), then from unambiguous
 * identifier shapes (IPC hostnames, IP / MAC addresses), then from the page the user is on.
 * Each page receives the term in the query parameter it actually reads (machines `?search=`,
 * clients `?hostname=`, tickets `?query=`, inventory `?query=`).
 */
export function resolveSubmitRoute(query: string, currentPath = ''): string | null {
  const raw = (query || '').trim()
  if (!raw) return null

  // A bare MAC address contains colons but is an identifier, not a `key:value` filter.
  if (/^([0-9a-f]{2}[:-]){2,5}[0-9a-f]{2}$/i.test(raw)) return withQuery('/dashboard/clients', 'hostname', raw)

  const parsed = parseSubmittedQuery(raw)
  const term = parsed.freeText || parsed.values[0] || ''
  const lower = term.toLowerCase()

  const machines = () => withQuery('/dashboard/machines', 'search', term)
  const clients = () => withQuery('/dashboard/clients', 'hostname', term)
  const tickets = () => withQuery('/dashboard/tickets', 'query', term)
  const inventory = () => withQuery('/dashboard/inventory', 'query', term)

  if (hasAny(parsed.keys, TICKET_KEYS) || parsed.prefixes.has('#')) return tickets()
  // The user directory has no query parameter; a submitted user search opens the directory.
  if (hasAny(parsed.keys, USER_KEYS)) return '/dashboard/users'
  if (hasAny(parsed.keys, CONTROLLER_KEYS) || parsed.prefixes.has('!')) return clients()
  if (hasAny(parsed.keys, MACHINE_KEYS) || parsed.prefixes.has('@')) return machines()
  if (parsed.keys.has('part') || parsed.prefixes.has('*')) return inventory()

  if (/^ipc[-_]/.test(lower) || /^\d{1,3}(\.\d{1,3}){1,3}$/.test(lower)) return clients()
  // Ticket numbers: "TKT-20260928-0001" as well as the id-derived "TKT-F2AF7680" (hex, may start with a letter).
  if (/^(tck|tkt|inc|sfc)([-_ ]?\d|[-_][0-9a-f]{4,})/.test(lower)) return tickets()

  if (currentPath.startsWith('/dashboard/machines')) return machines()
  if (currentPath.startsWith('/dashboard/clients')) return clients()
  if (currentPath.startsWith('/dashboard/tickets')) return tickets()

  return inventory()
}
