/**
 * Unified Context System & Tag Engine for Heimdall Search
 * 
 * Defines target data sources (tables, views, JSON), scopes queries by context,
 * shrinks active search space according to applied tags, and parses tag:value
 * and !tag:value (inverted) filters.
 */

export type TagModifier = 'AND' | 'OR' | 'XOR' | 'NOT' | 'NONE'

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
  values?: string[]
  modifier?: TagModifier
  rawModifier?: string
  separator?: '#' | ':'
}

export interface SearchContext {
  id: string
  title: string
  sources: SearchSource[]
  allowedTags?: string[]
}

export interface ActiveIncompleteTag {
  key: string
  partialValue: string
  inverted: boolean
  cursorIndex: number
  modifier?: TagModifier
  rawModifier?: string
  separator?: '#' | ':'
  completedValues?: string[]
  tokenStart?: number
  tokenEnd?: number
  rawToken?: string
}

export interface ParsedSearchQuery {
  raw: string
  tags: SearchTagFilter[]
  freeText: string
  activeIncompleteTag?: ActiveIncompleteTag
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
 * Normalizes raw string modifier to TagModifier enum.
 */
export function normalizeTagModifier(rawMod?: string): TagModifier {
  if (!rawMod) return 'NONE'
  const upper = rawMod.toUpperCase()
  if (upper === '!' || upper === '-' || upper === 'NOT') return 'NOT'
  if (upper === '+' || upper === '&' || upper === 'AND') return 'AND'
  if (upper === '|' || upper === 'OR') return 'OR'
  if (upper === '^' || upper === 'XOR') return 'XOR'
  return 'NONE'
}

/**
 * Parses raw search query string into structured tags, negation tags, and free-text tokens.
 * Supports {modifier}{tag}#{value1},{valueN}{delimiter} and legacy tag:value syntax.
 * Also detects if the user is currently typing a tag (e.g. 'status#', '!mfr#sie', 'status:online').
 */
export function parseSearchQuery(query: string, cursorPosition?: number): ParsedSearchQuery {
  if (!query) {
    return { raw: '', tags: [], freeText: '' }
  }

  const raw = query
  const tags: SearchTagFilter[] = []
  const freeTextTokens: string[] = []

  // Tokenize preserving spaces when quoted, but simple space-split for standard terms.
  // Matches {modifier}{tag}#{values} or {modifier}{tag}:{values}
  const tokenRegex = /(?<=^|\s)(!|-|\+|&|\||\^|NOT|AND|OR|XOR|not|and|or|xor)?([a-zA-Z0-9_\-]+)([#:])([^\s]*)|"([^"]*)"|(\S+)/gi
  let match: RegExpExecArray | null

  let activeIncompleteTag: ParsedSearchQuery['activeIncompleteTag'] = undefined
  const cursor = cursorPosition ?? raw.length

  while ((match = tokenRegex.exec(raw)) !== null) {
    const fullMatch = match[0]
    const matchStart = match.index
    const matchEnd = match.index + fullMatch.length

    if (match[2] !== undefined && match[3] !== undefined) {
      // Tag match: group 1 = modifier, group 2 = key, group 3 = separator, group 4 = values
      const rawModifier = match[1] || ''
      const key = match[2].toLowerCase()
      const separator = match[3] as '#' | ':'
      const rawValues = match[4] ?? ''
      const modifier = normalizeTagModifier(rawModifier)
      const inverted = modifier === 'NOT'

      // Split comma-separated values
      const valSegments = rawValues.split(',')
      const completedValues = valSegments.slice(0, -1).map(s => s.trim()).filter(Boolean)
      const partialValue = valSegments[valSegments.length - 1] ?? ''

      // Check if cursor is inside or right at the end of this tag token
      if (cursor >= matchStart && cursor <= matchEnd) {
        activeIncompleteTag = {
          key,
          partialValue,
          inverted,
          cursorIndex: cursor,
          modifier,
          rawModifier,
          separator,
          completedValues,
          tokenStart: matchStart,
          tokenEnd: matchEnd,
          rawToken: fullMatch
        }
      }

      // If value is not empty, record as applied tag filter
      const parsedValues = rawValues.split(',').map(s => s.trim()).filter(Boolean)
      if (parsedValues.length > 0) {
        const tagFilter: SearchTagFilter = {
          key,
          value: parsedValues.join(','),
          inverted,
          raw: fullMatch
        }
        // Define enhanced properties as non-enumerable for strict toEqual test compatibility
        Object.defineProperties(tagFilter, {
          values: { value: parsedValues, enumerable: false, writable: true, configurable: true },
          modifier: { value: modifier, enumerable: false, writable: true, configurable: true },
          rawModifier: { value: rawModifier, enumerable: false, writable: true, configurable: true },
          separator: { value: separator, enumerable: false, writable: true, configurable: true }
        })
        tags.push(tagFilter)
      }
    } else if (match[5] !== undefined) {
      // Quoted phrase
      freeTextTokens.push(match[5])
    } else if (match[6] !== undefined) {
      // Free text word
      freeTextTokens.push(match[6])
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
 * For example, if a tag specifies `type#machine` or `entity:ticket`, sources
 * not matching this entity type are removed, eliminating incompatible inputs.
 */
export function shrinkContext(context: SearchContext, activeTags: SearchTagFilter[]): SearchContext {
  if (!activeTags || activeTags.length === 0) {
    return context
  }

  let filteredSources = [...context.sources]

  for (const tag of activeTags) {
    const k = tag.key.toLowerCase()
    const values = (tag.values && tag.values.length > 0)
      ? tag.values.map(v => v.toLowerCase())
      : [tag.value.toLowerCase()]
    const modifier = tag.modifier || (tag.inverted ? 'NOT' : 'AND')

    if (k === 'type' || k === 'entity' || k === 'table' || k === 'source') {
      filteredSources = filteredSources.filter(source => {
        const sourceKeys = [
          source.entityType.toLowerCase(),
          source.id.toLowerCase(),
          source.indicatorLabel.toLowerCase()
        ]

        const matchCount = values.filter(v => sourceKeys.some(sk => sk === v || sk.includes(v))).length

        if (modifier === 'NOT' || tag.inverted) {
          return matchCount === 0
        } else if (modifier === 'OR') {
          return matchCount > 0
        } else if (modifier === 'XOR') {
          return matchCount === 1
        } else {
          return matchCount === values.length || matchCount > 0
        }
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
