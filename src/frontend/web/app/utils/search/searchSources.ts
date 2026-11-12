import type { SearchResultItem, TagPill } from '~/types/search'
import { buildEntityLink } from './searchNavigation'
import { resolveSearchTemplate } from './searchTemplates'

/**
 * Search sources.
 *
 * `searchServer` asks the backend (`GET /api/search`, tenant-scoped there) for stations,
 * controllers, tickets, users and inventory in one request; nothing but the matches reaches
 * the browser.
 *
 * The entity catalogues below are the fallback for when that endpoint cannot be reached: they
 * are fetched once, shared by every search instance for a short time and ranked client-side.
 * Concurrent callers share the in-flight request; a failed request is not cached. Users have
 * no catalogue on purpose: the user directory is never downloaded for searching.
 */

export type CatalogName = 'machines' | 'controllers' | 'tickets'

const CATALOG_TTL_MS = 30_000

const CATALOG_URLS: Record<CatalogName, string> = {
  machines: '/api/proxy/v1/machine',
  controllers: '/api/proxy/v1/ClientPc',
  tickets: '/api/tickets'
}

type Fetcher = (url: string) => Promise<any>

const catalogCache = new Map<CatalogName, { at: number; promise: Promise<any[]> }>()

const defaultFetcher: Fetcher = (url) => $fetch<any>(url)

const toArray = (res: any): any[] => {
  if (Array.isArray(res)) return res
  if (res && Array.isArray(res.tickets)) return res.tickets
  if (res && Array.isArray(res.items)) return res.items
  return []
}

export function loadCatalog(name: CatalogName, fetcher: Fetcher = defaultFetcher, now: number = Date.now()): Promise<any[]> {
  const cached = catalogCache.get(name)
  if (cached && now - cached.at < CATALOG_TTL_MS) return cached.promise

  // The fetcher may throw synchronously (e.g. no $fetch outside Nuxt); treat that as a rejection.
  const promise = Promise.resolve()
    .then(() => fetcher(CATALOG_URLS[name]))
    .then(toArray)
  catalogCache.set(name, { at: now, promise })
  promise.catch(() => {
    if (catalogCache.get(name)?.promise === promise) catalogCache.delete(name)
  })
  return promise
}

/** Drops cached catalogues (all, or one) so the next search refetches. */
export function invalidateCatalog(name?: CatalogName) {
  if (name) catalogCache.delete(name)
  else catalogCache.clear()
}

/** Fields a record is ranked on, most important first. */
export const CATALOG_FIELDS: Record<CatalogName, (raw: any) => Array<string | null | undefined>> = {
  machines: (m) => [m.name, m.displayName, m.customIdentifier, m.pinnedObjectHandle, m.machineType, m.groupId, m.technology],
  controllers: (c) => [c.hostname, c.name, c.displayName, c.ipAddress ?? c.systemMetadata?.IPAddress, c.macAddress, c.machineIdentifier],
  tickets: (t) => [t.ticketNumber, t.title, t.stationName, t.assignedTechnicianName, Array.isArray(t.tags) ? t.tags.join(' ') : undefined, t.description]
}

export function ticketToSearchItem(raw: any): SearchResultItem {
  const number = raw.ticketNumber ? String(raw.ticketNumber) : ''
  const title = raw.title || number || 'Ticket'
  const subtitle = [number, raw.stationName, raw.priority].filter(Boolean).join(' • ')
  return {
    id: String(raw.id ?? ''),
    name: title,
    displayName: number && number !== title ? number : undefined,
    itemType: 'Ticket',
    typeLabel: raw.status ? `Ticket · ${raw.status}` : 'Ticket',
    subtitle: subtitle || undefined,
    status: raw.status,
    sourceTable: 'tickets',
    link: buildEntityLink('ticket', raw.id),
    metadata: { priority: raw.priority, stationName: raw.stationName, assignee: raw.assignedTechnicianName }
  }
}

export type SearchScopeKind = CatalogName | 'inventory' | 'users'

/**
 * Scope tags restrict which entity kinds are searched. `ip:` and `mac:` are controller
 * identifiers, so they scope to controllers and their value is the search term.
 */
const SCOPE_KEYS: Record<string, SearchScopeKind> = {
  station: 'machines',
  machine: 'machines',
  host: 'controllers',
  ip: 'controllers',
  mac: 'controllers',
  ticket: 'tickets',
  part: 'inventory',
  user: 'users',
  person: 'users'
}

