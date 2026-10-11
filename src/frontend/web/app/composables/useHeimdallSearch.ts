import { ref, computed, watch, unref, type Ref } from 'vue'
import { watchDebounced } from '@vueuse/core'
import type { 
  TagPill, 
  AutoTagResult, 
  SearchInstanceConfig, 
  SearchResultItem, 
  SearchGroup,
  KeyLookupSuggestion,
  HeimdallSearchOptions,
  RankedIndexingTableRef
} from '~/types/search'
import { autoTagEngine } from '~/utils/search/AutoTagEngine'
import { 
  resolveSearchTemplate, 
  type SearchTemplateDef 
} from '~/utils/search/searchTemplates'
import {
  BASELINE_KNOWN_KEY_VALUES,
  ALL_FILTER_KEYS,
  harvestDynamicKeyValues,
  getSuggestionsForKey,
  buildDynamicSearchKeyGroups,
  type RankedIndexingTable
} from '~/utils/search/indexingTables'
import { rankByFuzzyScore, isFuzzyMatch } from '~/utils/search/fuzzy'
import { 
  SearchDiagnosticsService, 
  defaultSearchDiagnostics,
  type SearchDiagnosticsRecord 
} from '~/utils/search/searchDiagnostics'
import { searchRegistry } from '~/utils/search/searchRegistry'
import {
  SAMPLE_MACHINES_DATA,
  SAMPLE_CLIENT_PCS_DATA,
  SAMPLE_TICKETS_DATA,
  SAMPLE_INVENTORY_DATA,
  SAMPLE_USERS_DATA,
  defaultTelemetrySource
} from '~/utils/search/defaultContexts'

/**
 * Heimdall Search composable - tag-aware fuzzy search across all Heimdall entities
 * Universal high-throughput multi-tier fuzzy indexing engine for industrial assets,
 * client PCs, telemetry, and enterprise field inventory.
 */
const DEFAULT_CONFIG: SearchInstanceConfig = {
  instanceId: 'dashboard',
  placeholder: 'Search stations, controllers, inventory, specs... (Cmd+K)',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  allowedTagKeys: ['line', 'tech', 'status', 'mfr', 'manufacturer', 'serial', 'location', 'category', 'station', 'type', 'isstock', 'ip', 'mac', 'spec', 'cost', 'role', 'quality', 'unit'],
  minCharsForSuggestions: 2,
  debounceMs: 150,
  enableAutoTagging: true,
  showGlobalShortcut: true,
  enableDiagnostics: true
}

