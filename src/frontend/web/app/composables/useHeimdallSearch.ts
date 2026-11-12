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
import { rankByFuzzyScore } from '~/utils/search/fuzzy'
import { rankRecords } from '~/utils/search/searchRanking'
import {
  loadCatalog,
  resolveSearchScope,
  inventoryFilterExpression,
  searchServer,
  ticketToSearchItem,
  CATALOG_FIELDS,
  type CatalogName,
  type SearchScopeKind,
  type ServerSearchKind
} from '~/utils/search/searchSources'
import { loadRecentSearches, addRecentSearch, saveRecentSearches } from '~/utils/search/recentSearches'
import { 
  SearchDiagnosticsService, 
  defaultSearchDiagnostics,
  type SearchDiagnosticsRecord 
} from '~/utils/search/searchDiagnostics'

/**
 * Heimdall Search composable - tag-aware fuzzy search across all Heimdall entities
 * Universal high-throughput multi-tier fuzzy indexing engine for industrial assets,
 * client PCs, telemetry, and enterprise field inventory.
 */
/** Hits per entity kind asked from the server: a few of each in a mixed list, more when one kind is the subject. */
const MIXED_RESULT_LIMIT = 8
const FOCUSED_RESULT_LIMIT = 20

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
    instanceId: (optionsOrConfig as any).instanceId || DEFAULT_CONFIG.instanceId,
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
  const searchKeyGroups = ref<SearchGroup[]>(buildDynamicSearchKeyGroups(dynamicKnownKeyValues.value))
  const isLoading = ref(false)
  const isDropdownOpen = ref(false)
  // Set when every queried source failed; `failedSources` lists partial outages
  const error = ref<string | null>(null)
  const failedSources = ref<string[]>([])
  const recentSearches = ref<string[]>(loadRecentSearches())

  const rememberSearch = (query: string = rawInput.value) => {
    const next = addRecentSearch(recentSearches.value, query)
    if (next === recentSearches.value) return
    recentSearches.value = next
    saveRecentSearches(next)
  }

  const clearRecentSearches = () => {
    recentSearches.value = []
    saveRecentSearches([])
  }

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
      const itemVal = kv[k] ?? item[tag.key] ?? item[k]
      if (itemVal === undefined || itemVal === null) return false

      if (Array.isArray(itemVal)) {
        if (!itemVal.some(v => String(v).toLowerCase() === targetVal)) return false
      } else {
        if (String(itemVal).toLowerCase() !== targetVal) return false
      }
    }
    return true
  }

  // Fields of a raw record the active template declares searchable, most important first
  const templateFields = (item: any, template: SearchTemplateDef): string[] =>
    template.searchableFields.map(f => (item?.[f] != null ? String(item[f]) : ''))

  const rankOptions = () => ({ fuzzy: fuzzyConfig.value.enabled, threshold: fuzzyConfig.value.threshold })

  const MAX_CACHED_QUERIES = 50
  const QUERY_CACHE_TTL_MS = 60_000
  const localQueryCache = new Map<string, { primary: SearchResultItem[]; cross: SearchResultItem[]; timestamp: number }>()

  // Monotonic id of the latest search; responses of older searches are discarded
  let searchSequence = 0

  const clearResults = () => {
    results.value = []
    primaryResults.value = []
    crossTableResults.value = []
  }

  /** Free text of a query string: everything that is not a `key:value` or prefix-scope token. */
  const stripTagTokens = (queryStr: string) =>
    queryStr.replace(/(?:\w+):(?:"[^"]*"|\S+)/g, ' ').replace(/(?:^|\s)[@#!*%](?:"[^"]*"|\S+)/g, ' ').replace(/\s+/g, ' ').trim()

  // Execute multi-tier search across instance primary and cross-table indexing
  const executeSearch = async (queryStr: string = effectiveQueryString.value) => {
    const startTime = typeof performance !== 'undefined' ? performance.now() : Date.now()
    let resolvedDataSourceType: 'uri' | 'dbConnection' | 'redisCache' | 'custom' | 'inMemory' = 'inMemory'
    const sequence = ++searchSequence
    const isLatest = () => sequence === searchSequence

    if (!queryStr && tags.value.length === 0) {
      clearResults()
      error.value = null
      failedSources.value = []
      isLoading.value = false
      return
    }

    const activeTags = [...tags.value]
    const cacheKey = `${config.instanceId}:${queryStr}:${activeTags.map(t => `${t.key}=${t.value}`).join(';')}`
    const cached = localQueryCache.get(cacheKey)
    const cacheIsFresh = !!cached && (Date.now() - cached.timestamp) < QUERY_CACHE_TTL_MS

    if (cacheIsFresh) {
      // Show the cached page immediately; the refresh below replaces it without a spinner.
      primaryResults.value = cached!.primary
      crossTableResults.value = cached!.cross
      results.value = [...cached!.primary, ...cached!.cross]
    } else {
      isLoading.value = true
    }

    try {
      // An explicitly passed query is authoritative; otherwise the live input is the free text.
      const isExplicitQuery = queryStr !== effectiveQueryString.value
      const cleanQ = (isExplicitQuery ? stripTagTokens(queryStr) : (rawInput.value || '')).toLowerCase().trim()
      let primary: SearchResultItem[] = []
      let cross: SearchResultItem[] = []
      const failed: string[] = []
      let attemptedSources = 0

      // -----------------------------------------------------------------
      // PATH A: Direct in-memory data or custom dataSource handler provided
      // -----------------------------------------------------------------
      const unrefData = rawDataRef ? unref(rawDataRef) : undefined
      const isDataProvided = unrefData !== undefined
      const isCustomSource = !!rawDataSource && typeof rawDataSource === 'object' && rawDataSource.type === 'custom'

      if (isDataProvided || isCustomSource) {
        let candidateItems: any[] = []

        if (isCustomSource) {
          resolvedDataSourceType = 'custom'
          const customRes = await rawDataSource.handler(queryStr, { tags: activeTags, freeText: cleanQ })
          candidateItems = Array.isArray(customRes) ? customRes : (customRes?.items || (customRes ? [customRes] : []))
        } else if (Array.isArray(unrefData)) {
          candidateItems = unrefData
        } else if (unrefData && typeof unrefData === 'object') {
          candidateItems = Array.isArray((unrefData as any).items) ? (unrefData as any).items : [unrefData]
        }

        const currentTemplate = activeTemplate.value
        const tagMatched = candidateItems.filter(raw => matchItemWithTags(raw, activeTags, currentTemplate))
        primary = rankRecords(cleanQ, tagMatched, raw => templateFields(raw, currentTemplate), rankOptions())
          .map(s => currentTemplate.extractSearchItem(s.item))

        // Query secondary indexing tables for cross-table results if configured
        if (indexingTablesRef.value.length > 0 && cleanQ.length >= 2) {
          const secondaryTables = indexingTablesRef.value.filter(t => t.rank > 1)
          for (const tbl of secondaryTables) {
            const tblData = typeof tbl.data === 'function' ? tbl.data() : unref(tbl.data)
            if (!Array.isArray(tblData)) continue
            const searchable = tbl.searchFields || ['name', 'hostname', 'customIdentifier']
            const matched = rankRecords(cleanQ, tblData, rawItem => searchable.map(field => rawItem?.[field]), rankOptions()).slice(0, 5)

            matched.forEach(({ item: m }, index) => {
              const name = m.name || m.hostname || m.customIdentifier || 'Cross Item'
              cross.push({
                // Stable id: keyboard selection and list keys must survive a re-render.
                id: String(m.id ?? m[tbl.keyField || 'id'] ?? `${tbl.name}:${name}:${index}`),
                name,
                displayName: m.displayName,
                itemType: tbl.name,
                typeLabel: tbl.name,
                sourceTable: tbl.name,
                isCrossTable: true,
                subtitle: tbl.description
              })
            })
          }
        }
      } else {
        // -----------------------------------------------------------------
        // PATH B: Endpoint-based multi-source search (all sources in parallel)
        // -----------------------------------------------------------------
        resolvedDataSourceType = 'uri'
        const isMachinesInstance = config.instanceId === 'machines'
        const isNodesInstance = config.instanceId === 'nodes' || config.instanceId === 'clients'
        const isInventoryInstance = config.instanceId === 'inventory'

        const scope = resolveSearchScope(activeTags)
        const inScope = (kind: SearchScopeKind) => !scope.kinds || scope.kinds.has(kind)
        // Scope values such as `station:OP20` are search terms for that scope.
        const term = [cleanQ, ...scope.terms.map(t => t.toLowerCase())].filter(Boolean).join(' ')
        const filterQuery = [inventoryFilterExpression(scope.filterTags), term].filter(Boolean).join(' ')
        const hasCriteria = term.length > 0 || scope.filterTags.length > 0 || !!scope.kinds

        const runSource = async <T>(label: string, task: () => Promise<T>, fallback: T): Promise<T> => {
          attemptedSources++
          try {
            return await task()
          } catch {
            failed.push(label)
            return fallback
          }
        }

        const searchCatalog = (name: CatalogName, limit?: number) => async (): Promise<SearchResultItem[]> => {
          const records = await loadCatalog(name)
          const ranked = rankRecords(term, records, CATALOG_FIELDS[name], rankOptions()).map(s => s.item)
          const page = limit ? ranked.slice(0, limit) : ranked
          if (name === 'machines') return page.map(m => resolveSearchTemplate('machine').extractSearchItem(m))
          if (name === 'controllers') return page.map(c => resolveSearchTemplate('endpoint').extractSearchItem(c))
          return page.map(ticketToSearchItem)
        }

        const searchInventory = (limit?: number) => async (): Promise<SearchResultItem[]> => {
          if (!filterQuery) return []
          const endpoint = typeof rawDataSource === 'string'
            ? rawDataSource
            : (rawDataSource?.type === 'uri' ? rawDataSource.url : (config.defaultEndpoints?.[0] || '/api/proxy/inventory/search'))
          const queryParam = encodeURIComponent(filterQuery)
          let rows: any[]
          try {
            const res = await $fetch<any>(`${endpoint}?query=${queryParam}`)
            rows = Array.isArray(res) ? res : (res?.items || [])
          } catch {
            // The backend search endpoint is unavailable: fall back to the BFF filter API.
            const res = await $fetch<any>(`/api/inventory/filter?query=${queryParam}`)
            rows = Array.isArray(res?.items) ? res.items : []
          }
          // Stations live in the inventory table too; they are a search kind of their own and
          // would otherwise be listed twice (once as a part that links to the inventory page).
          const items = rows
            .filter((item: any) => String(item?.itemType || '').toLowerCase() !== 'machine')
            .map((item: any) => resolveSearchTemplate('inventory').extractSearchItem(item))
          return limit ? items.slice(0, limit) : items
        }

        const asCross = (items: SearchResultItem[]) => items.map(i => ({ ...i, isCrossTable: true }))
        const none = async (): Promise<SearchResultItem[]> => []

        // Server-side search: one request, matched, ranked and tenant-scoped by the backend.
        // Inventory filter tags and custom endpoints are only understood by the inventory search,
        // so those use the per-source path below, which is also the fallback when the search
        // endpoint cannot be reached (the catalogues are then ranked in the browser).
        let served = false
        if (hasCriteria && term.length >= 2 && scope.filterTags.length === 0 && !rawDataSource) {
          const wanted: ServerSearchKind[] = isMachinesInstance
            ? ['stations', 'inventory']
            : isNodesInstance
              ? ['controllers', 'stations']
              : ([
                  inScope('inventory') && 'inventory',
                  inScope('machines') && 'stations',
                  inScope('controllers') && !isInventoryInstance && 'controllers',
                  inScope('tickets') && !isInventoryInstance && 'tickets',
                  inScope('users') && !isInventoryInstance && 'users'
                ].filter(Boolean) as ServerSearchKind[])
          const focused = !!scope.kinds || isMachinesInstance || isNodesInstance
          if (wanted.length > 0) {
            try {
              const hits = await searchServer(term, wanted, focused ? FOCUSED_RESULT_LIMIT : MIXED_RESULT_LIMIT)
              if (isMachinesInstance) {
                primary = hits.stations
                cross = asCross(hits.inventory.slice(0, 10))
              } else if (isNodesInstance) {
                primary = hits.controllers
                cross = asCross(hits.stations.slice(0, 5))
              } else {
                primary = hits.inventory
                cross = asCross([...hits.stations, ...hits.controllers, ...hits.tickets, ...hits.users])
              }
              attemptedSources++
              served = true
            } catch {
              // Users have no catalogue to fall back to: say so when they were asked for explicitly.
              if (scope.kinds?.has('users')) {
                attemptedSources++
                failed.push('users')
              }
            }
          }
        }

        if (hasCriteria && !served) {
          if (isMachinesInstance) {
            const [machines, parts] = await Promise.all([
              runSource('stations', searchCatalog('machines'), []),
              runSource('inventory', term.length >= 2 ? searchInventory(10) : none, [])
            ])
            primary = machines
            cross = asCross(parts)
          } else if (isNodesInstance) {
            const [nodes, machines] = await Promise.all([
              runSource('controllers', searchCatalog('controllers'), []),
              runSource('stations', term.length >= 2 ? searchCatalog('machines', 5) : none, [])
            ])
            primary = nodes
            cross = asCross(machines)
          } else {
            const crossEnabled = term.length >= 2 || !!scope.kinds
            const [parts, machines, nodes, tickets] = await Promise.all([
              inScope('inventory') ? runSource('inventory', searchInventory(), []) : none(),
              inScope('machines') && crossEnabled ? runSource('stations', searchCatalog('machines', 6), []) : none(),
              inScope('controllers') && crossEnabled && !isInventoryInstance ? runSource('controllers', searchCatalog('controllers', 6), []) : none(),
              inScope('tickets') && crossEnabled && !isInventoryInstance ? runSource('tickets', searchCatalog('tickets', 6), []) : none()
            ])
            primary = parts
            cross = asCross([...machines, ...nodes, ...tickets])
          }
        }
      }

      // A newer search started while this one was in flight: its results win.
      if (!isLatest()) return

      primaryResults.value = primary
      crossTableResults.value = cross
      results.value = [...primary, ...cross]
      failedSources.value = failed
      error.value = attemptedSources > 0 && failed.length === attemptedSources
        ? 'Search is unavailable right now. Check the connection to the Heimdall backend and try again.'
        : null

      if (failed.length === 0) {
        if (localQueryCache.size >= MAX_CACHED_QUERIES) {
          const oldest = localQueryCache.keys().next().value
          if (oldest !== undefined) localQueryCache.delete(oldest)
        }
        localQueryCache.set(cacheKey, { primary, cross, timestamp: Date.now() })
      }

      // Record GDPR-compliant diagnostics
      const durationMs = (typeof performance !== 'undefined' ? performance.now() : Date.now()) - startTime
      diagnosticsService.recordSearch({
        rawQuery: queryStr,
        durationMs,
        resultCount: results.value.length,
        itemTypes: results.value.map(r => r.itemType),
        matchedTagKeys: activeTags.map(t => t.key),
        fuzzyApplied: fuzzyConfig.value.enabled,
        dataSourceType: resolvedDataSourceType,
        templateUsed: activeTemplate.value.name,
        status: error.value ? 'error' : 'success',
        errorCode: error.value ? 'ALL_SOURCES_FAILED' : undefined
      })
      diagnostics.value = diagnosticsService.getRecords()
    } catch (err: any) {
      if (!isLatest()) return
      clearResults()
      failedSources.value = []
      error.value = 'Search failed. Please try again.'

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
      // Only the latest search owns the loading flag; a stale one must not hide the spinner.
      if (isLatest()) isLoading.value = false
    }
  }

  // Process input for auto-tagging, key:value lookup, and stage 2 key matches
  const handleInputChange = (value: string) => {
    if (rawInput.value !== value) {
      rawInput.value = value
    }

    const trimmed = value.trim()

    // 1. Check if user is typing `key:` or `key:partial`
    const keyMatch = value.match(/(?:^|\s)([a-zA-Z0-9_-]+):([^\s]*)$/)
    if (keyMatch) {
      const activeKey = keyMatch[1].toLowerCase()
      const partialVal = (keyMatch[2] || '').toLowerCase()
      activePendingKey.value = activeKey

      // Retrieve suggestions from dynamically harvested known KVs
      const suggestions = getSuggestionsForKey(
        activeKey, 
        partialVal, 
        dynamicKnownKeyValues.value, 
        config.instanceId
      )

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
    addTag({ key, value: val })
    rawInput.value = ''
    activePendingKey.value = ''
    valueSuggestions.value = []
  }

  const removeTag = (tagId: string) => {
    tags.value = tags.value.filter(t => t.id !== tagId)
    executeSearch()
  }

  const clearAllTags = () => {
    // Invalidate any in-flight search so its response cannot repopulate the cleared list
    searchSequence++
    isLoading.value = false
    error.value = null
    failedSources.value = []
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
        // Below the minimum length: drop results and cancel whatever is still in flight
        searchSequence++
        isLoading.value = false
        error.value = null
        failedSources.value = []
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
    error,
    failedSources,
    recentSearches,
    rememberSearch,
    clearRecentSearches,
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

/**
 */
