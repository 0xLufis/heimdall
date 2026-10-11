import { ref, computed, type Ref, unref } from 'vue'

export interface FacetOption {
  value: string
  label?: string
  count: number
}

export interface FacetDefinition<T = any> {
  key: string
  label: string
  getValues?: (item: T) => string[] | string | undefined | null
}

export interface ParsedTag {
  raw: string
  key?: string
  value: string
  isNegative: boolean
}

export interface SearchFilterConfig<T = any> {
  searchFields?: (keyof T | string)[]
  facetDefinitions?: FacetDefinition<T>[]
  initialSearchQuery?: string
  initialTags?: string[]
  initialFacets?: Record<string, string[]>
}

/**
 * Parses a tag string such as:
 * - "status:active" -> { key: 'status', value: 'active', isNegative: false }
 * - "!status:decommissioned" -> { key: 'status', value: 'decommissioned', isNegative: true }
 * - "#welder" -> { value: '#welder', isNegative: false }
 * - "!#broken" -> { value: '#broken', isNegative: true }
 */
export function parseTagString(rawTag: string): ParsedTag {
  const trimmed = rawTag.trim()
  const isNegative = trimmed.startsWith('!')
  const clean = isNegative ? trimmed.slice(1).trim() : trimmed

  const colonIdx = clean.indexOf(':')
  if (colonIdx > 0) {
    const key = clean.slice(0, colonIdx).trim().toLowerCase()
    const value = clean.slice(colonIdx + 1).trim()
    return {
      raw: trimmed,
      key,
      value,
      isNegative
    }
  }

  return {
    raw: trimmed,
    value: clean,
    isNegative
  }
}

/**
 * Extracts searchable text strings from an item based on searchFields or all top-level primitive values.
 */
function extractSearchableStrings<T>(item: T, searchFields?: (keyof T | string)[]): string[] {
  if (!item || typeof item !== 'object') return []

  const strings: string[] = []

  if (searchFields && searchFields.length > 0) {
    for (const field of searchFields) {
      const val = (item as any)[field]
      if (val !== undefined && val !== null) {
        if (typeof val === 'string' || typeof val === 'number') {
          strings.push(String(val))
        } else if (Array.isArray(val)) {
          for (const el of val) {
            if (typeof el === 'string' || typeof el === 'number') strings.push(String(el))
            else if (el && typeof el === 'object') {
              if (el.name) strings.push(String(el.name))
              if (el.label) strings.push(String(el.label))
              if (el.id) strings.push(String(el.id))
            }
          }
        } else if (typeof val === 'object') {
          if (val.name) strings.push(String(val.name))
          if (val.customIdentifier) strings.push(String(val.customIdentifier))
          if (val.id) strings.push(String(val.id))
        }
      }
    }
    return strings
  }

  // Auto-extract from top-level primitive properties
  for (const [k, v] of Object.entries(item)) {
    if (v === undefined || v === null) continue
    if (typeof v === 'string' || typeof v === 'number') {
      strings.push(String(v))
    } else if (Array.isArray(v)) {
      for (const el of v) {
        if (typeof el === 'string' || typeof el === 'number') strings.push(String(el))
        else if (el && typeof el === 'object') {
          if (el.name) strings.push(String(el.name))
          if (el.label) strings.push(String(el.label))
        }
      }
    } else if (typeof v === 'object') {
      if ((v as any).name) strings.push(String((v as any).name))
      if ((v as any).customIdentifier) strings.push(String((v as any).customIdentifier))
    }
  }

  return strings
}

/**
 * Standard Composable splitting OmniSearch into:
 * 1. Text Search Engine (free text matching & scoring)
 * 2. Structured Filtering Engine (tags, key:value facets, negative gates)
 * Reusable across any dataset in Heimdall.
 */
