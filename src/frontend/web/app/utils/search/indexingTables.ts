import { unref } from 'vue'
import type { TagPill, KeyLookupSuggestion, SearchGroup } from '~/types/search'

export interface RankedIndexingTable<T = any> {
  name: string
  rank: number // Lower number = higher priority / rank (1, 2, 3...)
  data?: T[] | any
  keyField?: string
  searchFields?: string[]
  kvFields?: Record<string, string | ((item: T) => string | string[] | undefined)>
  customLookup?: (query: string, tagFilters: TagPill[]) => Promise<T[]> | T[]
  description?: string
}

export const BASELINE_KNOWN_KEY_VALUES: Record<string, { values: string[]; description: string }> = {
  tech: {
    values: ['Assembly', 'Test', 'SMT', 'Welding', 'Fastening', 'Dispensing', 'Robotics'],
    description: 'Filter by engineering technology / discipline'
  },
  line: {
    values: ['Line 1', 'Line 2', 'Assy-01', 'SMT-Alpha', 'Weld-Main'],
    description: 'Filter by manufacturing production line'
  },
  status: {
    values: ['online', 'offline', 'InStorage', 'InMachine', 'UnderRepair', 'warning', 'critical'],
    description: 'Filter by operational / equipment status'
  },
  isstock: {
    values: ['true', 'false'],
    description: 'Filter between bulk stock items and serialized parts'
  },
  mfr: {
    values: ['Siemens', 'Beckhoff', 'KUKA', 'Cognex', 'Advantech', 'IFM Electronic', 'Festo', 'Fanuc', 'Omron'],
    description: 'Filter by brand / OEM equipment manufacturer'
  },
  manufacturer: {
    values: ['Siemens', 'Beckhoff', 'KUKA', 'Cognex', 'Advantech', 'IFM Electronic', 'Festo', 'Fanuc', 'Omron'],
    description: 'Filter by brand / OEM equipment manufacturer'
  },
  location: {
    values: ['Warehouse A', 'Shelf A3-4', 'Bin 42-B', 'Tool Crib', 'Production Floor A'],
    description: 'Filter by physical warehouse or shelf storage location'
  },
  type: {
    values: ['hardware', 'software', 'parts', 'stock', 'machine', 'ipc'],
    description: 'Filter by asset class type'
  },
  category: {
    values: ['Servo', 'PLC', 'IPC', 'Robot', 'Motor', 'Sensor', 'Driver', 'Camera', 'Valve', 'CPU', 'RAM'],
    description: 'Filter by component hardware category'
  }
}

export const ALL_FILTER_KEYS = [
  { key: 'line', label: 'line:', description: 'Production line or cell group' },
  { key: 'tech', label: 'tech:', description: 'Engineering discipline / technology' },
  { key: 'status', label: 'status:', description: 'Live status (online, offline, InStorage, InMachine)' },
  { key: 'mfr', label: 'mfr:', description: 'Manufacturer (Siemens, KUKA, Advantech...)' },
  { key: 'manufacturer', label: 'manufacturer:', description: 'Manufacturer brand / OEM' },
  { key: 'serial', label: 'serial:', description: 'Unique part serial number' },
  { key: 'location', label: 'location:', description: 'Storage shelf or machine location' },
  { key: 'isstock', label: 'isstock:', description: 'Bulk stock (true) vs serialized part (false)' },
  { key: 'station', label: 'station:', description: 'Target machine station ID' },
  { key: 'type', label: 'type:', description: 'hardware, software, parts, stock' },
  { key: 'ip', label: 'ip:', description: 'IP address lookup' },
  { key: 'mac', label: 'mac:', description: 'MAC address pattern' },
  { key: 'spec', label: 'spec:', description: 'Engineering parameter (15kW, 400V, 12000RPM)' },
  { key: 'cost', label: 'cost:', description: 'Valuation filter (e.g. >500k)' },
  { key: 'role', label: 'role:', description: 'User / persona organizational role' },
  { key: 'quality', label: 'quality:', description: 'Telemetry quality indicator' }
]

/**
 * Extracts a property value using dot-notation path (e.g. "manufacturer.name").
 */
