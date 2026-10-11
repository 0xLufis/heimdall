import { ref, computed, watch, shallowRef } from 'vue'
import {
  parseSearchQuery,
  type ParsedSearchQuery,
  type SearchTagFilter,
  type TagModifier,
  type ContextSearchResult
} from '~/utils/search/searchContext'
import {
  searchRegistry,
  type SearchProvider,
  type TagMetadataItem
} from '~/utils/search/searchRegistry'
import {
  formatResultTitle,
  formatResultSubtitle,
  matchesTagFilters,
  type SearchIndexTable
} from '~/utils/search/searchIndexer'
import { fzfMatch, highlightFzfMatches } from '~/utils/search/fzfSearch'

export interface ConfigurableSearchOptions {
  contextId?: string // e.g. 'global', 'machines', 'clients', 'tickets', 'inventory'
  providerIds?: string[]
  initialQuery?: string
  placeholder?: string
  debounceMs?: number
}

export interface SearchExecutionMetadata {
  executedAt: number
  durationMs: number
  totalResults: number
  entityTypeCounts: Record<string, number>
  query: string
  cached: boolean
}

export function useConfigurableSearch(options: ConfigurableSearchOptions = {}) {
  const searchQuery = ref(options.initialQuery || '')
  const cursorPosition = ref<number | undefined>(undefined)
  const activeSuggestionIndex = ref(0)
  const isLoading = ref(false)
  
  const searchResultCache = new Map<string, { results: ContextSearchResult[], metadata: SearchExecutionMetadata, timestamp: number }>()
  const searchMetadata = ref<SearchExecutionMetadata>({
    executedAt: 0,
    durationMs: 0,
    totalResults: 0,
    entityTypeCounts: {},
    query: '',
    cached: false
  })

  // Active providers based on contextId and providerIds
  const activeProviders = computed<SearchProvider[]>(() => {
    if (options.providerIds && options.providerIds.length > 0) {
      return options.providerIds
        .map(id => searchRegistry.getProvider(id))
        .filter(Boolean) as SearchProvider[]
    }
    return searchRegistry.getProvidersForContext(options.contextId)
  })

  const activeProviderIds = computed<string[]>(() => {
    return activeProviders.value.map(p => p.id)
  })

  // Parsed query state
  const parsedQuery = computed<ParsedSearchQuery>(() => {
    return parseSearchQuery(searchQuery.value, cursorPosition.value)
  })

  // Active incomplete tag (user is currently typing {modifier}{tag}#...)
  const activeTag = computed(() => {
    return parsedQuery.value.activeIncompleteTag
  })

  const isEditingTag = computed(() => {
    return !!activeTag.value
  })

  // Available tag keys from active providers (normalized to canonical names)
  const availableTagKeys = computed<string[]>(() => {
    const CANONICAL_MAP: Record<string, string> = {
      mfr: 'manufacturer',
      host: 'hostname',
      serialnumber: 'serial',
      ticketnumber: 'ticket',
      ipaddress: 'ip',
      macaddress: 'mac',
      osversion: 'os',
      loc: 'location',
      dept: 'department'
    }
    const keys = new Set<string>()
    for (const p of activeProviders.value) {
      for (const k of p.tagKeys) {
        const canonical = CANONICAL_MAP[k.toLowerCase()] || k.toLowerCase()
        keys.add(canonical)
      }
    }
    keys.add('type')
    keys.add('entity')
    return Array.from(keys).sort()
  })

  // Scoped tag metadata recommendations when editing a tag
  const scopedTagRecommendations = computed<TagMetadataItem[]>(() => {
    if (!activeTag.value) return []

    const tagKey = activeTag.value.key.toLowerCase()
    const partial = (activeTag.value.partialValue || '').toLowerCase().trim()

    // Harvest metadata strictly scoped to this tag key across active providers
    const allMetadata = searchRegistry.harvestTagMetadata(tagKey, activeProviderIds.value)

    if (!partial) {
      // Return top metadata values for this tag
      return allMetadata.slice(0, 15)
    }

    // Filter using FZF / substring matching on metadata values
    const filtered: Array<{ item: TagMetadataItem; score: number }> = []

    for (const meta of allMetadata) {
      const match = fzfMatch(partial, meta.value)
      if (match.matches) {
        filtered.push({
          item: meta,
          score: match.score
        })
      } else if (meta.value.toLowerCase().includes(partial)) {
        filtered.push({
          item: meta,
          score: 50
        })
      }
    }

    filtered.sort((a, b) => b.score - a.score)
    return filtered.map(f => f.item).slice(0, 15)
  })

  // In-memory indexing tables for active providers
  const indexedTables = computed<SearchIndexTable[]>(() => {
    return searchRegistry.getIndexTables(activeProviderIds.value)
  })

  // Matching entity results when not editing a tag or evaluating free-text
  const results = computed<ContextSearchResult[]>(() => {
    const query = parsedQuery.value
    const freeText = query.freeText.trim()
    const tags = query.tags
    const candidateResults: ContextSearchResult[] = []

    // If query is completely empty, return empty results
    if (!searchQuery.value.trim() && tags.length === 0) {
      searchMetadata.value = {
        executedAt: Date.now(),
        durationMs: 0,
        totalResults: 0,
        entityTypeCounts: {},
        query: '',
        cached: false
      }
      return []
    }

    const cacheKey = `${activeProviderIds.value.join(',')}:${searchQuery.value}`
    const cached = searchResultCache.get(cacheKey)
    const now = Date.now()
    if (cached && (now - cached.timestamp < 10000)) {
      searchMetadata.value = { ...cached.metadata, cached: true }
      return cached.results
    }

    const startTime = typeof performance !== 'undefined' ? performance.now() : Date.now()

    for (const table of indexedTables.value) {
      for (const item of table.records.values()) {
        // 1. Tag filtering (AND, OR, XOR, NOT)
        if (!matchesTagFilters(item, tags, table)) {
          continue
        }

        const title = formatResultTitle(item, table)
        const subtitle = formatResultSubtitle(item, table)

        // 2. Free-text fuzzy scoring
        let score = 100
        let matchedIndices: number[] = []

        if (freeText.length > 0) {
          const titleMatch = fzfMatch(freeText, title)
          const subMatch = fzfMatch(freeText, subtitle)

          if (titleMatch.matches) {
            score = titleMatch.score + 200 // title bonus
            matchedIndices = titleMatch.indices
          } else if (subMatch.matches) {
            score = subMatch.score
          } else {
            // Check searchable fields
            let colMatched = false
            for (const col of table.columns) {
              const val = String(item[col] ?? '')
              const m = fzfMatch(freeText, val)
              if (m.matches) {
                score = m.score - 50
                colMatched = true
                break
              }
            }
            if (!colMatched) continue
          }
        }

        const link = table.linkGenerator ? table.linkGenerator(item) : `/dashboard?searchItem=${item.id}`
        const highlightedTitle = matchedIndices.length > 0
          ? highlightFzfMatches(title, matchedIndices)
          : title

        candidateResults.push({
          id: String(item[table.indexedKey] ?? item.id ?? Math.random().toString()),
          title,
          subtitle,
          entityType: table.entityType,
          indicatorLabel: table.indicatorLabel,
          indicatorColor: table.indicatorColor,
          link,
          rawItem: item,
          score,
          matchedIndices,
          highlightedTitle
        })
      }
    }

    candidateResults.sort((a, b) => b.score - a.score)

    const endTime = typeof performance !== 'undefined' ? performance.now() : Date.now()
    const counts: Record<string, number> = {}
    for (const r of candidateResults) {
      counts[r.entityType] = (counts[r.entityType] || 0) + 1
    }

    const meta: SearchExecutionMetadata = {
      executedAt: now,
      durationMs: Math.round((endTime - startTime) * 100) / 100,
      totalResults: candidateResults.length,
      entityTypeCounts: counts,
      query: searchQuery.value,
      cached: false
    }
    searchMetadata.value = meta
    searchResultCache.set(cacheKey, { results: candidateResults, metadata: meta, timestamp: now })

    return candidateResults
  })

  // Apply a tag metadata recommendation to the search input
  const applyTagRecommendation = (rec: TagMetadataItem) => {
    const incomplete = activeTag.value
    if (!incomplete) return

    const raw = searchQuery.value
    const rawMod = incomplete.rawModifier || ''
    const sep = incomplete.separator || '#'
    const completed = incomplete.completedValues || []
    const allValues = [...completed, rec.value]

    // Form completed token with space delimiter: {modifier}{tag}#{value1},{valueN} 
    const completedToken = `${rawMod}${incomplete.key}${sep}${allValues.join(',')} `

    if (incomplete.rawToken && raw.includes(incomplete.rawToken)) {
      searchQuery.value = raw.replace(incomplete.rawToken, completedToken)
    } else {
      searchQuery.value = `${raw.trimEnd()} ${completedToken}`.trimStart()
    }

    cursorPosition.value = searchQuery.value.length
    activeSuggestionIndex.value = 0
  }

  // Manually apply a tag
  const applyTag = (key: string, values: string[], modifier: TagModifier = 'NONE') => {
    let modPrefix = ''
    if (modifier === 'NOT') modPrefix = '!'
    else if (modifier === 'AND') modPrefix = '+'
    else if (modifier === 'OR') modPrefix = '|'
    else if (modifier === 'XOR') modPrefix = '^'

    const token = `${modPrefix}${key}#${values.join(',')} `
    searchQuery.value = `${searchQuery.value.trim()} ${token}`.trim() + ' '
    cursorPosition.value = searchQuery.value.length
  }

  // Remove tag by index
  const removeTag = (index: number) => {
    const tag = parsedQuery.value.tags[index]
    if (!tag) return
    searchQuery.value = searchQuery.value.replace(tag.raw, '').replace(/\s+/g, ' ').trim()
  }

  // Clear query
  const clearQuery = () => {
    searchQuery.value = ''
    cursorPosition.value = undefined
    activeSuggestionIndex.value = 0
  }

  return {
    searchQuery,
    cursorPosition,
    parsedQuery,
    activeProviders,
    activeProviderIds,
    activeTag,
    isEditingTag,
    availableTagKeys,
    scopedTagRecommendations,
    results,
    searchMetadata,
    activeSuggestionIndex,
    isLoading,
    applyTagRecommendation,
    applyTag,
    removeTag,
    clearQuery
  }
}
