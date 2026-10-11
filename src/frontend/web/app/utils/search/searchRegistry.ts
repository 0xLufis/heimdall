import type { SearchSource, SearchContext } from './searchContext'
import {
  indexTable,
  type SearchIndexTable
} from './searchIndexer'
import {
  defaultMachinesSource,
  defaultClientPcsSource,
  defaultTicketsSource,
  defaultInventorySource,
  defaultUsersSource,
  defaultTemplatesSource,
  defaultTelemetrySource,
  SAMPLE_MACHINES_DATA,
  SAMPLE_CLIENT_PCS_DATA,
  SAMPLE_TICKETS_DATA,
  SAMPLE_INVENTORY_DATA,
  SAMPLE_USERS_DATA
} from './defaultContexts'

export interface SearchProvider<T = any> {
  id: string
  name: string
  entityType: string // 'Machine' | 'InventoryItem' | 'ClientPc' | 'MaintenanceTicket' | 'User' | 'AssetTemplate'
  indicatorLabel: string // 'MCH' | 'PRT' | 'IPC' | 'TCK' | 'USR' | 'TMPL'
  indicatorColor: string // Tailwind classes e.g. 'bg-indigo-500/10 text-indigo-600 border-indigo-500/20'
  indexedKey: string
  columns: string[]
  tagKeys: string[]
  data: T[] | (() => T[] | Promise<T[]>)
  titleField?: string
  subtitleField?: string
  getTitle?: (item: T) => string
  getSubtitle?: (item: T) => string
  getLink?: (item: T) => string
  extractMetadata?: (item: T) => Record<string, string | string[]>
}

export interface TagMetadataIndicator {
  label: string
  color: string
  entityType: string
}

export interface TagMetadataItem {
  value: string
  tagKey: string
  entityTypes: string[]
  indicatorLabels: string[]
  indicators: TagMetadataIndicator[]
  count: number
  description?: string
}

export class SearchRegistry {
  private providers = new Map<string, SearchProvider>()
  private cachedTables = new Map<string, SearchIndexTable>()

  constructor() {
    this.registerDefaults()
  }

  public registerProvider<T>(provider: SearchProvider<T>): void {
    this.providers.set(provider.id, provider)
    this.cachedTables.delete(provider.id)
  }

  public unregisterProvider(id: string): boolean {
    this.cachedTables.delete(id)
    return this.providers.delete(id)
  }

  public getProvider<T = any>(id: string): SearchProvider<T> | undefined {
    return this.providers.get(id)
  }

  public getAllProviders(): SearchProvider[] {
    return Array.from(this.providers.values())
  }

  public getProvidersForContext(contextId?: string): SearchProvider[] {
    if (!contextId || contextId === 'global' || contextId === 'global-plant') {
      return this.getAllProviders()
    }

    if (contextId === 'machines' || contextId === 'stations') {
      const p = this.providers.get('machines')
      return p ? [p] : []
    }

    if (contextId === 'clients' || contextId === 'nodes' || contextId === 'client_pcs') {
      const p = this.providers.get('client_pcs')
      return p ? [p] : []
    }

    if (contextId === 'inventory' || contextId === 'inventory-workspace') {
      return [
        this.providers.get('inventory'),
        this.providers.get('templates'),
        this.providers.get('machines')
      ].filter(Boolean) as SearchProvider[]
    }

    if (contextId === 'tickets' || contextId === 'ticketing' || contextId === 'ticketing-workspace') {
      return [
        this.providers.get('tickets'),
        this.providers.get('machines'),
        this.providers.get('users')
      ].filter(Boolean) as SearchProvider[]
    }

    if (contextId === 'telemetry' || contextId === 'telemetry-hub' || contextId === 'telemetry-workspace') {
      const p = this.providers.get('telemetry')
      return p ? [p] : []
    }

    return this.getAllProviders()
  }

  /**
   * Builds or returns cached SearchIndexTable for the specified providers.
   */
  public getIndexTables(providerIds?: string[]): SearchIndexTable[] {
    const targetProviders = providerIds && providerIds.length > 0
      ? providerIds.map(id => this.providers.get(id)).filter(Boolean) as SearchProvider[]
      : this.getAllProviders()

    return targetProviders.map(p => {
      let tbl = this.cachedTables.get(p.id)
      if (!tbl) {
        const source = this.toSearchSource(p)
        tbl = indexTable(source)
        this.cachedTables.set(p.id, tbl)
      }
      return tbl
    })
  }