function getNestedValue(obj: any, path: string): any {
  if (!obj || typeof obj !== 'object') return undefined
  const parts = path.split('.')
  let curr = obj
  for (const p of parts) {
    if (curr == null) return undefined
    curr = curr[p]
  }
  return curr
}

/**
 * Scans ranked indexing tables to dynamically harvest known key-values.
 * Tables with lower rank numbers are prioritized.
 */
export function harvestDynamicKeyValues(
  tables: RankedIndexingTable[] = [],
  seedDict: Record<string, { values: string[]; description: string }> = BASELINE_KNOWN_KEY_VALUES
): Record<string, { values: string[]; description: string }> {
  // Deep clone seed dictionary so we never mutate the baseline
  const result: Record<string, { values: string[]; description: string }> = {}
  for (const [k, v] of Object.entries(seedDict)) {
    result[k] = {
      description: v.description,
      values: [...v.values]
    }
  }

  // Sort tables by rank ascending (1 is highest priority)
  const sortedTables = [...tables].sort((a, b) => a.rank - b.rank)

  for (const table of sortedTables) {
    if (!table.kvFields) continue

    const rawData = typeof table.data === 'function' ? table.data() : unref(table.data)
    if (!Array.isArray(rawData)) continue

    for (const [key, extractorOrProp] of Object.entries(table.kvFields)) {
      if (!result[key]) {
        result[key] = {
          description: `Filter by ${key} (${table.name})`,
          values: []
        }
      }

      const existingValues = new Set(result[key].values.map(v => v.toLowerCase()))

      for (const item of rawData) {
        if (!item) continue

        let extracted: any
        if (typeof extractorOrProp === 'function') {
          extracted = extractorOrProp(item)
        } else {
          extracted = getNestedValue(item, extractorOrProp)
        }

        if (extracted == null) continue

        const valList = Array.isArray(extracted) ? extracted : [extracted]
        for (const val of valList) {
          const strVal = String(val).trim()
          if (strVal && !existingValues.has(strVal.toLowerCase())) {
            existingValues.add(strVal.toLowerCase())
            // Insert newly discovered values at front of list (prioritized by table rank)
            result[key].values.unshift(strVal)
          }
        }
      }
    }
  }

  return result
}

/**
 * Constructs KeyLookupSuggestion array matching a typed key and partial value.
 */
export function getSuggestionsForKey(
  key: string,
  partialVal: string,
  knownKvs: Record<string, { values: string[]; description: string }>,
  sourceTable = 'omni'
): KeyLookupSuggestion[] {
  const cleanKey = key.toLowerCase()
  const known = knownKvs[cleanKey]
  if (!known) return []

  const lowerPartial = partialVal.toLowerCase()
  return known.values
    .filter(v => v.toLowerCase().includes(lowerPartial))
    .map(v => ({
      key: cleanKey,
      value: v,
      description: known.description,
      sourceTable
    }))
}

/**
 * Organizes search keys into structured SearchGroup[] categories.
 */
export function buildDynamicSearchKeyGroups(
  knownKvs: Record<string, { values: string[]; description: string }>
): SearchGroup[] {
  const allKeys = Object.keys(knownKvs)

  const coreKeys = ['line', 'tech', 'status', 'station', 'type', 'mfr', 'manufacturer'].filter(k => allKeys.includes(k))
  const invKeys = ['serial', 'location', 'isstock', 'category', 'cost'].filter(k => allKeys.includes(k))
  const teleKeys = ['ip', 'mac', 'spec', 'quality', 'unit', 'key'].filter(k => allKeys.includes(k))
  const otherKeys = allKeys.filter(k => !coreKeys.includes(k) && !invKeys.includes(k) && !teleKeys.includes(k))

  const groups: SearchGroup[] = [
    { group: 'Core Attributes', keys: coreKeys },
    { group: 'Inventory & Parts', keys: invKeys },
    { group: 'Telemetry & Specs', keys: teleKeys }
  ]

  if (otherKeys.length > 0) {
    groups.push({ group: 'Custom Facets', keys: otherKeys })
  }

  return groups
}