export const useHeimdallSearch = (
  optionsOrConfig: HeimdallSearchOptions | Partial<SearchInstanceConfig> = {}
) => {
  // Normalize options whether caller passed HeimdallSearchOptions or SearchInstanceConfig
  const isParameterized = 'dataSource' in optionsOrConfig || 'data' in optionsOrConfig || 'indexingTables' in optionsOrConfig || 'template' in optionsOrConfig
  const topConfig = optionsOrConfig.config || {}
  
  const config: SearchInstanceConfig = {
    ...DEFAULT_CONFIG,
    ...topConfig,
    instanceId: (optionsOrConfig as any).instanceId || topConfig.instanceId || DEFAULT_CONFIG.instanceId,
    placeholder: (optionsOrConfig as any).placeholder || topConfig.placeholder || DEFAULT_CONFIG.placeholder,
    defaultEndpoints: (optionsOrConfig as any).defaultEndpoints || DEFAULT_CONFIG.defaultEndpoints,
    allowedTagKeys: (optionsOrConfig as any).allowedTagKeys || topConfig.allowedTagKeys || DEFAULT_CONFIG.allowedTagKeys,
    defaultTags: (optionsOrConfig as any).defaultTags || topConfig.defaultTags,
    minCharsForSuggestions: (optionsOrConfig as any).minCharsForSuggestions ?? topConfig.minCharsForSuggestions ?? DEFAULT_CONFIG.minCharsForSuggestions,
    debounceMs: (optionsOrConfig as any).debounceMs ?? topConfig.debounceMs ?? DEFAULT_CONFIG.debounceMs,
    enableAutoTagging: (optionsOrConfig as any).enableAutoTagging ?? topConfig.enableAutoTagging ?? DEFAULT_CONFIG.enableAutoTagging,
    showGlobalShortcut: (optionsOrConfig as any).showGlobalShortcut ?? DEFAULT_CONFIG.showGlobalShortcut,
    enableDiagnostics: (optionsOrConfig as any).enableDiagnostics ?? topConfig.enableDiagnostics ?? true
  }

  // Active Template
  const templateInput = (optionsOrConfig as HeimdallSearchOptions).template || config.instanceId
  const activeTemplate = computed<SearchTemplateDef>(() => resolveSearchTemplate(templateInput))

  // Ingested Data and Data Source
  const rawDataSource = (optionsOrConfig as HeimdallSearchOptions).dataSource
  const rawDataRef = (optionsOrConfig as HeimdallSearchOptions).data

  // Indexing Tables
  const indexingTablesRef = ref<RankedIndexingTable[]>(
    ((optionsOrConfig as HeimdallSearchOptions).indexingTables as RankedIndexingTable[]) || []
  )

  // Dynamically generated known KVs harvested from ranked tables & baseline seed
  const dynamicKnownKeyValues = computed(() => {
    return harvestDynamicKeyValues(indexingTablesRef.value, BASELINE_KNOWN_KEY_VALUES)
  })

  // Fuzzy configuration
  const fuzzyConfig = computed(() => ({
    enabled: topConfig.fuzzy?.enabled ?? true,
    threshold: topConfig.fuzzy?.threshold ?? 0.7,
    maxDistance: topConfig.fuzzy?.maxDistance ?? 2
  }))

  // GDPR-compliant diagnostics collector
  const diagnosticsService = new SearchDiagnosticsService({
    enabled: config.enableDiagnostics
  })
  const diagnostics = ref<SearchDiagnosticsRecord[]>([])

  // Search state
  const rawInput = ref('')
  const tags = ref<TagPill[]>(config.defaultTags ? [...config.defaultTags.map(t => ({ id: `tag-${t.key}-${t.value}`, ...t, removable: true }))] : [])
  const autoSuggestions = ref<AutoTagResult[]>([])
  const results = ref<SearchResultItem[]>([])
  const primaryResults = ref<SearchResultItem[]>([])
  const crossTableResults = ref<SearchResultItem[]>([])
  const matchingKeys = ref<typeof ALL_FILTER_KEYS>([])
  const valueSuggestions = ref<KeyLookupSuggestion[]>([])
  const activePendingKey = ref<string>('')
  const activePendingModifier = ref<string>('')
  const activePendingSeparator = ref<string>(':')
  const activePendingCompletedValues = ref<string[]>([])
  const searchKeyGroups = ref<SearchGroup[]>(buildDynamicSearchKeyGroups(dynamicKnownKeyValues.value))
  const isLoading = ref(false)
  const isDropdownOpen = ref(false)

  // Current free-text typing extracted after tag parsing
  const freeText = computed(() => {
    return rawInput.value
  })

  // Full effective query formatted for API
  const effectiveQueryString = computed(() => {
    const tagPart = tags.value.map(t => `${t.key}:"${t.value}"`).join(' ')
    const textPart = rawInput.value.trim()
    return `${tagPart} ${textPart}`.trim()
  })

  // Fetch available search keys for autocomplete
  const fetchSearchKeys = async () => {
    try {
      const res = await $fetch<any[]>('/api/proxy/inventory/keys')
      if (res && Array.isArray(res)) {
        searchKeyGroups.value = res.map(g => ({
          group: g.group || g.Group || 'Attributes',
          keys: g.keys || g.Keys || []
        }))
        return
      }
    } catch {
      // Fall back to dynamically generated search key groups
      searchKeyGroups.value = buildDynamicSearchKeyGroups(dynamicKnownKeyValues.value)
    }
  }

  // Re-build index from indexing tables or data
  const rebuildIndex = () => {
    searchKeyGroups.value = buildDynamicSearchKeyGroups(dynamicKnownKeyValues.value)
  }

  // Filter items matching active tags
  const matchItemWithTags = (item: any, tagFilters: TagPill[], template: SearchTemplateDef): boolean => {
    if (!tagFilters.length) return true
    const kv = template.extractKvFields ? template.extractKvFields(item) : {}

    for (const tag of tagFilters) {
      const k = tag.key.toLowerCase()
      const targetVal = tag.value.toLowerCase()

      // 1. Scoping tags with value 'all' (e.g. @machine, #ticket, !controller, *part)
      if (targetVal === 'all') {
        if (k === 'station' || k === 'machine') {
          if (item.itemType === 'Machine' || template.name === 'machine' || item.sourceTable === 'machines') continue
          return false
        }
        if (k === 'ticket' || k === 'tickets') {
          if (item.itemType === 'Ticket' || item.itemType === 'MaintenanceTicket' || template.name === 'ticket' || item.sourceTable === 'tickets') continue
          return false
        }
        if (k === 'host' || k === 'controller' || k === 'client' || k === 'ipc') {
          if (item.itemType === 'ClientPc' || item.itemType === 'endpoint' || template.name === 'endpoint' || item.sourceTable === 'nodes' || item.sourceTable === 'client_pcs') continue
          return false
        }
        if (k === 'part' || k === 'inventory' || k === 'stock') {
          if (item.itemType === 'InventoryItem' || item.sourceTable === 'inventory' || template.name === 'inventory') continue
          return false
        }
        if (k === 'metric' || k === 'telemetry') {
          if (item.itemType === 'Telemetry' || item.itemType === 'TelemetryMetric' || template.name === 'telemetry' || item.sourceTable === 'telemetry') continue
          return false
        }
      }

      // 2. Entity / Type tag (e.g. type:machine, type:ticket, type:clientpc, type:inventory, type:telemetry)
      if (k === 'type' || k === 'entity') {
        const itemTypeLower = (item.itemType || template.name || '').toLowerCase()
        const typeLabelLower = (item.typeLabel || '').toLowerCase()
        const sourceTableLower = (item.sourceTable || '').toLowerCase()
        const matchesType = itemTypeLower.includes(targetVal) || 
                            typeLabelLower.includes(targetVal) || 
                            sourceTableLower.includes(targetVal) ||
                            (targetVal === 'machine' && itemTypeLower === 'machine') ||
                            (targetVal === 'ticket' && (itemTypeLower.includes('ticket') || sourceTableLower === 'tickets')) ||
                            ((targetVal === 'client' || targetVal === 'controller' || targetVal === 'ipc') && itemTypeLower.includes('client')) ||
                            ((targetVal === 'part' || targetVal === 'inventory') && (itemTypeLower.includes('inventory') || sourceTableLower === 'inventory')) ||
                            ((targetVal === 'metric' || targetVal === 'telemetry') && (itemTypeLower.includes('telemetry') || sourceTableLower === 'telemetry'))

        if (matchesType) continue
        return false
      }

      // 3. Status filter (e.g. %alarm or status:maintenance)
      if (k === 'status' && (targetVal === 'maintenance' || targetVal === 'alarm')) {
        const statusVal = String(kv.status ?? item.status ?? item.equipmentStatus ?? '').toLowerCase()
        const priorityVal = String(item.priority ?? '').toLowerCase()
        const isAlarm = statusVal === 'maintenance' || statusVal === 'warning' || statusVal === 'alarm' || 
                        statusVal === 'offline' || priorityVal === 'critical' || priorityVal === 'high'
        if (isAlarm) continue
        return false
      }

      // 4. Normal tag filtering with alias resolution
      const resolveAliases = (key: string): string[] => {
        if (key === 'mfr' || key === 'manufacturer') return ['mfr', 'manufacturer']
        if (key === 'ip' || key === 'ipaddress') return ['ip', 'ipAddress', 'ipaddress']
        if (key === 'mac' || key === 'macaddress') return ['mac', 'macAddress', 'macaddress']
        if (key === 'serial' || key === 'serialnumber') return ['serial', 'serialNumber', 'serialnumber']
        if (key === 'ticket' || key === 'ticketnumber') return ['ticket', 'ticketNumber', 'ticketnumber']
        if (key === 'host' || key === 'hostname') return ['host', 'hostname']
        if (key === 'metric' || key === 'symbol') return ['metric', 'name', 'canonicalKey', 'id']
        if (key === 'category' || key === 'cat') return ['category', 'typeLabel']
        if (key === 'unit') return ['unit']
        return [key]
      }

      let matched = false
      for (const field of resolveAliases(k)) {
        const itemVal = kv[field] ?? item[field] ?? item[field.toLowerCase()]
        if (itemVal !== undefined && itemVal !== null) {
          if (Array.isArray(itemVal)) {
            if (itemVal.some(v => String(v).toLowerCase() === targetVal)) {
              matched = true
              break
            }
          } else if (String(itemVal).toLowerCase() === targetVal) {
            matched = true
            break
          }
        }
      }

      if (!matched) return false
    }
    return true
  }

  // Filter and score item with query text
  const matchItemWithText = (
    item: any, 
    cleanQ: string, 
    template: SearchTemplateDef, 
    useFuzzy: boolean, 
    fuzzyThreshold: number
  ): { matches: boolean; score: number } => {
    if (!cleanQ) return { matches: true, score: 1.0 }

    const fields = template.searchableFields.map(f => {
      const val = item[f]
      return val != null ? String(val) : ''
    }).filter(Boolean)

    // Exact or substring match
    for (const f of fields) {
      const lower = f.toLowerCase()
      if (lower === cleanQ) return { matches: true, score: 1.0 }
      if (lower.startsWith(cleanQ)) return { matches: true, score: 0.95 }
      if (lower.includes(cleanQ)) return { matches: true, score: 0.85 }
    }

    // Fuzzy match if enabled
    if (useFuzzy && cleanQ.length >= 3) {
      for (const f of fields) {
        if (isFuzzyMatch(cleanQ, f, fuzzyThreshold)) {
          return { matches: true, score: 0.75 }
        }
      }
    }

    return { matches: false, score: 0.0 }
  }

  const localQueryCache = new Map<string, { primary: SearchResultItem[]; cross: SearchResultItem[]; timestamp: number }>()

  // Execute multi-tier search across instance primary and cross-table indexing
  const executeSearch = async (queryStr: string = effectiveQueryString.value) => {
    const startTime = typeof performance !== 'undefined' ? performance.now() : Date.now()
    let resolvedDataSourceType: 'uri' | 'dbConnection' | 'redisCache' | 'custom' | 'inMemory' = 'inMemory'

    if (!queryStr && tags.value.length === 0) {
      if (config.instanceId === 'telemetry' || config.instanceId === 'telemetry-workspace' || config.instanceId === 'telemetry-hub') {
        // Allow telemetry workspace to return baseline items
      } else {
        results.value = []
        primaryResults.value = []
        crossTableResults.value = []
        return
      }
    }

    const cacheKey = `${config.instanceId}:${queryStr}:${tags.value.map(t => `${t.key}=${t.value}`).join(';')}`
    const cached = localQueryCache.get(cacheKey)
    if (cached && (Date.now() - cached.timestamp) < 60000) {
      primaryResults.value = cached.primary
      crossTableResults.value = cached.cross
      results.value = [...cached.primary, ...cached.cross]
    } else {
      isLoading.value = true
    }

    try {
      let cleanQ = (rawInput.value || '').toLowerCase().trim()
      if (!cleanQ && queryStr) {
        cleanQ = queryStr.replace(/(?:\w+):(?:"[^"]+"|\S+)/g, '').replace(/[@#!*%](?:"[^"]+"|\S+)/g, '').toLowerCase().trim()
      }
      let primary: SearchResultItem[] = []
      let cross: SearchResultItem[] = []

      // -----------------------------------------------------------------
      // PATH A: Direct in-memory data or custom dataSource handler provided
      // -----------------------------------------------------------------
      const unrefData = rawDataRef ? unref(rawDataRef) : undefined
      const isDataProvided = unrefData !== undefined

      if (isDataProvided || (rawDataSource && typeof rawDataSource === 'object' && rawDataSource.type === 'custom')) {
        let candidateItems: any[] = []

        if (rawDataSource && typeof rawDataSource === 'object' && rawDataSource.type === 'custom') {
          resolvedDataSourceType = 'custom'
          const customRes = await rawDataSource.handler(queryStr, { tags: tags.value, freeText: cleanQ })
          candidateItems = Array.isArray(customRes) ? customRes : (customRes?.items || [customRes])
        } else if (Array.isArray(unrefData)) {
          candidateItems = unrefData
        } else if (unrefData && typeof unrefData === 'object') {
          candidateItems = Array.isArray((unrefData as any).items) ? (unrefData as any).items : [unrefData]
        }

        const currentTemplate = activeTemplate.value
        const scoredItems: Array<{ item: SearchResultItem; score: number }> = []

        for (const raw of candidateItems) {
          if (!matchItemWithTags(raw, tags.value, currentTemplate)) continue
          const { matches, score } = matchItemWithText(
            raw, 
            cleanQ, 
            currentTemplate, 
            fuzzyConfig.value.enabled, 
            fuzzyConfig.value.threshold
          )
          if (matches) {
            scoredItems.push({
              item: currentTemplate.extractSearchItem(raw),
              score
            })
          }
        }

        scoredItems.sort((a, b) => b.score - a.score)
        primary = scoredItems.map(s => s.item)

        // Query secondary indexing tables for cross-table results if configured
        if (indexingTablesRef.value.length > 0) {
          const secondaryTables = indexingTablesRef.value.filter(t => t.rank > 1)
          for (const tbl of secondaryTables) {
            const tblData = typeof tbl.data === 'function' ? tbl.data() : unref(tbl.data)
            if (Array.isArray(tblData) && cleanQ.length >= 2) {
              const matched = tblData.filter(rawItem => {
                const searchable = tbl.searchFields || ['name', 'hostname', 'customIdentifier']
                return searchable.some(field => {
                  const val = String(rawItem[field] || '').toLowerCase()
                  return val.includes(cleanQ) || (fuzzyConfig.value.enabled && isFuzzyMatch(cleanQ, val, 0.75))
                })
              }).slice(0, 5)

              for (const m of matched) {
                cross.push({
                  id: m.id || `${tbl.name}-${Math.random()}`,
                  name: m.name || m.hostname || m.customIdentifier || 'Cross Item',
                  displayName: m.displayName,
                  itemType: tbl.name,
                  typeLabel: tbl.name,
                  sourceTable: tbl.name,
                  isCrossTable: true,
                  subtitle: tbl.description
                })
              }
            }
          }
        }
      } else {
        // -----------------------------------------------------------------
        // PATH B: Endpoint-based or instance-based multi-tier search
        // -----------------------------------------------------------------
        const isMachinesInstance = config.instanceId === 'machines'
        const isNodesInstance = config.instanceId === 'nodes' || config.instanceId === 'clients'

        if (isMachinesInstance) {
          resolvedDataSourceType = 'uri'
          // Stage 1 Primary: Machines / Stations
          try {
            const machines = await $fetch<any[]>('/api/proxy/v1/machine')
            if (Array.isArray(machines)) {
              const filtered = machines.filter(m => {
                if (!cleanQ) return true
                const nameMatch = (m.name || '').toLowerCase().includes(cleanQ)
                const dispMatch = (m.displayName || '').toLowerCase().includes(cleanQ)
                const identMatch = (m.customIdentifier || '').toLowerCase().includes(cleanQ)
                const typeMatch = (m.machineType || '').toLowerCase().includes(cleanQ)
                const groupMatch = (m.groupId || '').toLowerCase().includes(cleanQ)
                const fuzzyMatch = fuzzyConfig.value.enabled && cleanQ.length >= 3 && (
                  isFuzzyMatch(cleanQ, m.name || '', fuzzyConfig.value.threshold) ||
                  isFuzzyMatch(cleanQ, m.displayName || '', fuzzyConfig.value.threshold)
                )
                return nameMatch || dispMatch || identMatch || typeMatch || groupMatch || fuzzyMatch
              })
              primary = filtered.map(m => resolveSearchTemplate('machine').extractSearchItem(m))
            }
          } catch {}

          // Stage 3 Cross-table: Inventory / Parts installed on machines
          try {
            const inv = await $fetch<any>(`/api/inventory/filter?query=${encodeURIComponent(queryStr)}`)
            if (inv && Array.isArray(inv.items)) {
              cross = inv.items.slice(0, 10).map((item: any) => ({
                id: item.id,
                name: item.name || item.customIdentifier || 'Asset',
                displayName: item.displayName,
                itemType: item.itemType || 'Asset',
                typeLabel: item.itemType === 'hardware' ? 'Component' : 'License',
                manufacturerName: item.manufacturer?.name,
                subtitle: item.serialNumber ? `SN: ${item.serialNumber}` : undefined,
                sourceTable: 'inventory',
                isCrossTable: true,
                link: `/dashboard/inventory`
              }))
            }
          } catch {}
        } else if (isNodesInstance) {
          resolvedDataSourceType = 'uri'
          // Stage 1 Primary: Nodes / Client PCs
          try {
            const pcs = await $fetch<any[]>('/api/proxy/v1/ClientPc')
            if (Array.isArray(pcs)) {
              const filtered = pcs.filter(p => {
                if (!cleanQ) return true
                const exact = (p.hostname || '').toLowerCase().includes(cleanQ) ||
                  (p.macAddress || '').toLowerCase().includes(cleanQ) ||
                  (p.ipAddress || '').toLowerCase().includes(cleanQ)
                const fuzzy = fuzzyConfig.value.enabled && cleanQ.length >= 3 && isFuzzyMatch(cleanQ, p.hostname || '', fuzzyConfig.value.threshold)
                return exact || fuzzy
              })
              primary = filtered.map(p => resolveSearchTemplate('endpoint').extractSearchItem(p))
            }
          } catch {}

          // Stage 3 Cross-table: Machines assigned to nodes
          try {
            const machines = await $fetch<any[]>('/api/proxy/v1/machine')
            if (Array.isArray(machines)) {
              cross = machines.filter(m => (m.name || '').toLowerCase().includes(cleanQ)).slice(0, 5).map(m => ({
                id: m.id,
                name: m.name,
                itemType: 'Machine',
                typeLabel: 'Station',
                sourceTable: 'machines',
                isCrossTable: true,
                link: `/dashboard/machines?id=${encodeURIComponent(m.id)}&search=${encodeURIComponent(m.name)}`
              }))
            }
          } catch {}
        } else if (config.instanceId === 'telemetry' || config.instanceId === 'telemetry-workspace' || config.instanceId === 'telemetry-hub') {
          resolvedDataSourceType = 'uri'
          // Stage 1 Primary: Scoped strictly to Telemetry-related search points
          const rawMetrics: any[] = [...defaultTelemetrySource.data]
          try {
            const cat = await $fetch<any[]>('/api/telemetry/catalog')
            if (Array.isArray(cat) && cat.length > 0) {
              const existingIds = new Set(rawMetrics.map(m => m.id))
              for (const c of cat) {
                if (!existingIds.has(c.id || c.key)) {
                  rawMetrics.push({
                    id: c.id || c.key,
                    name: c.name || c.key,
                    category: c.category || 'General',
                    dataType: c.dataType || 'Float64',
                    unit: c.unit || '',
                    description: c.description || c.pathOrSymbol || ''
                  })
                }
              }
            }
          } catch {}

          const metricItems: SearchResultItem[] = rawMetrics
            .filter(m => matchItemWithTags(m, tags.value, resolveSearchTemplate('telemetry')))
            .map(m => resolveSearchTemplate('telemetry').extractSearchItem(m))
            .filter(item => {
              if (!cleanQ) return true
              const nameMatch = (item.name || '').toLowerCase().includes(cleanQ)
              const dispMatch = (item.displayName || '').toLowerCase().includes(cleanQ)
              const subMatch = (item.subtitle || '').toLowerCase().includes(cleanQ)
              const typeMatch = (item.typeLabel || '').toLowerCase().includes(cleanQ)
              const idMatch = (item.id || '').toLowerCase().includes(cleanQ)
              const fuzzyMatch = fuzzyConfig.value.enabled && cleanQ.length >= 3 && (
                isFuzzyMatch(cleanQ, item.name || '', fuzzyConfig.value.threshold) ||
                isFuzzyMatch(cleanQ, item.displayName || '', fuzzyConfig.value.threshold)
              )
              return nameMatch || dispMatch || subMatch || typeMatch || idMatch || fuzzyMatch
            })

          primary = metricItems
          cross = []
        } else {
          // Inventory or Global Search
          resolvedDataSourceType = 'uri'
          const endpoint = typeof rawDataSource === 'string' 
            ? rawDataSource 
            : (rawDataSource?.type === 'uri' ? rawDataSource.url : (config.defaultEndpoints?.[0] || '/api/proxy/inventory/search'))
          const queryParam = encodeURIComponent(queryStr)

          // Helper to check for active scoping tag
          const hasScope = (key: string, val: string = 'all') => {
            return tags.value.some(t => {
              const k = t.key.toLowerCase()
              const v = t.value.toLowerCase()
              if (k === key.toLowerCase() && (val === 'all' || v === val.toLowerCase())) return true
              if (k === 'type' || k === 'entity') {
                if (key === 'station' && (v === 'machine' || v === 'station' || v === 'mch')) return true
                if (key === 'ticket' && (v === 'ticket' || v === 'maintenanceticket' || v === 'tck')) return true
                if (key === 'host' && (v === 'clientpc' || v === 'controller' || v === 'ipc')) return true
                if (key === 'part' && (v === 'inventoryitem' || v === 'part' || v === 'inventory' || v === 'prt')) return true
              }
              return false
            })
          }

          const isStationScoped = hasScope('station', 'all')
          const isTicketScoped = hasScope('ticket', 'all')
          const isControllerScoped = hasScope('host', 'all')
          const isPartScoped = hasScope('part', 'all')
          const isAlarmScoped = hasScope('status', 'maintenance')

          // 1. Machines
          let rawMachines: any[] = []
          try {
            const m = await $fetch<any[]>('/api/proxy/v1/machine')
            if (Array.isArray(m) && m.length > 0) rawMachines = m
          } catch {}
          if (rawMachines.length === 0) rawMachines = SAMPLE_MACHINES_DATA

          const machineItems: SearchResultItem[] = rawMachines
            .filter(m => matchItemWithTags(m, tags.value, resolveSearchTemplate('machine')))
            .map(m => resolveSearchTemplate('machine').extractSearchItem(m))
            .filter(item => !cleanQ || isFuzzyMatch(cleanQ, item.name, 0.7) || (item.displayName && isFuzzyMatch(cleanQ, item.displayName, 0.7)))

          // 2. Client PCs
          let rawPcs: any[] = []
          try {
            const p = await $fetch<any[]>('/api/proxy/v1/ClientPc')
            if (Array.isArray(p) && p.length > 0) rawPcs = p
          } catch {}
          if (rawPcs.length === 0) rawPcs = SAMPLE_CLIENT_PCS_DATA

          const pcItems: SearchResultItem[] = rawPcs
            .filter(p => matchItemWithTags(p, tags.value, resolveSearchTemplate('endpoint')))
            .map(p => resolveSearchTemplate('endpoint').extractSearchItem(p))
            .filter(item => !cleanQ || isFuzzyMatch(cleanQ, item.name, 0.7))

          // 3. Tickets
          const rawTickets: any[] = SAMPLE_TICKETS_DATA
          const ticketItems: SearchResultItem[] = rawTickets
            .filter(t => matchItemWithTags(t, tags.value, resolveSearchTemplate('machine')))
            .map(t => ({
              id: t.id || t.ticketNumber,
              name: t.ticketNumber || t.id,
              displayName: t.title || t.ticketNumber,
              itemType: 'Ticket',
              typeLabel: t.priority ? `${t.priority} Ticket` : 'Ticket',
              subtitle: `${t.department || 'Maintenance'} • Status: ${t.status} • Station: ${t.stationName || 'N/A'}`,
              status: t.status === 'Open' ? 'warning' : (t.status === 'InProgress' ? 'maintenance' : 'online'),
              sourceTable: 'tickets',
              isCrossTable: false,
              link: `/dashboard/tickets?ticketId=${encodeURIComponent(t.id || t.ticketNumber)}`
            }))
            .filter(item => !cleanQ || (item.displayName && isFuzzyMatch(cleanQ, item.displayName, 0.65)) || isFuzzyMatch(cleanQ, item.name, 0.7))

          // 4. Inventory
          let rawInv: any[] = []
          try {
            const inv = await $fetch<any[]>(`${endpoint}?query=${queryParam}`)
            if (Array.isArray(inv) && inv.length > 0) rawInv = inv
          } catch {
            try {
              const res = await $fetch<any>(`/api/inventory/filter?query=${queryParam}`)
              if (res && Array.isArray(res.items)) rawInv = res.items
            } catch {}
          }
          if (rawInv.length === 0 && (!queryStr || isPartScoped || (!isStationScoped && !isTicketScoped && !isControllerScoped && !isAlarmScoped))) {
            rawInv = SAMPLE_INVENTORY_DATA
          }

          const invItems: SearchResultItem[] = rawInv
            .filter(i => matchItemWithTags(i, tags.value, resolveSearchTemplate('inventory')))
            .map(i => resolveSearchTemplate('inventory').extractSearchItem(i))
            .filter(item => !cleanQ || isFuzzyMatch(cleanQ, item.name, 0.7) || (item.displayName && isFuzzyMatch(cleanQ, item.displayName, 0.7)))

          // Route results based on active scope
          if (isStationScoped) {
            primary = machineItems
            cross = []
          } else if (isTicketScoped) {
            primary = ticketItems
            cross = []
          } else if (isControllerScoped) {
            primary = pcItems
            cross = []
          } else if (isPartScoped) {
            primary = invItems
            cross = []
          } else if (isAlarmScoped) {
            primary = [...machineItems, ...ticketItems, ...pcItems].filter(i => 
              i.status === 'maintenance' || i.status === 'warning' || i.status === 'offline' || i.typeLabel?.includes('Critical')
            )
            cross = []
          } else {
            // Unscoped global search: merge inventory and machines as primary, tickets and PCs as cross
            primary = [...invItems.slice(0, 10), ...machineItems.slice(0, 5)]
            cross = [...pcItems.slice(0, 4), ...ticketItems.slice(0, 4)]
          }
        }
      }

      primaryResults.value = primary
      crossTableResults.value = cross
      results.value = [...primary, ...cross]
      localQueryCache.set(cacheKey, { primary, cross, timestamp: Date.now() })

      // Record GDPR-compliant diagnostics
      const durationMs = (typeof performance !== 'undefined' ? performance.now() : Date.now()) - startTime
      diagnosticsService.recordSearch({
        rawQuery: queryStr,
        durationMs,
        resultCount: results.value.length,
        itemTypes: results.value.map(r => r.itemType),
        matchedTagKeys: tags.value.map(t => t.key),
        fuzzyApplied: fuzzyConfig.value.enabled,
        dataSourceType: resolvedDataSourceType,
        templateUsed: activeTemplate.value.name,
        status: 'success'
      })
      diagnostics.value = diagnosticsService.getRecords()
    } catch (err: any) {
      results.value = []
      primaryResults.value = []
      crossTableResults.value = []

      const durationMs = (typeof performance !== 'undefined' ? performance.now() : Date.now()) - startTime
      diagnosticsService.recordSearch({
        rawQuery: queryStr,
        durationMs,
        resultCount: 0,
        fuzzyApplied: fuzzyConfig.value.enabled,
        dataSourceType: resolvedDataSourceType,
        templateUsed: activeTemplate.value.name,
        status: 'error',
        errorCode: err?.message ? String(err.message).substring(0, 50) : 'SEARCH_FAILED'
      })
      diagnostics.value = diagnosticsService.getRecords()
    } finally {
      isLoading.value = false
    }
  }

  // Process input for auto-tagging, key:value lookup, and stage 2 key matches
  const handleInputChange = (value: string) => {
    if (rawInput.value !== value) {
      rawInput.value = value
    }

    const trimmed = value.trim()

    // 1. Check if user is typing `key:` or `{modifier}{key}#{values}`
    const keyMatch = value.match(/(?:^|\s)(!|-|\+|&|\||\^|NOT|AND|OR|XOR)?([a-zA-Z0-9_-]+)([#:])([^\s]*)$/i)
    if (keyMatch) {
      const rawMod = keyMatch[1] || ''
      const activeKey = keyMatch[2].toLowerCase()
      const sep = keyMatch[3]
      const rawValStr = keyMatch[4] || ''

      const valSegments = rawValStr.split(',')
      const completed = valSegments.slice(0, -1).map(s => s.trim()).filter(Boolean)
      const partialVal = (valSegments[valSegments.length - 1] || '').toLowerCase()

      activePendingKey.value = activeKey
      activePendingModifier.value = rawMod
      activePendingSeparator.value = sep
      activePendingCompletedValues.value = completed

      // Retrieve suggestions from dynamically harvested known KVs
      const suggestions = getSuggestionsForKey(
        activeKey, 
        partialVal, 
        dynamicKnownKeyValues.value, 
        config.instanceId
      )

      // Also harvest from searchRegistry to populate indicators for entity types
      const registryMetadata = searchRegistry.harvestTagMetadata(activeKey)
      for (const meta of registryMetadata) {
        if (!partialVal || meta.value.toLowerCase().includes(partialVal)) {
          const existing = suggestions.find(s => s.value.toLowerCase() === meta.value.toLowerCase())
          if (existing) {
            existing.indicators = meta.indicators.map(i => ({ label: i.label, color: i.color }))
            if (meta.count) existing.count = meta.count
          } else {
            suggestions.push({
              key: activeKey,
              value: meta.value,
              description: meta.entityTypes.join(', '),
              sourceTable: meta.indicatorLabels[0] || config.instanceId,
              indicators: meta.indicators.map(i => ({ label: i.label, color: i.color })),
              count: meta.count
            })
          }
        }
      }

      // If exact partial match has few results, supplement with fuzzy matching on known values
      if (suggestions.length < 3 && partialVal.length >= 2 && fuzzyConfig.value.enabled) {
        const known = dynamicKnownKeyValues.value[activeKey]
        if (known) {
          const scored = rankByFuzzyScore(
            partialVal, 
            known.values, 
            v => [v], 
            fuzzyConfig.value.threshold
          )
          for (const s of scored) {
            if (!suggestions.some(ex => ex.value.toLowerCase() === s.item.toLowerCase())) {
              suggestions.push({
                key: activeKey,
                value: s.item,
                description: known.description,
                sourceTable: config.instanceId
              })
            }
          }
        }
      }

      valueSuggestions.value = suggestions
      matchingKeys.value = []
    } else {
      activePendingKey.value = ''
      activePendingModifier.value = ''
      activePendingSeparator.value = ':'
      activePendingCompletedValues.value = []
      valueSuggestions.value = []

      // 2. Stage 2: Filter Key Suggestions matching typed text
      if (trimmed.length > 0) {
        const lower = trimmed.toLowerCase()
        matchingKeys.value = ALL_FILTER_KEYS.filter(k => 
          k.key.toLowerCase().includes(lower) || lower.includes(k.key.toLowerCase())
        ).slice(0, 6)
      } else {
        matchingKeys.value = ALL_FILTER_KEYS.slice(0, 5)
      }
    }

    if (!config.enableAutoTagging || value.length < (config.minCharsForSuggestions || 2)) {
      autoSuggestions.value = []
      return
    }

    // Check for explicit key:value tags typed in (followed by whitespace)
    if (value.endsWith(' ') || value.includes(' ')) {
      const { tags: extractedTags, remainingText } = autoTagEngine.parseExplicitTags(value)
      if (extractedTags.length > 0) {
        for (const t of extractedTags) {
          addTag(t)
        }
        if (rawInput.value !== remainingText) {
          rawInput.value = remainingText
        }
        return
      }
    }

    // Run regex + fuzzy dictionary auto-tagging
    const { autoTags } = autoTagEngine.analyzeText(value)
    
    // Filter out tags that are already in active tags list
    autoSuggestions.value = autoTags.filter(
      at => !tags.value.some(existing => existing.key === at.tag.key && existing.value.toLowerCase() === at.tag.value.toLowerCase())
    )
  }

  // React to programmatic or external rawInput changes
  watch(rawInput, (newVal) => {
    if (newVal !== undefined) {
      handleInputChange(newVal)
    }
  })

  const addTag = (tag: Partial<TagPill>) => {
    const key = tag.key || 'keyword'
    const value = tag.value || ''
    if (!value) return

    // Avoid duplicate tags
    if (tags.value.some(t => t.key === key && t.value.toLowerCase() === value.toLowerCase())) {
      return
    }

    tags.value.push({
      id: `tag-${key}-${value}-${Date.now()}`,
      key,
      value,
      label: tag.label || `${key}: ${value}`,
      color: tag.color || (key === 'status' ? 'emerald' : key === 'tech' ? 'teal' : key === 'line' ? 'purple' : key === 'mfr' || key === 'manufacturer' ? 'blue' : 'indigo'),
      removable: true
    })

    // Clear suggestion if accepted
    autoSuggestions.value = autoSuggestions.value.filter(
      s => !(s.tag.key === key && s.tag.value.toLowerCase() === value.toLowerCase())
    )

    // Re-execute search with new tags
    executeSearch()
  }

  const selectKeySuggestion = (key: string) => {
    rawInput.value = `${key}:`
    activePendingKey.value = key
    handleInputChange(`${key}:`)
  }

  const selectValueSuggestion = (val: string) => {
    const key = activePendingKey.value || 'tag'
    const mod = activePendingModifier.value || ''
    const sep = activePendingSeparator.value || ':'
    const completed = activePendingCompletedValues.value || []
    const allValues = [...completed, val]
    const label = `${mod}${key}${sep}${allValues.join(',')}`

    addTag({ key, value: allValues.join(','), label })
    rawInput.value = ''
    activePendingKey.value = ''
    activePendingModifier.value = ''
    activePendingSeparator.value = ':'
    activePendingCompletedValues.value = []
    valueSuggestions.value = []
  }

  const removeTag = (tagId: string) => {
    tags.value = tags.value.filter(t => t.id !== tagId)
    executeSearch()
  }

  const clearAllTags = () => {
    tags.value = []
    rawInput.value = ''
    results.value = []
    primaryResults.value = []
    crossTableResults.value = []
    autoSuggestions.value = []
    valueSuggestions.value = []
    activePendingKey.value = ''
  }

  // Debounced live search
  watchDebounced(
    rawInput,
    () => {
      if (rawInput.value.length >= (config.minCharsForSuggestions || 2) || tags.value.length > 0) {
        executeSearch()
      } else if (tags.value.length === 0) {
        results.value = []
        primaryResults.value = []
        crossTableResults.value = []
      }
    },
    { debounce: config.debounceMs || 250 }
  )

  return {
    rawInput,
    tags,
    freeText,
    autoSuggestions,
    results,
    primaryResults,
    crossTableResults,
    matchingKeys,
    valueSuggestions,
    activePendingKey,
    searchKeyGroups,
    isLoading,
    isDropdownOpen,
    effectiveQueryString,
    config,
    handleInputChange,
    addTag,
    selectKeySuggestion,
    selectValueSuggestion,
    removeTag,
    clearAllTags,
    executeSearch,
    fetchSearchKeys,
    
    // Parameterized features
    activeTemplate,
    dynamicKnownKeyValues,
    rebuildIndex,
    diagnostics,
    searchDiagnostics: diagnosticsService
  }
}