  /**
   * Scopes and harvests tag metadata values across active providers for a given tag key.
   * Returns metadata items complete with entity type indicators ([MCH], [PRT], [IPC], [TCK], etc.).
   */
  public harvestTagMetadata(tagKey: string, providerIds?: string[]): TagMetadataItem[] {
    const normKey = tagKey.toLowerCase().trim()
    const targetProviders = providerIds && providerIds.length > 0
      ? providerIds.map(id => this.providers.get(id)).filter(Boolean) as SearchProvider[]
      : this.getAllProviders()

    // Map: valueLower -> TagMetadataItem
    const resultMap = new Map<string, TagMetadataItem>()

    // Tag aliases mapping
    const resolveTagKeyAliases = (key: string): string[] => {
      if (key === 'mfr' || key === 'manufacturer') return ['mfr', 'manufacturer']
      if (key === 'ip' || key === 'ipaddress') return ['ip', 'ipaddress']
      if (key === 'mac' || key === 'macaddress') return ['mac', 'macaddress']
      if (key === 'serial' || key === 'serialnumber') return ['serial', 'serialnumber']
      if (key === 'ticket' || key === 'ticketnumber') return ['ticket', 'ticketnumber']
      if (key === 'host' || key === 'hostname') return ['host', 'hostname']
      if (key === 'os' || key === 'osversion') return ['os', 'osversion']
      if (key === 'tech' || key === 'category') return ['tech', 'category']
      if (key === 'loc' || key === 'location') return ['loc', 'location']
      if (key === 'dept' || key === 'department') return ['dept', 'department']
      return [key]
    }

    const searchKeys = resolveTagKeyAliases(normKey)

    for (const provider of targetProviders) {
      const data = typeof provider.data === 'function' ? (provider.data as any)() : provider.data
      const records = Array.isArray(data) ? data : []

      for (const record of records) {
        // Collect candidate values from record
        const candidateValues: string[] = []

        if (normKey === 'type' || normKey === 'entity') {
          candidateValues.push(provider.entityType)
          candidateValues.push(provider.indicatorLabel)
        } else {
          for (const k of searchKeys) {
            const rawVal = record[k] ?? record[k.toLowerCase()]
            if (rawVal !== undefined && rawVal !== null) {
              if (Array.isArray(rawVal)) {
                for (const v of rawVal) {
                  const s = String(v).trim()
                  if (s) candidateValues.push(s)
                }
              } else {
                const s = String(rawVal).trim()
                if (s) candidateValues.push(s)
              }
            }
          }
        }

        // Aggregate into resultMap
        for (const val of candidateValues) {
          const valLower = val.toLowerCase()
          if (!resultMap.has(valLower)) {
            resultMap.set(valLower, {
              value: val,
              tagKey: normKey,
              entityTypes: [provider.entityType],
              indicatorLabels: [provider.indicatorLabel],
              indicators: [{
                label: provider.indicatorLabel,
                color: provider.indicatorColor,
                entityType: provider.entityType
              }],
              count: 1
            })
          } else {
            const item = resultMap.get(valLower)!
            item.count++
            if (!item.entityTypes.includes(provider.entityType)) {
              item.entityTypes.push(provider.entityType)
            }
            if (!item.indicatorLabels.includes(provider.indicatorLabel)) {
              item.indicatorLabels.push(provider.indicatorLabel)
              item.indicators.push({
                label: provider.indicatorLabel,
                color: provider.indicatorColor,
                entityType: provider.entityType
              })
            }
          }
        }
      }
    }

    return Array.from(resultMap.values()).sort((a, b) => b.count - a.count || a.value.localeCompare(b.value))
  }

  /**
   * Converts a SearchProvider to a SearchSource for SearchContext compatibility.
   */
  public toSearchSource<T>(provider: SearchProvider<T>): SearchSource<T> {
    return {
      id: provider.id,
      name: provider.name,
      entityType: provider.entityType,
      indicatorLabel: provider.indicatorLabel,
      indicatorColor: provider.indicatorColor,
      data: provider.data,
      indexedKey: provider.indexedKey,
      columns: provider.columns,
      titleField: provider.titleField,
      subtitleField: provider.subtitleField,
      linkGenerator: provider.getLink || ((item: any) => `/dashboard?selected=${item[provider.indexedKey]}`)
    }
  }

  /**
   * Creates a SearchContext from registered providers.
   */
  public toSearchContext(id: string, title: string, providerIds?: string[]): SearchContext {
    const providers = providerIds && providerIds.length > 0
      ? providerIds.map(pid => this.providers.get(pid)).filter(Boolean) as SearchProvider[]
      : this.getAllProviders()

    const sources = providers.map(p => this.toSearchSource(p))
    const allowedTags = new Set<string>()
    for (const p of providers) {
      for (const t of p.tagKeys) allowedTags.add(t.toLowerCase())
    }

    return {
      id,
      title,
      sources,
      allowedTags: Array.from(allowedTags)
    }
  }

