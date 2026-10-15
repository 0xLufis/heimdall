/**
 * Unified Context System & Tag Engine for Heimdall Search
 * 
 * Defines target data sources (tables, views, JSON), scopes queries by context,
 * shrinks active search space according to applied tags, and parses tag:value
 * and !tag:value (inverted) filters.
 */

export interface SearchSource<T = any> {
  id: string
  name: string
  entityType: string // e.g., 'Machine', 'ClientPc', 'Ticket', 'InventoryItem', 'AssetTemplate', 'User'
  indicatorLabel: string // Short indicator, e.g. 'MCH', 'IPC', 'TKT', 'PART', 'USR'
  indicatorColor: string // Tailwind color class, e.g. 'bg-blue-500/10 text-blue-500 border-blue-500/20'
  data: T[] | (() => T[] | Promise<T[]>)
  indexedKey: string // Primary unique ID field name (e.g. 'id')
  columns: string[] // List of searchable & indexable fields
  titleField?: string
  subtitleField?: string
  linkGenerator?: (item: T) => string
}

export interface SearchTagFilter {
  key: string
  value: string
  inverted: boolean // true if prefixed with '!' e.g. !manufacturer:Siemens
  raw: string
}

export interface SearchContext {
  id: string
  title: string
  sources: SearchSource[]
  allowedTags?: string[]
}

export interface ParsedSearchQuery {
  raw: string
  tags: SearchTagFilter[]
  freeText: string
  activeIncompleteTag?: {
    key: string
    partialValue: string
    inverted: boolean
    cursorIndex: number
  }
}

export interface ContextSearchResult {
  id: string
  title: string
  subtitle?: string
  entityType: string
  indicatorLabel: string
  indicatorColor: string
  link?: string
  rawItem: any
  score: number
  matchedIndices?: number[]
  highlightedTitle?: string
}

/**
 * Parses raw search query string into structured tags, negation tags, and free-text tokens.
 * Also detects if the user is currently typing a tag (e.g. 'status:' or '!mfr:sie') to trigger completion.
 */
export function parseSearchQuery(query: string, cursorPosition?: number): ParsedSearchQuery {
  if (!query) {
    return { raw: '', tags: [], freeText: '' }
  }

  const raw = query
  const tags: SearchTagFilter[] = []
  const freeTextTokens: string[] = []

  // Tokenize preserving spaces when quoted, but simple space-split for standard terms
  const regex = /(!?[a-zA-Z0-9_\-]+):([^\s]*)|"([^"]*)"|(\S+)/g
  let match: RegExpExecArray | null

  let activeIncompleteTag: ParsedSearchQuery['activeIncompleteTag'] = undefined
  const cursor = cursorPosition ?? raw.length

  while ((match = regex.exec(raw)) !== null) {
    const fullMatch = match[0]
    const matchStart = match.index
    const matchEnd = match.index + fullMatch.length

    if (match[1] !== undefined) {
      // Tag match (e.g. !manufacturer:Siemens or status:)
      let rawKey = match[1]
      const inverted = rawKey.startsWith('!')
      const key = inverted ? rawKey.substring(1).toLowerCase() : rawKey.toLowerCase()
      const val = match[2] ?? ''

      // Check if cursor is inside or right at the end of this tag token
      if (cursor >= matchStart && cursor <= matchEnd) {
        activeIncompleteTag = {
          key,
          partialValue: val,
          inverted,
          cursorIndex: cursor
        }
      }

      // If value is not empty, record as applied tag filter
      if (val.length > 0) {
        tags.push({
          key,
          value: val,
          inverted,
          raw: fullMatch
        })
      }
    } else if (match[3] !== undefined) {
      // Quoted phrase
      freeTextTokens.push(match[3])
    } else if (match[4] !== undefined) {
      // Free text word
      freeTextTokens.push(match[4])
    }
  }

  return {
    raw,
    tags,
    freeText: freeTextTokens.join(' ').trim(),
    activeIncompleteTag
  }
}

/**
 * Dynamically shrinks the search context based on applied tags.
 * For example, if a tag specifies `type:machine` or `entity:ticket`, sources
 * not matching this entity type are removed, eliminating incompatible inputs.
 */
export function shrinkContext(context: SearchContext, activeTags: SearchTagFilter[]): SearchContext {
  if (!activeTags || activeTags.length === 0) {
    return context
  }

  let filteredSources = [...context.sources]

  for (const tag of activeTags) {
    const k = tag.key.toLowerCase()
    const v = tag.value.toLowerCase()

    if (k === 'type' || k === 'entity' || k === 'table' || k === 'source') {
      filteredSources = filteredSources.filter(source => {
        const matches =
          source.entityType.toLowerCase() === v ||
          source.id.toLowerCase() === v ||
          source.indicatorLabel.toLowerCase() === v

        return tag.inverted ? !matches : matches
      })
    }
  }

  // Calculate remaining allowed tags from remaining sources
  const remainingTags = new Set<string>()
  for (const s of filteredSources) {
    for (const c of s.columns) {
      remainingTags.add(c.toLowerCase())
    }
    remainingTags.add('type')
    remainingTags.add('entity')
  }

  return {
    ...context,
    sources: filteredSources,
    allowedTags: Array.from(remainingTags)
  }
}
