/**
 * Table Indexing and Inverted Index Engine for Heimdall OmniSearch
 * 
 * Provides compile-time and runtime table indexing:
 * indexTable(source, indexedKey, columns)
 * Exposes unique column contents per tag key to power the code-completion popup.
 */

import type { SearchSource, SearchTagFilter, ParsedSearchQuery, ContextSearchResult } from './searchContext'

export interface SearchIndexTable {
  sourceId: string
  entityType: string
  indicatorLabel: string
  indicatorColor: string
  indexedKey: string
  columns: string[]
  tagValues: Map<string, Set<string>> // tagKey (lowercase) -> Set of unique values
  records: Map<string, any> // id -> raw record
  invertedIndex: Map<string, Map<string, string[]>> // column -> value -> recordIds
  titleField?: string
  subtitleField?: string
  linkGenerator?: (item: any) => string
}

function getFieldValue(obj: any, path: string): any {
  if (!obj || typeof obj !== 'object') return undefined
  if (obj[path] !== undefined) return obj[path]
  const parts = path.split('.')
  let curr = obj
  for (const p of parts) {
    if (curr == null) return undefined
    curr = curr[p]
  }
  return curr
}

/**
 * Builds an indexing table from a search source.
 * Extracts unique values for each specified column and compiles an inverted lookup index.
 */
export function indexTable(
  source: SearchSource,
  indexedKey: string = source.indexedKey,
  columns: string[] = source.columns
): SearchIndexTable {
  const data = typeof source.data === 'function' ? (source.data as any)() : source.data
  const recordsList = Array.isArray(data) ? data : []

  const tagValues = new Map<string, Set<string>>()
  const records = new Map<string, any>()
  const invertedIndex = new Map<string, Map<string, string[]>>()

  for (const col of columns) {
    tagValues.set(col.toLowerCase(), new Set<string>())
    invertedIndex.set(col.toLowerCase(), new Map<string, string[]>())
  }

  // Also index standard implicit tags
  tagValues.set('type', new Set([source.entityType, source.indicatorLabel.toLowerCase()]))
  tagValues.set('entity', new Set([source.entityType, source.indicatorLabel.toLowerCase()]))

  for (const item of recordsList) {
    const id = String(getFieldValue(item, indexedKey) ?? Math.random().toString())
    records.set(id, item)

    for (const col of columns) {
      const val = getFieldValue(item, col)
      if (val === undefined || val === null) continue

      const colKey = col.toLowerCase()
      const colMap = invertedIndex.get(colKey)!
      const colSet = tagValues.get(colKey)!

      if (Array.isArray(val)) {
        for (const v of val) {
          const sVal = String(v).trim()
          if (!sVal) continue
          colSet.add(sVal)
          const lower = sVal.toLowerCase()
          if (!colMap.has(lower)) colMap.set(lower, [])
          colMap.get(lower)!.push(id)
        }
      } else {
        const sVal = String(val).trim()
        if (!sVal) continue
        colSet.add(sVal)
        const lower = sVal.toLowerCase()
        if (!colMap.has(lower)) colMap.set(lower, [])
        colMap.get(lower)!.push(id)
      }
    }
  }

  return {
    sourceId: source.id,
    entityType: source.entityType,
    indicatorLabel: source.indicatorLabel,
    indicatorColor: source.indicatorColor,
    indexedKey,
    columns,
    tagValues,
    records,
    invertedIndex,
    titleField: source.titleField,
    subtitleField: source.subtitleField,
    linkGenerator: source.linkGenerator
  }
}

/**
 * Returns available unique values for a given tag key across a list of indexed tables.
 */
export function getIndexedTagValues(indexTables: SearchIndexTable[], tagKey: string): string[] {
  const normKey = tagKey.toLowerCase()
  const unique = new Set<string>()

  for (const table of indexTables) {
    const set = table.tagValues.get(normKey)
    if (set) {
      for (const val of set) {
        unique.add(val)
      }
    }
  }

  return Array.from(unique).sort()
}

/**
 * Checks whether an item passes all positive and negative tag filters.
 */
export function matchesTagFilters(item: any, tags: SearchTagFilter[], table: SearchIndexTable): boolean {
  if (!tags || tags.length === 0) return true

  for (const tag of tags) {
    const k = tag.key.toLowerCase()
    const targetVal = tag.value.toLowerCase()

    // Handle type/entity tag
    if (k === 'type' || k === 'entity' || k === 'table') {
      const match =
        table.entityType.toLowerCase() === targetVal ||
        table.sourceId.toLowerCase() === targetVal ||
        table.indicatorLabel.toLowerCase() === targetVal

      if (tag.inverted ? match : !match) {
        return false
      }
      continue
    }

    const fieldVal = getFieldValue(item, k)
    let isMatch = false

    if (fieldVal !== undefined && fieldVal !== null) {
      if (Array.isArray(fieldVal)) {
        isMatch = fieldVal.some(v => String(v).toLowerCase().includes(targetVal))
      } else {
        const s = String(fieldVal).toLowerCase()
        isMatch = s.includes(targetVal) || s === targetVal
      }
    }

    // Invert condition if '!' was present
    if (tag.inverted) {
      if (isMatch) return false
    } else {
      if (!isMatch) return false
    }
  }

  return true
}

/**
 * Formats a default title for a result item.
 */
export function formatResultTitle(item: any, table: SearchIndexTable): string {
  if (table.titleField) {
    const custom = getFieldValue(item, table.titleField)
    if (custom) return String(custom)
  }
  return item.name || item.title || item.ticketNumber || item.hostname || item.serialNumber || item.id || 'Untitled Entity'
}

/**
 * Formats a default subtitle for a result item.
 */
export function formatResultSubtitle(item: any, table: SearchIndexTable): string {
  if (table.subtitleField) {
    const custom = getFieldValue(item, table.subtitleField)
    if (custom) return String(custom)
  }
  const parts: string[] = []
  if (item.status) parts.push(`Status: ${item.status}`)
  if (item.manufacturer) parts.push(`Mfr: ${item.manufacturer}`)
  if (item.location) parts.push(`Loc: ${item.location}`)
  if (item.department) parts.push(`Dept: ${item.department}`)
  if (item.priority) parts.push(`Priority: ${item.priority}`)
  return parts.join(' • ')
}