/** Filter keys the inventory search understands (alias -> backend key). */
const INVENTORY_FILTER_KEYS: Record<string, string> = {
  name: 'name',
  displayname: 'displayname',
  manufacturer: 'manufacturer',
  mfr: 'manufacturer',
  team: 'team',
  type: 'type',
  status: 'status',
  isstock: 'isstock',
  tech: 'tech',
  technology: 'technology',
  location: 'location',
  shelf: 'shelf',
  serial: 'serial',
  serialnumber: 'serialnumber'
}

export interface SearchScope {
  /** Entity kinds to search; null means no scope tag is active (search everything). */
  kinds: Set<SearchScopeKind> | null
  /** Search terms carried by tags: scope values (except the catch-all `all`) and values of tags no source can filter on. */
  terms: string[]
  /** Tags the inventory search applies as filters. */
  filterTags: TagPill[]
}

export function resolveSearchScope(tags: TagPill[]): SearchScope {
  const kinds = new Set<SearchScopeKind>()
  const terms: string[] = []
  const filterTags: TagPill[] = []
  for (const tag of tags) {
    const key = tag.key.toLowerCase()
    const kind = SCOPE_KEYS[key]
    if (kind) {
      kinds.add(kind)
      if (tag.value && tag.value.toLowerCase() !== 'all') terms.push(tag.value)
    } else if (INVENTORY_FILTER_KEYS[key]) {
      filterTags.push(tag)
    } else if (tag.value) {
      // A tag nothing can filter on (`spec:15kW`) must still narrow the search: its value becomes a term.
      // It used to be sent to the inventory search, which ignored it and returned unfiltered rows.
      terms.push(tag.value)
    }
  }
  return { kinds: kinds.size > 0 ? kinds : null, terms, filterTags }
}

/** `key:"value"` filter expression for the inventory search, with aliases (`mfr`) resolved. */
export function inventoryFilterExpression(filterTags: TagPill[]): string {
  return filterTags
    .map(t => `${INVENTORY_FILTER_KEYS[t.key.toLowerCase()] || t.key}:"${t.value}"`)
    .join(' ')
}

export type ServerSearchKind = 'stations' | 'controllers' | 'tickets' | 'users' | 'inventory'

export const SERVER_SEARCH_URL = '/api/search'

export type ServerSearchResult = Record<ServerSearchKind, SearchResultItem[]>

export function userToSearchItem(raw: any): SearchResultItem {
  const name = raw.name || raw.email || 'User'
  return {
    id: String(raw.id ?? ''),
    name,
    displayName: raw.email && raw.email !== name ? raw.email : undefined,
    itemType: 'User',
    typeLabel: raw.role || 'User',
    subtitle: raw.email || undefined,
    sourceTable: 'users',
    link: buildEntityLink('user', raw.id),
    metadata: { email: raw.email, role: raw.role }
  }
}

const SERVER_MAPPERS: Record<ServerSearchKind, (raw: any) => SearchResultItem> = {
  stations: raw => resolveSearchTemplate('machine').extractSearchItem(raw),
  controllers: raw => resolveSearchTemplate('endpoint').extractSearchItem(raw),
  tickets: ticketToSearchItem,
  users: userToSearchItem,
  inventory: raw => resolveSearchTemplate('inventory').extractSearchItem(raw)
}

/**
 * Runs the search on the server and maps each hit through the same templates the catalogue
 * ranking uses, so both paths render identically. Rows arrive ordered by relevance. Throws when
 * the endpoint is unreachable or answers with something else than a search result, which lets
 * the caller fall back to the catalogues.
 */
export async function searchServer(
  term: string,
  kinds: ServerSearchKind[],
  limit: number,
  fetcher: Fetcher = defaultFetcher
): Promise<ServerSearchResult> {
  const params = new URLSearchParams({ q: term, kinds: kinds.join(','), limit: String(limit) })
  const res = await fetcher(`${SERVER_SEARCH_URL}?${params.toString()}`)
  if (!res || typeof res !== 'object' || Array.isArray(res) || !Array.isArray(res.kinds)) {
    throw new Error('Unexpected search response')
  }
  const result = { stations: [], controllers: [], tickets: [], users: [], inventory: [] } as ServerSearchResult
  for (const kind of kinds) {
    const rows = Array.isArray(res[kind]) ? res[kind] : []
    result[kind] = rows.map(SERVER_MAPPERS[kind])
  }
  return result
}
