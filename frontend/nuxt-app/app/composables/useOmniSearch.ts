import { ref, computed, watch } from 'vue'
import { watchDebounced } from '@vueuse/core'
import type { 
  TagPill, 
  AutoTagResult, 
  SearchInstanceConfig, 
  SearchResultItem, 
  SearchGroup 
} from '~/types/search'
import { autoTagEngine } from '~/utils/search/AutoTagEngine'

const DEFAULT_CONFIG: SearchInstanceConfig = {
  instanceId: 'dashboard',
  placeholder: 'Search stations, controllers, inventory, specs... (Cmd+K)',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  allowedTagKeys: ['manufacturer', 'category', 'status', 'station', 'ip', 'mac', 'spec', 'cost', 'type'],
  minCharsForSuggestions: 2,
  debounceMs: 250,
  enableAutoTagging: true,
  showGlobalShortcut: true
}

export const useOmniSearch = (customConfig: Partial<SearchInstanceConfig> = {}) => {
  const config = { ...DEFAULT_CONFIG, ...customConfig }

  const rawInput = ref('')
  const tags = ref<TagPill[]>(config.defaultTags ? [...config.defaultTags.map(t => ({ id: `tag-${t.key}-${t.value}`, ...t, removable: true }))] : [])
  const autoSuggestions = ref<AutoTagResult[]>([])
  const results = ref<SearchResultItem[]>([])
  const searchKeyGroups = ref<SearchGroup[]>([])
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
      }
    } catch {
      searchKeyGroups.value = [
        { group: 'Core Attributes', keys: ['manufacturer', 'category', 'status', 'station', 'type'] },
        { group: 'Telemetry & Specs', keys: ['ip', 'mac', 'spec', 'cost'] }
      ]
    }
  }

  // Execute search against configured endpoints
  const executeSearch = async (queryStr: string = effectiveQueryString.value) => {
    if (!queryStr && tags.value.length === 0) {
      results.value = []
      return
    }

    isLoading.value = true
    try {
      const endpoint = config.defaultEndpoints?.[0] || '/api/proxy/inventory/search'
      const queryParam = encodeURIComponent(queryStr)
      const res = await $fetch<any[]>(`${endpoint}?query=${queryParam}`)
      
      results.value = (res || []).map((item: any) => ({
        id: item.id || item.Id,
        name: item.name || item.hostname || item.customIdentifier || 'Unknown',
        displayName: item.displayName || item.name,
        itemType: item.itemType || 'Asset',
        typeLabel: item.typeLabel || item.itemType || 'Equipment',
        manufacturerName: item.manufacturerName || item.manufacturer?.name,
        subtitle: item.customIdentifier || item.macAddress || item.serialNumber,
        status: item.isOnline !== undefined ? (item.isOnline ? 'online' : 'offline') : undefined,
        metadata: item.metadata
      }))
    } catch {
      results.value = []
    } finally {
      isLoading.value = false
    }
  }

  // Process input for auto-tagging
  const handleInputChange = (value: string) => {
    rawInput.value = value

    if (!config.enableAutoTagging || value.length < (config.minCharsForSuggestions || 2)) {
      autoSuggestions.value = []
      return
    }

    // Check for explicit key:value tags typed in
    const { tags: extractedTags, remainingText } = autoTagEngine.parseExplicitTags(value)
    if (extractedTags.length > 0) {
      for (const t of extractedTags) {
        addTag(t)
      }
      rawInput.value = remainingText
      return
    }

    // Run regex + fuzzy dictionary auto-tagging
    const { autoTags } = autoTagEngine.analyzeText(value)
    
    // Filter out tags that are already in active tags list
    autoSuggestions.value = autoTags.filter(
      at => !tags.value.some(existing => existing.key === at.tag.key && existing.value.toLowerCase() === at.tag.value.toLowerCase())
    )
  }

  // React to rawInput changes
  watch(rawInput, (newVal) => {
    handleInputChange(newVal)
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
      color: tag.color || (key === 'status' ? 'emerald' : key === 'manufacturer' ? 'blue' : 'indigo'),
      removable: true
    })

    // Clear suggestion if accepted
    autoSuggestions.value = autoSuggestions.value.filter(
      s => !(s.tag.key === key && s.tag.value.toLowerCase() === value.toLowerCase())
    )

    // Re-execute search with new tags
    executeSearch()
  }

  const removeTag = (tagId: string) => {
    tags.value = tags.value.filter(t => t.id !== tagId)
    executeSearch()
  }

  const clearAllTags = () => {
    tags.value = []
    rawInput.value = ''
    results.value = []
    autoSuggestions.value = []
  }

  // Debounced live search
  watchDebounced(
    rawInput,
    () => {
      if (rawInput.value.length >= (config.minCharsForSuggestions || 2) || tags.value.length > 0) {
        executeSearch()
      } else if (tags.value.length === 0) {
        results.value = []
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
    searchKeyGroups,
    isLoading,
    isDropdownOpen,
    effectiveQueryString,
    config,
    handleInputChange,
    addTag,
    removeTag,
    clearAllTags,
    executeSearch,
    fetchSearchKeys
  }
}