export function useSearchAndFilter<T = any>(
  itemsSource: Ref<T[]> | (() => T[]) | T[],
  config: SearchFilterConfig<T> = {}
) {
  const searchQuery = ref(config.initialSearchQuery || '')
  const tagFilters = ref<string[]>(config.initialTags ? [...config.initialTags] : [])
  const facetFilters = ref<Record<string, string[]>>({ ...(config.initialFacets || {}) })

  const rawItems = computed<T[]>(() => {
    return unref(itemsSource) || []
  })

  // Parsed structured tag objects
  const parsedTags = computed<ParsedTag[]>(() => {
    return tagFilters.value.map(parseTagString)
  })

  // ─── 1. STRUCTURED FILTERING ENGINE ───────────────────────────────────────────
  const filteredItems = computed<T[]>(() => {
    const all = rawItems.value
    const tags = parsedTags.value
    const facets = facetFilters.value

    return all.filter((item: any) => {
      // A. Check Structured Tag Filters (AND logic between tags)
      for (const tag of tags) {
        let matches = false

        if (tag.key) {
          // Attribute-keyed tag: e.g. status:active, line:Line 1, tech:welding
          const targetKey = tag.key.toLowerCase()
          const expectedVal = tag.value.toLowerCase()

          // Check direct property or nested metadata/data
          const directVal = item[targetKey] || item[tag.key]
          const metaVal = item.metadata?.[tag.key] || item.data?.[tag.key] || item.data?.[targetKey]
          const groupVal = targetKey === 'line' || targetKey === 'group' ? (item.groupId || item.groupName) : undefined
          const typeVal = targetKey === 'type' ? (item.machineType || item.itemType || item.type) : undefined
          const stationVal = targetKey === 'station' ? (item.stationName || item.stationId || item.customIdentifier) : undefined

          const checkCandidate = (cand: any): boolean => {
            if (cand === undefined || cand === null) return false
            const str = String(cand).toLowerCase()
            return str === expectedVal || str.includes(expectedVal)
          }

          matches = checkCandidate(directVal) || checkCandidate(metaVal) || checkCandidate(groupVal) || checkCandidate(typeVal) || checkCandidate(stationVal)

          // Also check item tags array if item has tags
          if (!matches && Array.isArray(item.tags)) {
            matches = item.tags.some((t: string) => {
              const lower = t.toLowerCase()
              return lower.includes(`${targetKey}:${expectedVal}`) || lower.includes(expectedVal)
            })
          }
        } else {
          // Freeform tag or hashtag: e.g. "#cnc", "welder"
          const expected = tag.value.toLowerCase()
          if (Array.isArray(item.tags)) {
            matches = item.tags.some((t: string) => {
              const clean = t.replace(/^#/, '').toLowerCase()
              const expClean = expected.replace(/^#/, '')
              return clean === expClean || clean.includes(expClean)
            })
          }
          if (!matches) {
            // Also search string representation
            const searchable = extractSearchableStrings(item, config.searchFields)
            matches = searchable.some(s => s.toLowerCase().includes(expected))
          }
        }

        // Apply positive vs negative logic gate
        if (tag.isNegative) {
          // If negative filter matched, item is excluded
          if (matches) return false
        } else {
          // Positive filter MUST match
          if (!matches) return false
        }
      }

      // B. Check Multi-Select Facet Filters (AND between different facets, OR within same facet)
      for (const [facetKey, selectedValues] of Object.entries(facets)) {
        if (!selectedValues || selectedValues.length === 0) continue

        let facetValues: string[] = []

        // Use custom facet definition extractor if provided
        const def = config.facetDefinitions?.find(d => d.key === facetKey)
        if (def && def.getValues) {
          const res = def.getValues(item)
          if (Array.isArray(res)) facetValues = res.map(String)
          else if (res !== undefined && res !== null) facetValues = [String(res)]
        } else {
          const directVal = item[facetKey]
          if (Array.isArray(directVal)) facetValues = directVal.map(String)
          else if (directVal !== undefined && directVal !== null) facetValues = [String(directVal)]
        }

        const normalizedItemValues = facetValues.map(v => v.toLowerCase())
        const hasAnyMatch = selectedValues.some(sv => normalizedItemValues.includes(sv.toLowerCase()))

        if (!hasAnyMatch) return false
      }

      return true
    })
  })

  // ─── 2. TEXT SEARCH & MATCH SCORING ENGINE ────────────────────────────────────
  const searchedAndFilteredItems = computed<T[]>(() => {
    const base = filteredItems.value
    const query = searchQuery.value.trim().toLowerCase()

    if (!query) {
      return base
    }

    const tokens = query.split(/\s+/).filter(Boolean)

    // Score and filter each item
    const scored: { item: T; score: number }[] = []

    for (const item of base) {
      const searchableTexts = extractSearchableStrings(item, config.searchFields)
      const combinedText = searchableTexts.join(' ').toLowerCase()

      let matchesAllTokens = true
      let score = 0

      for (const token of tokens) {
        const tokenIdx = combinedText.indexOf(token)
        if (tokenIdx === -1) {
          matchesAllTokens = false
          break
        }

        // Closer to start of string = higher relevance
        if (tokenIdx === 0) score += 20
        else score += 10

        // Exact token equality on one of the individual fields gets highest bonus
        if (searchableTexts.some(t => t.toLowerCase() === token)) {
          score += 30
        }
      }

      if (matchesAllTokens) {
        scored.push({ item, score })
      }
    }

    // Sort descending by relevance score
    return scored
      .sort((a, b) => b.score - a.score)
      .map(s => s.item)
  })

  // ─── 3. DYNAMIC FACET VALUE & COUNT COMPUTATION ───────────────────────────────
  const availableFacets = computed(() => {
    if (!config.facetDefinitions || config.facetDefinitions.length === 0) return []

    const currentBase = rawItems.value

    return config.facetDefinitions.map(def => {
      const counts = new Map<string, number>()

      for (const item of currentBase) {
        let vals: string[] = []
        if (def.getValues) {
          const res = def.getValues(item)
          if (Array.isArray(res)) vals = res.map(String)
          else if (res !== undefined && res !== null) vals = [String(res)]
        } else {
          const direct = (item as any)[def.key]
          if (Array.isArray(direct)) vals = direct.map(String)
          else if (direct !== undefined && direct !== null) vals = [String(direct)]
        }

        for (const v of vals) {
          if (!v) continue
          counts.set(v, (counts.get(v) || 0) + 1)
        }
      }

      const options: FacetOption[] = Array.from(counts.entries()).map(([value, count]) => ({
        value,
        count
      })).sort((a, b) => b.count - a.count)

      return {
        key: def.key,
        label: def.label,
        options
      }
    })
  })

  // ─── 4. CONTROLS & STATE HELPERS ──────────────────────────────────────────────
  const totalCount = computed(() => rawItems.value.length)
  const matchCount = computed(() => searchedAndFilteredItems.value.length)

  const hasActiveFilters = computed(() => {
    const hasSearch = searchQuery.value.trim().length > 0
    const hasTags = tagFilters.value.length > 0
    const hasFacets = Object.values(facetFilters.value).some(arr => arr && arr.length > 0)
    return hasSearch || hasTags || hasFacets
  })

  function addTag(tag: string) {
    const trimmed = tag.trim()
    if (!trimmed) return
    if (!tagFilters.value.includes(trimmed)) {
      tagFilters.value.push(trimmed)
    }
  }

  function removeTag(tag: string) {
    tagFilters.value = tagFilters.value.filter(t => t !== tag)
  }

  function toggleFacet(facetKey: string, value: string) {
    const current = facetFilters.value[facetKey] ? [...facetFilters.value[facetKey]] : []
    const idx = current.indexOf(value)
    if (idx >= 0) {
      current.splice(idx, 1)
    } else {
      current.push(value)
    }
    facetFilters.value = {
      ...facetFilters.value,
      [facetKey]: current
    }
  }

  function clearSearch() {
    searchQuery.value = ''
  }

  function clearFilters() {
    tagFilters.value = []
    facetFilters.value = {}
  }

  function resetAll() {
    searchQuery.value = ''
    tagFilters.value = []
    facetFilters.value = {}
  }

  return {
    searchQuery,
    tagFilters,
    facetFilters,
    parsedTags,
    filteredItems,
    searchedAndFilteredItems,
    availableFacets,
    totalCount,
    matchCount,
    hasActiveFilters,
    addTag,
    removeTag,
    toggleFacet,
    clearSearch,
    clearFilters,
    resetAll
  }
}