  private registerDefaults(): void {
    // 1. Machines / Stations Provider
    this.registerProvider({
      id: 'machines',
      name: 'Production Machines',
      entityType: 'Machine',
      indicatorLabel: 'MCH',
      indicatorColor: 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border-indigo-500/20',
      indexedKey: 'id',
      columns: ['name', 'manufacturer', 'status', 'location', 'tech', 'line'],
      tagKeys: ['name', 'manufacturer', 'status', 'location', 'tech', 'line', 'type', 'entity'],
      data: SAMPLE_MACHINES_DATA,
      getLink: (item: any) => `/dashboard/machines?id=${item.id}&search=${encodeURIComponent(item.name)}`
    })

    // 2. Client PCs / IPC Controllers Provider
    this.registerProvider({
      id: 'client_pcs',
      name: 'Client IPC Controllers',
      entityType: 'ClientPc',
      indicatorLabel: 'IPC',
      indicatorColor: 'bg-cyan-500/10 text-cyan-600 dark:text-cyan-400 border-cyan-500/20',
      indexedKey: 'id',
      columns: ['hostname', 'ipAddress', 'osVersion', 'status', 'location'],
      tagKeys: ['hostname', 'ip', 'os', 'status', 'location', 'type', 'entity'],
      titleField: 'hostname',
      subtitleField: 'ipAddress',
      data: SAMPLE_CLIENT_PCS_DATA,
      getLink: (item: any) => `/dashboard/clients?selected=${item.id}&hostname=${encodeURIComponent(item.hostname)}`
    })

    // 3. Maintenance Tickets Provider
    this.registerProvider({
      id: 'tickets',
      name: 'Maintenance Tickets',
      entityType: 'MaintenanceTicket',
      indicatorLabel: 'TCK',
      indicatorColor: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
      indexedKey: 'id',
      columns: ['ticketNumber', 'title', 'status', 'priority', 'department'],
      tagKeys: ['ticket', 'title', 'status', 'priority', 'department', 'type', 'entity'],
      titleField: 'title',
      subtitleField: 'ticketNumber',
      data: SAMPLE_TICKETS_DATA,
      getLink: (item: any) => `/dashboard/tickets?ticketId=${item.id}`
    })

    // 4. Parts & Inventory Provider
    this.registerProvider({
      id: 'inventory',
      name: 'Parts & Components',
      entityType: 'InventoryItem',
      indicatorLabel: 'PRT',
      indicatorColor: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 border-emerald-500/20',
      indexedKey: 'id',
      columns: ['name', 'category', 'manufacturer', 'location', 'status', 'serialNumber'],
      tagKeys: ['name', 'category', 'manufacturer', 'location', 'status', 'serial', 'type', 'entity'],
      data: SAMPLE_INVENTORY_DATA,
      getLink: (item: any) => `/dashboard/inventory/${item.id}`
    })

    // 5. Staff & Personnel Provider
    this.registerProvider({
      id: 'users',
      name: 'Staff & Personnel',
      entityType: 'User',
      indicatorLabel: 'USR',
      indicatorColor: 'bg-purple-500/10 text-purple-600 dark:text-purple-400 border-purple-500/20',
      indexedKey: 'id',
      columns: ['name', 'email', 'role', 'department'],
      tagKeys: ['name', 'email', 'role', 'department', 'type', 'entity'],
      data: SAMPLE_USERS_DATA,
      getLink: (item: any) => `/dashboard/users/${item.id}`
    })

    // 6. Asset Blueprints & Templates Provider
    this.registerProvider({
      id: 'templates',
      name: 'Asset Blueprints',
      entityType: 'AssetTemplate',
      indicatorLabel: 'TMPL',
      indicatorColor: 'bg-blue-500/10 text-blue-600 dark:text-blue-400 border-blue-500/20',
      indexedKey: 'id',
      columns: ['name', 'manufacturer', 'topLevelCategory', 'identifierPattern', 'tag'],
      tagKeys: ['name', 'manufacturer', 'toplevelcategory', 'tag', 'type', 'entity'],
      data: defaultTemplatesSource.data,
      getLink: (item: any) => `/dashboard/inventory?section=templates&templateId=${item.id}`
    })

    // 7. Telemetry Metrics & Data Points Provider
    this.registerProvider({
      id: 'telemetry',
      name: 'Telemetry Metrics & Data Points',
      entityType: 'TelemetryMetric',
      indicatorLabel: 'TEL',
      indicatorColor: 'bg-amber-500/10 text-amber-600 dark:text-amber-400 border-amber-500/20',
      indexedKey: 'id',
      columns: ['name', 'category', 'dataType', 'description', 'unit'],
      tagKeys: ['name', 'category', 'datatype', 'unit', 'type', 'entity', 'metric', 'quality'],
      titleField: 'name',
      subtitleField: 'category',
      data: defaultTelemetrySource.data,
      getLink: (item: any) => `/dashboard/telemetry?tab=live&metric=${item.id}`
    })
  }
}

export const searchRegistry = new SearchRegistry()
