import { ref, computed, watch } from 'vue'
import { watchDebounced } from '@vueuse/core'
import type { 
  TagPill, 
  AutoTagResult, 
  SearchInstanceConfig, 
  SearchResultItem, 
  SearchGroup,
  KeyLookupSuggestion
} from '~/types/search'
import { autoTagEngine } from '~/utils/search/AutoTagEngine'

const KNOWN_KEY_VALUES: Record<string, { values: string[]; description: string }> = {
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

const ALL_FILTER_KEYS = [
  { key: 'line', label: 'line:', description: 'Production line or cell group' },
  { key: 'tech', label: 'tech:', description: 'Engineering discipline / technology' },
  { key: 'status', label: 'status:', description: 'Live status (online, offline, InStorage, InMachine)' },
  { key: 'mfr', label: 'mfr:', description: 'Manufacturer (Siemens, KUKA, Advantech...)' },
  { key: 'serial', label: 'serial:', description: 'Unique part serial number' },
  { key: 'location', label: 'location:', description: 'Storage shelf or machine location' },
  { key: 'isstock', label: 'isstock:', description: 'Bulk stock (true) vs serialized part (false)' },
  { key: 'station', label: 'station:', description: 'Target machine station ID' },
  { key: 'type', label: 'type:', description: 'hardware, software, parts, stock' },
  { key: 'ip', label: 'ip:', description: 'IP address lookup' },
  { key: 'mac', label: 'mac:', description: 'MAC address pattern' },
  { key: 'spec', label: 'spec:', description: 'Engineering parameter (15kW, 400V, 12000RPM)' },
  { key: 'cost', label: 'cost:', description: 'Valuation filter (e.g. >500k)' }
]

const DEFAULT_CONFIG: SearchInstanceConfig = {
  instanceId: 'dashboard',
  placeholder: 'Search stations, controllers, inventory, specs... (Cmd+K)',
  defaultEndpoints: ['/api/proxy/inventory/search'],
  allowedTagKeys: ['line', 'tech', 'status', 'mfr', 'manufacturer', 'serial', 'location', 'category', 'station', 'type', 'isstock', 'ip', 'mac', 'spec', 'cost'],
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
  const primaryResults = ref<SearchResultItem[]>([])
  const crossTableResults = ref<SearchResultItem[]>([])
  const matchingKeys = ref<typeof ALL_FILTER_KEYS>([])
  const valueSuggestions = ref<KeyLookupSuggestion[]>([])
  const activePendingKey = ref<string>('')
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
        { group: 'Core Attributes', keys: ['line', 'tech', 'status', 'station', 'type', 'mfr'] },
        { group: 'Inventory & Parts', keys: ['serial', 'location', 'isstock', 'category'] },
        { group: 'Telemetry & Specs', keys: ['ip', 'mac', 'spec', 'cost'] }
      ]
    }
  }

  // Execute multi-tier search across instance primary and cross-table indexing
  const executeSearch = async (queryStr: string = effectiveQueryString.value) => {
    if (!queryStr && tags.value.length === 0) {
      results.value = []
      primaryResults.value = []
      crossTableResults.value = []
      return
    }

    isLoading.value = true
    try {
      const cleanQ = queryStr.toLowerCase().trim()
      const isMachinesInstance = config.instanceId === 'machines'
      const isNodesInstance = config.instanceId === 'nodes' || config.instanceId === 'clients'

      let primary: SearchResultItem[] = []
      let cross: SearchResultItem[] = []

      if (isMachinesInstance) {
        // Stage 1 Primary: Machines / Stations
        try {
          const machines = await $fetch<any[]>('/api/proxy/v1/machine')
          if (Array.isArray(machines)) {
            const filtered = machines.filter(m => {
              if (!cleanQ) return true
              const nameMatch = (m.name || '').toLowerCase().includes(cleanQ)
              const dispMatch = (m.displayName || '').toLowerCase().includes(cleanQ)
              const identMatch = (m.customIdentifier || '').toLowerCase().includes(cleanQ)
              const typeMatch = (m.machineType || '').toLowerCase().includes(cleanQ)
              const groupMatch = (m.groupId || '').toLowerCase().includes(cleanQ)
              return nameMatch || dispMatch || identMatch || typeMatch || groupMatch
            })
            primary = filtered.map(m => ({
              id: m.id,
              name: m.name || m.displayName || 'Station',
              displayName: m.displayName,
              itemType: 'Machine',
              typeLabel: m.machineType || 'Station',
              subtitle: `Line: ${m.groupId || 'Default'} • Controllers: ${m.controllers?.length || 0}`,
              status: 'online',
              sourceTable: 'machines',
              isCrossTable: false,
              link: `/dashboard/machines?id=${m.id}`
            }))
          }
        } catch {
          // Machine fallback
        }

        // Stage 3 Cross-table: Inventory / Parts installed on machines
        try {
          const inv = await $fetch<any>(`/api/inventory/filter?query=${encodeURIComponent(queryStr)}`)
          if (inv && Array.isArray(inv.items)) {
            cross = inv.items.slice(0, 10).map((item: any) => ({
              id: item.id,
              name: item.name || item.customIdentifier || 'Asset',
              displayName: item.displayName,
              itemType: item.itemType || 'Asset',
              typeLabel: item.itemType === 'hardware' ? 'Component' : 'License',
              manufacturerName: item.manufacturer?.name,
              subtitle: item.serialNumber ? `SN: ${item.serialNumber}` : undefined,
              sourceTable: 'inventory',
              isCrossTable: true,
              link: `/dashboard/inventory`
            }))
          }
        } catch {
          // Cross fallback
        }
      } else if (isNodesInstance) {
        // Stage 1 Primary: Nodes / Client PCs
        try {
          const pcs = await $fetch<any[]>('/api/proxy/v1/ClientPc')
          if (Array.isArray(pcs)) {
            const filtered = pcs.filter(p => {
              if (!cleanQ) return true
              return (p.hostname || '').toLowerCase().includes(cleanQ) ||
                (p.macAddress || '').toLowerCase().includes(cleanQ) ||
                (p.ipAddress || '').toLowerCase().includes(cleanQ)
            })
            primary = filtered.map(p => ({
              id: p.id,
              name: p.hostname || p.name || 'Node',
              displayName: p.displayName,
              itemType: 'ClientPc',
              typeLabel: 'Industrial PC',
              subtitle: `IP: ${p.ipAddress || 'DHCP'} • MAC: ${p.macAddress || 'N/A'}`,
              status: p.lastSeen ? 'online' : 'offline',
              sourceTable: 'nodes',
              isCrossTable: false,
              link: '/dashboard/clients'
            }))
          }
        } catch {}

        // Stage 3 Cross-table: Machines assigned to nodes
        try {
          const machines = await $fetch<any[]>('/api/proxy/v1/machine')
          if (Array.isArray(machines)) {
            cross = machines.filter(m => (m.name || '').toLowerCase().includes(cleanQ)).slice(0, 5).map(m => ({
              id: m.id,
              name: m.name,
              itemType: 'Machine',
              typeLabel: 'Station',
              sourceTable: 'machines',
              isCrossTable: true,
              link: '/dashboard/machines'
            }))
          }
        } catch {}
      } else {
        // Inventory or Global Search
        const endpoint = config.defaultEndpoints?.[0] || '/api/proxy/inventory/search'
        const queryParam = encodeURIComponent(queryStr)
        try {
          const res = await $fetch<any[]>(`${endpoint}?query=${queryParam}`)
          primary = (res || []).map((item: any) => ({
            id: item.id || item.Id,
            name: item.name || item.hostname || item.customIdentifier || 'Unknown',
            displayName: item.displayName || item.name,
            itemType: item.itemType || 'Asset',
            typeLabel: item.typeLabel || item.itemType || 'Equipment',
            manufacturerName: item.manufacturerName || item.manufacturer?.name,
            subtitle: item.customIdentifier || item.macAddress || item.serialNumber,
            status: item.isOnline !== undefined ? (item.isOnline ? 'online' : 'offline') : undefined,
            sourceTable: 'inventory',
            isCrossTable: false,
            metadata: item.metadata
          }))
        } catch {
          // Fallback to local filter API
          try {
            const res = await $fetch<any>(`/api/inventory/filter?query=${queryParam}`)
            if (res && Array.isArray(res.items)) {
              primary = res.items.map((item: any) => ({
                id: item.id,
                name: item.name,
                displayName: item.displayName,
                itemType: item.itemType || 'Asset',
                typeLabel: item.itemType || 'Equipment',
                manufacturerName: item.manufacturer?.name,
                subtitle: item.serialNumber ? `SN: ${item.serialNumber}` : undefined,
                sourceTable: 'inventory',
                isCrossTable: false,
                metadata: item.metadata
              }))
            }
          } catch {
            primary = []
          }
        }

        // Cross-table machines if query matches
        if (cleanQ.length >= 2) {
          try {
            const machines = await $fetch<any[]>('/api/proxy/v1/machine')
            if (Array.isArray(machines)) {
              const matchedMachines = machines.filter(m => 
                (m.name || '').toLowerCase().includes(cleanQ) || 
                (m.groupId || '').toLowerCase().includes(cleanQ) ||
                (m.machineType || '').toLowerCase().includes(cleanQ)
              )
              cross = matchedMachines.slice(0, 6).map(m => ({
                id: m.id,
                name: m.name,
                displayName: m.displayName,
                itemType: 'Machine',
                typeLabel: m.machineType || 'Station',
                subtitle: `Line: ${m.groupId || 'General'}`,
                sourceTable: 'machines',
                isCrossTable: true,
                link: `/dashboard/machines?id=${m.id}`
              }))
            }
          } catch {}
        }
      }

      primaryResults.value = primary
      crossTableResults.value = cross
      results.value = [...primary, ...cross]
    } catch {
      results.value = []
      primaryResults.value = []
      crossTableResults.value = []
    } finally {
      isLoading.value = false
    }
  }

  // Process input for auto-tagging, key:value lookup, and stage 2 key matches
  const handleInputChange = (value: string) => {
    if (rawInput.value !== value) {
      rawInput.value = value
    }

    const trimmed = value.trim()

    // 1. Check if user is typing `key:` or `key:partial`
    const keyMatch = value.match(/(?:^|\s)([a-zA-Z0-9_-]+):([^\s]*)$/)
    if (keyMatch) {
      const activeKey = keyMatch[1].toLowerCase()
      const partialVal = (keyMatch[2] || '').toLowerCase()
      activePendingKey.value = activeKey

      if (KNOWN_KEY_VALUES[activeKey]) {
        const known = KNOWN_KEY_VALUES[activeKey]
        valueSuggestions.value = known.values
          .filter(v => v.toLowerCase().includes(partialVal))
          .map(v => ({
            key: activeKey,
            value: v,
            description: known.description,
            sourceTable: config.instanceId
          }))
      } else {
        valueSuggestions.value = []
      }
      matchingKeys.value = []
    } else {
      activePendingKey.value = ''
      valueSuggestions.value = []

      // 2. Stage 2: Filter Key Suggestions matching typed text
      if (trimmed.length > 0) {
        const lower = trimmed.toLowerCase()
        matchingKeys.value = ALL_FILTER_KEYS.filter(k => 
          k.key.toLowerCase().includes(lower) || lower.includes(k.key.toLowerCase())
        ).slice(0, 6)
      } else {
        matchingKeys.value = ALL_FILTER_KEYS.slice(0, 5)
      }
    }

    if (!config.enableAutoTagging || value.length < (config.minCharsForSuggestions || 2)) {
      autoSuggestions.value = []
      return
    }

    // Check for explicit key:value tags typed in (followed by whitespace)
    if (value.endsWith(' ') || value.includes(' ')) {
      const { tags: extractedTags, remainingText } = autoTagEngine.parseExplicitTags(value)
      if (extractedTags.length > 0) {
        for (const t of extractedTags) {
          addTag(t)
        }
        if (rawInput.value !== remainingText) {
          rawInput.value = remainingText
        }
        return
      }
    }

    // Run regex + fuzzy dictionary auto-tagging
    const { autoTags } = autoTagEngine.analyzeText(value)
    
    // Filter out tags that are already in active tags list
    autoSuggestions.value = autoTags.filter(
      at => !tags.value.some(existing => existing.key === at.tag.key && existing.value.toLowerCase() === at.tag.value.toLowerCase())
    )
  }

  // React to programmatic or external rawInput changes
  watch(rawInput, (newVal) => {
    if (newVal !== undefined) {
      handleInputChange(newVal)
    }
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
      color: tag.color || (key === 'status' ? 'emerald' : key === 'tech' ? 'teal' : key === 'line' ? 'purple' : key === 'mfr' || key === 'manufacturer' ? 'blue' : 'indigo'),
      removable: true
    })

    // Clear suggestion if accepted
    autoSuggestions.value = autoSuggestions.value.filter(
      s => !(s.tag.key === key && s.tag.value.toLowerCase() === value.toLowerCase())
    )

    // Re-execute search with new tags
    executeSearch()
  }

  const selectKeySuggestion = (key: string) => {
    rawInput.value = `${key}:`
    activePendingKey.value = key
    handleInputChange(`${key}:`)
  }

  const selectValueSuggestion = (val: string) => {
    const key = activePendingKey.value || 'tag'
    addTag({ key, value: val })
    rawInput.value = ''
    activePendingKey.value = ''
    valueSuggestions.value = []
  }

  const removeTag = (tagId: string) => {
    tags.value = tags.value.filter(t => t.id !== tagId)
    executeSearch()
  }

  const clearAllTags = () => {
    tags.value = []
    rawInput.value = ''
    results.value = []
    primaryResults.value = []
    crossTableResults.value = []
    autoSuggestions.value = []
    valueSuggestions.value = []
    activePendingKey.value = ''
  }

  // Debounced live search
  watchDebounced(
    rawInput,
    () => {
      if (rawInput.value.length >= (config.minCharsForSuggestions || 2) || tags.value.length > 0) {
        executeSearch()
      } else if (tags.value.length === 0) {
        results.value = []
        primaryResults.value = []
        crossTableResults.value = []
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
    primaryResults,
    crossTableResults,
    matchingKeys,
    valueSuggestions,
    activePendingKey,
    searchKeyGroups,
    isLoading,
    isDropdownOpen,
    effectiveQueryString,
    config,
    handleInputChange,
    addTag,
    selectKeySuggestion,
    selectValueSuggestion,
    removeTag,
    clearAllTags,
    executeSearch,
    fetchSearchKeys
  }
}
