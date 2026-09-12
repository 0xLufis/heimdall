import type { MaybeRefOrGetter } from 'vue'

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
  enableDiagnostics?: boolean
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
  sourceTable?: 'machines' | 'nodes' | 'inventory' | 'lines' | 'global' | 'telemetry' | 'users' | string
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

// --------------------------------------------------------------------------
// Parameterized OmniSearch Types & Contracts
// --------------------------------------------------------------------------

export type SearchDataSource =
  | string // URI shorthand
  | { type: 'uri'; url: string; method?: 'GET' | 'POST'; headers?: Record<string, string> }
  | { type: 'dbConnection'; connectionName?: string; table?: string; query?: string }
  | { type: 'redisCache'; key: string; endpoint?: string }
  | { type: 'custom'; handler: (query: string, context?: any) => Promise<any> | any }
  | any // Keep any for custom handlers as requested

export type SearchTemplateName =
  | 'machine'
  | 'machines'
  | 'endpoint'
  | 'endpoints'
  | 'client-pc'
  | 'clientpc'
  | 'nodes'
  | 'clients'
  | 'inventory'
  | 'parts'
  | 'stock'
  | 'user'
  | 'users'
  | 'personas'
  | 'telemetry'
  | string

export interface FuzzySearchConfig {
  enabled?: boolean
  threshold?: number // 0.0 - 1.0 (default 0.7)
  maxDistance?: number // max Damerau-Levenshtein distance (default 2)
  searchFields?: string[]
}

export interface SearchConfig {
  debounceMs?: number
  minCharsForSuggestions?: number
  enableAutoTagging?: boolean
  maxResults?: number
  fuzzy?: FuzzySearchConfig
  enableDiagnostics?: boolean
  placeholder?: string
  allowedTagKeys?: string[]
  defaultTags?: Array<{ key: string; value: string; label?: string }>
}

export interface RankedIndexingTableRef<T = any> {
  name: string
  rank: number
  data?: MaybeRefOrGetter<T[]>
  keyField?: string
  searchFields?: string[]
  kvFields?: Record<string, string | ((item: T) => string | string[] | undefined)>
  customLookup?: (query: string, tagFilters: TagPill[]) => Promise<T[]> | T[]
  description?: string
}

export interface OmniSearchOptions<T = any> {
  dataSource?: SearchDataSource
  data?: MaybeRefOrGetter<T | T[] | Record<string, any> | Record<string, any>[]>
  template?: SearchTemplateName | any
  indexingTables?: RankedIndexingTableRef<any>[]
  config?: SearchConfig
  
  // Backwards compatibility flat properties:
  instanceId?: string
  placeholder?: string
  defaultEndpoints?: string[]
  allowedTagKeys?: string[]
  defaultTags?: Array<{ key: string; value: string; label?: string }>
  minCharsForSuggestions?: number
  debounceMs?: number
  enableAutoTagging?: boolean
  showGlobalShortcut?: boolean
  enableDiagnostics?: boolean
}
