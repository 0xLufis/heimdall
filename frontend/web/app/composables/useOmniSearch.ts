import { ref, computed, watch, unref, type Ref } from 'vue'
import { watchDebounced } from '@vueuse/core'
import type { 
  TagPill, 
  AutoTagResult, 
  SearchInstanceConfig, 
  SearchResultItem, 
  SearchGroup,
  KeyLookupSuggestion,
  OmniSearchOptions,
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

const DEFAULT_CONFIG: SearchInstanceConfig = {
  instanceId: 'dashboard',
  placeholder: 'Search stations, controllers, inventory, specs... (Cmd+K)',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  allowedTagKeys: ['line', 'tech', 'status', 'mfr', 'manufacturer', 'serial', 'location', 'category', 'station', 'type', 'isstock', 'ip', 'mac', 'spec', 'cost', 'role', 'quality', 'unit'],
  minCharsForSuggestions: 2,
  debounceMs: 250,
  enableAutoTagging: true,
  showGlobalShortcut: true,
  enableDiagnostics: true
}

export const useOmniSearch = (
  optionsOrConfig: OmniSearchOptions | Partial<SearchInstanceConfig> = {}
) => {
  // Normalize options whether caller passed OmniSearchOptions or SearchInstanceConfig
  const isParameterized = 'dataSource' in optionsOrConfig || 'data' in optionsOrConfig || 'indexingTables' in optionsOrConfig || 'template' in optionsOrConfig
  const topConfig = optionsOrConfig.config || {}
  
  const config: SearchInstanceConfig = {
    ...DEFAULT_CONFIG,
    ...topConfig,
    instanceId: (optionsOrConfig as any).instanceId || topConfig.placeholder || DEFAULT_CONFIG.instanceId,
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
  const templateInput = (optionsOrConfig as OmniSearchOptions).template || config.instanceId
  const activeTemplate = computed<SearchTemplateDef>(() => resolveSearchTemplate(templateInput))

  // Ingested Data and Data Source
  const rawDataSource = (optionsOrConfig as OmniSearchOptions).dataSource
  const rawDataRef = (optionsOrConfig as OmniSearchOptions).data

  // Indexing Tables
  const indexingTablesRef = ref<RankedIndexingTable[]>(
    ((optionsOrConfig as OmniSearchOptions).indexingTables as RankedIndexingTable[]) || []
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

  // Execute multi-tier search across instance primary and cross-table indexing
  const executeSearch = async (queryStr: string = effectiveQueryString.value) => {
    const startTime = typeof performance !== 'undefined' ? performance.now() : Date.now()
    let resolvedDataSourceType: 'uri' | 'dbConnection' | 'redisCache' | 'custom' | 'inMemory' = 'inMemory'

    if (!queryStr && tags.value.length === 0) {
      results.value = []
      primaryResults.value = []
      crossTableResults.value = []
      return
    }

    isLoading.value = true
    try {
      const cleanQ = rawInput.value.toLowerCase().trim()
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
                link: '/dashboard/machines'
              }))
            }
          } catch {}
        } else {
          // Inventory or Global Search
          const endpoint = typeof rawDataSource === 'string' 
            ? rawDataSource 
            : (rawDataSource?.type === 'uri' ? rawDataSource.url : (config.defaultEndpoints?.[0] || '/api/proxy/inventory/search'))
          resolvedDataSourceType = 'uri'
          const queryParam = encodeURIComponent(queryStr)

          try {
            const res = await $fetch<any[]>(`${endpoint}?query=${queryParam}`)
            primary = (res || []).map((item: any) => resolveSearchTemplate('inventory').extractSearchItem(item))
          } catch {
            // Fallback to local filter API
            try {
              const res = await $fetch<any>(`/api/inventory/filter?query=${queryParam}`)
              if (res && Array.isArray(res.items)) {
                primary = res.items.map((item: any) => resolveSearchTemplate('inventory').extractSearchItem(item))
              }
            } catch {
              primary = []
            }
          }

          // Cross-table machines if query matches
          if (cleanQ.length >= 2) {
            try {
              const machines = await $fetch<any[]>('/api/proxy/v1/machine')
              if (Array.isArray(machines)) {
                const matchedMachines = machines.filter(m => 
                  (m.name || '').toLowerCase().includes(cleanQ) || 
                  (m.groupId || '').toLowerCase().includes(cleanQ) ||
                  (m.machineType || '').toLowerCase().includes(cleanQ)
                )
                cross = matchedMachines.slice(0, 6).map(m => ({
                  id: m.id,
                  name: m.name,
                  displayName: m.displayName,
                  itemType: 'Machine',
                  typeLabel: m.machineType || 'Station',
                  subtitle: `Line: ${m.groupId || 'General'}`,
                  sourceTable: 'machines',
                  isCrossTable: true,
                  link: `/dashboard/machines?id=${m.id}`
                }))
              }
            } catch {}
          }
        }
      }

      primaryResults.value = primary
      crossTableResults.value = cross
      results.value = [...primary, ...cross]

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
