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
 * Checks whether a single value matches a field on the item or the table.
 */
function itemMatchesTagValue(item: any, tagKey: string, targetVal: string, table: SearchIndexTable): boolean {
  const normTarget = targetVal.toLowerCase().trim()
  if (!normTarget) return false

  // Handle type/entity tag
  if (tagKey === 'type' || tagKey === 'entity' || tagKey === 'table') {
    return (
      table.entityType.toLowerCase() === normTarget ||
      table.sourceId.toLowerCase() === normTarget ||
      table.indicatorLabel.toLowerCase() === normTarget
    )
  }

  // Handle field matching including common industrial aliases
  const possibleKeys = [tagKey]
  if (tagKey === 'mfr') possibleKeys.push('manufacturer')
  else if (tagKey === 'manufacturer') possibleKeys.push('mfr')
  else if (tagKey === 'ip') possibleKeys.push('ipAddress')
  else if (tagKey === 'ipaddress') possibleKeys.push('ip')
  else if (tagKey === 'mac') possibleKeys.push('macAddress')
  else if (tagKey === 'macaddress') possibleKeys.push('mac')
  else if (tagKey === 'serial') possibleKeys.push('serialNumber')
  else if (tagKey === 'serialnumber') possibleKeys.push('serial')
  else if (tagKey === 'os') possibleKeys.push('osVersion')
  else if (tagKey === 'osversion') possibleKeys.push('os')
  else if (tagKey === 'ticket') possibleKeys.push('ticketNumber')
  else if (tagKey === 'ticketnumber') possibleKeys.push('ticket')
  else if (tagKey === 'host') possibleKeys.push('hostname')
  else if (tagKey === 'hostname') possibleKeys.push('host')
  else if (tagKey === 'loc') possibleKeys.push('location')
  else if (tagKey === 'location') possibleKeys.push('loc')

  for (const k of possibleKeys) {
    const fieldVal = getFieldValue(item, k)
    if (fieldVal !== undefined && fieldVal !== null) {
      if (Array.isArray(fieldVal)) {
        if (fieldVal.some(v => String(v).toLowerCase().includes(normTarget))) {
          return true
        }
      } else {
        const s = String(fieldVal).toLowerCase()
        if (s.includes(normTarget) || s === normTarget) {
          return true
        }
      }
    }
  }

  return false
}

/**
 * Checks whether an item passes all positive, negative, and relational tag filters (AND, OR, XOR, NOT).
 */
export function matchesTagFilters(item: any, tags: SearchTagFilter[], table: SearchIndexTable): boolean {
  if (!tags || tags.length === 0) return true

  for (const tag of tags) {
    const k = tag.key.toLowerCase()
    const values = (tag.values && tag.values.length > 0)
      ? tag.values
      : (tag.value ? tag.value.split(',').map(s => s.trim()).filter(Boolean) : [])

    if (values.length === 0) continue

    const modifier = tag.modifier || (tag.inverted ? 'NOT' : 'NONE')
    const matchCount = values.filter(v => itemMatchesTagValue(item, k, v, table)).length

    if (modifier === 'NOT' || tag.inverted) {
      // Negating: item must NOT match ANY of the comma-separated values
      if (matchCount > 0) {
        return false
      }
    } else if (modifier === 'OR') {
      // OR: item must match AT LEAST ONE of the comma-separated values
      if (matchCount === 0) {
        return false
      }
    } else if (modifier === 'XOR') {
      // XOR: item must match EXACTLY ONE of the comma-separated values
      if (matchCount !== 1) {
        return false
      }
    } else {
      // AND or NONE (default): item must match ALL values specified
      if (matchCount < values.length) {
        return false
      }
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
