import { ref, computed, watch, shallowRef } from 'vue'
import {
  parseSearchQuery,
  shrinkContext,
  type SearchContext,
  type SearchSource,
  type SearchTagFilter,
  type ParsedSearchQuery,
  type ContextSearchResult
} from '~/utils/search/searchContext'
import {
  indexTable,
  getIndexedTagValues,
  matchesTagFilters,
  formatResultTitle,
  formatResultSubtitle,
  type SearchIndexTable
} from '~/utils/search/searchIndexer'
import { fzfMatch, highlightFzfMatches } from '~/utils/search/fzfSearch'

import { searchRegistry } from '~/utils/search/searchRegistry'

export interface TagCompletionItem {
  key: string
  value: string
  inverted: boolean
  displayLabel: string
  score: number
  indices: number[]
  indicatorLabels?: string[]
  indicators?: Array<{ label: string; color: string }>
  count?: number
}

export function useTagContextSearch(initialContext: SearchContext) {
  const currentContext = shallowRef<SearchContext>(initialContext)
  const searchQuery = ref('')
  const cursorPosition = ref<number | undefined>(undefined)
  const activeSuggestionIndex = ref(0)
  const isLoading = ref(false)

  // In-memory indexing tables for all sources in active context
  const indexedTables = shallowRef<SearchIndexTable[]>([])

  // Compile / update indices
  function updateIndices() {
    const tables: SearchIndexTable[] = []
    for (const source of currentContext.value.sources) {
      tables.push(indexTable(source))
    }
    indexedTables.value = tables
  }

  // Initial index build
  updateIndices()

  // Re-index when context sources change
  watch(() => currentContext.value.sources, () => {
    updateIndices()
  })

  // Parsed query reactive state
  const parsedQuery = computed<ParsedSearchQuery>(() => {
    return parseSearchQuery(searchQuery.value, cursorPosition.value)
  })

  // Active shrunk context based on applied tags
  const activeContext = computed<SearchContext>(() => {
    return shrinkContext(currentContext.value, parsedQuery.value.tags)
  })

  // Filtered indexed tables matching shrunk context
  const activeIndexedTables = computed<SearchIndexTable[]>(() => {
    const activeSourceIds = new Set(activeContext.value.sources.map(s => s.id))
    return indexedTables.value.filter(t => activeSourceIds.has(t.sourceId))
  })

  // Available tag keys in current context
  const availableTags = computed<string[]>(() => {
    const keys = new Set<string>()
    for (const table of activeIndexedTables.value) {
      for (const col of table.columns) {
        keys.add(col.toLowerCase())
      }
    }
    keys.add('type')
    keys.add('entity')
    return Array.from(keys).sort()
  })

  // Autocomplete code completion suggestions when user types a tag (e.g. 'status#', '!mfr#sie', 'status:online')
  const completionSuggestions = computed<TagCompletionItem[]>(() => {
    const activeIncomplete = parsedQuery.value.activeIncompleteTag
    if (!activeIncomplete) return []

    const key = activeIncomplete.key.toLowerCase()
    const partial = activeIncomplete.partialValue.toLowerCase().trim()
    const inverted = activeIncomplete.inverted
    const sep = activeIncomplete.separator || '#'
    const rawMod = activeIncomplete.rawModifier ?? (inverted ? '!' : '')
    const completed = activeIncomplete.completedValues || []

    // Scope recommendations strictly to metadata values of this tag
    const metadataItems = searchRegistry.harvestTagMetadata(key)
    const indexValues = getIndexedTagValues(activeIndexedTables.value, key)

    // Merge metadata items with any table-indexed values not yet captured
    const valueMap = new Map<string, {
      value: string
      indicatorLabels: string[]
      indicators: Array<{ label: string; color: string }>
      count: number
    }>()

    for (const m of metadataItems) {
      valueMap.set(m.value.toLowerCase(), {
        value: m.value,
        indicatorLabels: m.indicatorLabels,
        indicators: m.indicators,
        count: m.count
      })
    }

    for (const val of indexValues) {
      const vLower = val.toLowerCase()
      if (!valueMap.has(vLower)) {
        // Find which tables contain this value
        const matchedTables = activeIndexedTables.value.filter(t => t.tagValues.get(key)?.has(val))
        valueMap.set(vLower, {
          value: val,
          indicatorLabels: matchedTables.map(t => t.indicatorLabel),
          indicators: matchedTables.map(t => ({ label: t.indicatorLabel, color: t.indicatorColor })),
          count: 1
        })
      }
    }

    if (valueMap.size === 0) return []

    const results: TagCompletionItem[] = []

    for (const item of valueMap.values()) {
      const val = item.value
      const displayLabel = sep === '#' || completed.length > 0
        ? `${rawMod}${key}#${[...completed, val].join(',')} `
        : `${rawMod}${key}:${val}`

      if (!partial) {
        results.push({
          key,
          value: val,
          inverted,
          displayLabel,
          score: 100,
          indices: [],
          indicatorLabels: item.indicatorLabels,
          indicators: item.indicators,
          count: item.count
        })
      } else {
        const match = fzfMatch(partial, val)
        if (match.matches) {
          results.push({
            key,
            value: val,
            inverted,
            displayLabel,
            score: match.score,
            indices: match.indices,
            indicatorLabels: item.indicatorLabels,
            indicators: item.indicators,
            count: item.count
          })
        } else if (val.toLowerCase().includes(partial)) {
          results.push({
            key,
            value: val,
            inverted,
            displayLabel,
            score: 50,
            indices: [],
            indicatorLabels: item.indicatorLabels,
            indicators: item.indicators,
            count: item.count
          })
        }
      }
    }

    // Sort by score descending then count descending
    results.sort((a, b) => b.score - a.score || (b.count || 0) - (a.count || 0))
    return results.slice(0, 15)
  })

  // Is completion popup open?
  const isCompletionOpen = computed(() => {
    return completionSuggestions.value.length > 0 && !!parsedQuery.value.activeIncompleteTag
  })

  // Apply a completion selection
  function applyCompletion(item: TagCompletionItem) {
    const activeIncomplete = parsedQuery.value.activeIncompleteTag
    if (!activeIncomplete) return

    const raw = searchQuery.value
    const sep = activeIncomplete.separator || '#'
    const rawMod = activeIncomplete.rawModifier ?? (activeIncomplete.inverted ? '!' : '')
    const completed = activeIncomplete.completedValues || []
    const allValues = [...completed, item.value]

    const completedToken = `${rawMod}${activeIncomplete.key}${sep}${allValues.join(',')} `

    if (activeIncomplete.rawToken && raw.includes(activeIncomplete.rawToken)) {
      searchQuery.value = raw.replace(activeIncomplete.rawToken, completedToken)
    } else {
      const tokenRegex = new RegExp(`(!?[a-zA-Z0-9_-]+)[#:][^\\s]*`, 'gi')
      searchQuery.value = raw.replace(tokenRegex, completedToken)
    }

    cursorPosition.value = searchQuery.value.length
    activeSuggestionIndex.value = 0
  }

  // Search Results computation
  const results = computed<ContextSearchResult[]>(() => {
    const query = parsedQuery.value
    const freeText = query.freeText.trim()
    const candidateResults: ContextSearchResult[] = []

    for (const table of activeIndexedTables.value) {
      for (const item of table.records.values()) {
        // 1. Tag filtering (positive & inverse)
        if (!matchesTagFilters(item, query.tags, table)) {
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
            // Check other columns
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
            if (!colMatched) continue // No match
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

    // Sort by score descending
    candidateResults.sort((a, b) => b.score - a.score)
    return candidateResults
  })

  // Keyboard navigation helpers
  function selectNextSuggestion() {
    if (completionSuggestions.value.length === 0) return
    activeSuggestionIndex.value = (activeSuggestionIndex.value + 1) % completionSuggestions.value.length
  }

  function selectPrevSuggestion() {
    if (completionSuggestions.value.length === 0) return
    activeSuggestionIndex.value = (activeSuggestionIndex.value - 1 + completionSuggestions.value.length) % completionSuggestions.value.length
  }

  function applyActiveSuggestion(): boolean {
    if (isCompletionOpen.value && completionSuggestions.value[activeSuggestionIndex.value]) {
      applyCompletion(completionSuggestions.value[activeSuggestionIndex.value])
      return true
    }
    return false
  }

  return {
    currentContext,
    searchQuery,
    cursorPosition,
    parsedQuery,
    activeContext,
    availableTags,
    completionSuggestions,
    isCompletionOpen,
    activeSuggestionIndex,
    results,
    isLoading,
    updateIndices,
    applyCompletion,
    selectNextSuggestion,
    selectPrevSuggestion,
    applyActiveSuggestion
  }
}
