export interface TagPill {
  id: string
  key: string
  value: string
  label?: string
  color?: string
  isAutoDetected?: boolean
  removable?: boolean
}

export interface AutoTagResult {
  tag: TagPill
  confidence: number
  matchedSubstring: string
  source: 'regex' | 'fuzzy_dict' | 'token_classifier'
}

export interface SearchInstanceConfig {
  instanceId: 'dashboard' | 'inventory' | 'clients' | 'map' | 'tickets' | 'machines' | 'nodes' | 'global' | string
  placeholder?: string
  defaultEndpoints?: string[]
  allowedTagKeys?: string[]
  defaultTags?: Array<{ key: string; value: string; label?: string }>
  includedDescriptors?: string[]
  minCharsForSuggestions?: number
  debounceMs?: number
  enableAutoTagging?: boolean
  showGlobalShortcut?: boolean
}

export interface SearchResultItem {
  id: string
  name: string
  displayName?: string
  itemType: string
  typeLabel?: string
  manufacturerName?: string | null
  subtitle?: string
  link?: string
  status?: string
  metadata?: Record<string, any>
  sourceTable?: 'machines' | 'nodes' | 'inventory' | 'lines' | 'global' | string
  isCrossTable?: boolean
}

export interface KeyLookupSuggestion {
  key: string
  value: string
  description?: string
  sourceTable?: string
  count?: number
}

export interface SearchGroup {
  group: string
  keys?: string[]
  items?: SearchResultItem[]
}

export interface OmniSearchState {
  rawQuery: string
  freeText: string
  tags: TagPill[]
  autoSuggestions: AutoTagResult[]
  results: SearchResultItem[]
  isLoading: boolean
  isOpen: boolean
}
