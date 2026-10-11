import { ref, computed } from 'vue'

export interface ReferenceOption {
  id: string
  label: string
  [key: string]: any
}

export interface MetadataKeyInfo {
  key: string
  label: string
  count: number
  sampleValues: string[]
}

// Module-level shared singleton reactive state
const oems = ref<ReferenceOption[]>([])
const importers = ref<ReferenceOption[]>([])
const parentPcs = ref<ReferenceOption[]>([])
const stations = ref<ReferenceOption[]>([])
const components = ref<ReferenceOption[]>([])
const technologies = ref<ReferenceOption[]>([])
const modelNumbers = ref<ReferenceOption[]>([])
const metadataKeys = ref<MetadataKeyInfo[]>([])
const metadataValuesByKey = ref<Record<string, string[]>>({})
const responsibleTeams = ref<ReferenceOption[]>([])

const isLoading = ref(false)
const lastFetched = ref<number | null>(null)
const isInitialized = ref(false)

const STORAGE_KEY = 'heimdall_asset_reference_cache_v1'

export const useAssetReferenceCache = () => {
  // Load initial state from localStorage if available
  const loadFromStorage = () => {
    if (typeof window === 'undefined' || !window.localStorage) return
    try {
      const cached = localStorage.getItem(STORAGE_KEY)
      if (cached) {
        const parsed = JSON.parse(cached)
        if (parsed && typeof parsed === 'object') {
          if (Array.isArray(parsed.oems)) oems.value = parsed.oems
          if (Array.isArray(parsed.importers)) importers.value = parsed.importers
          if (Array.isArray(parsed.parentPcs)) parentPcs.value = parsed.parentPcs
          if (Array.isArray(parsed.stations)) stations.value = parsed.stations
          if (Array.isArray(parsed.components)) components.value = parsed.components
          if (Array.isArray(parsed.technologies)) technologies.value = parsed.technologies
          if (Array.isArray(parsed.modelNumbers)) modelNumbers.value = parsed.modelNumbers
          if (Array.isArray(parsed.metadataKeys)) metadataKeys.value = parsed.metadataKeys
          if (parsed.metadataValuesByKey) metadataValuesByKey.value = parsed.metadataValuesByKey
          if (Array.isArray(parsed.responsibleTeams)) responsibleTeams.value = parsed.responsibleTeams
          if (parsed.lastFetched) lastFetched.value = parsed.lastFetched
        }
      }
    } catch {
      // Ignore parse errors
    }
  }

  const saveToStorage = () => {
    if (typeof window === 'undefined' || !window.localStorage) return
    try {
      const payload = {
        oems: oems.value,
        importers: importers.value,
        parentPcs: parentPcs.value,
        stations: stations.value,
        components: components.value,
        technologies: technologies.value,
        modelNumbers: modelNumbers.value,
        metadataKeys: metadataKeys.value,
        metadataValuesByKey: metadataValuesByKey.value,
        responsibleTeams: responsibleTeams.value,
        lastFetched: lastFetched.value
      }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(payload))
    } catch {
      // Ignore storage errors
    }
  }

  // Deep recursive extraction of all assets in tree
  const flattenInventoryTree = (nodes: any[]): any[] => {
    const flat: any[] = []
    const traverse = (items: any[]) => {
      for (const item of items) {
        flat.push(item)
        if (Array.isArray(item.children) && item.children.length > 0) {
          traverse(item.children)
        }
      }
    }
    traverse(nodes)
    return flat
  }

  // Harvest and populate cache collections from raw API results
  const ingestInventoryData = (
    rawManufacturers: any[],
    rawSuppliers: any[],
    rawMachines: any[],
    rawPcs: any[],
    rawInventoryTree: any[],
    rawTeams: any[] = []
  ) => {
    const allItems = flattenInventoryTree(rawInventoryTree || [])

    // 1. OEMs / Manufacturers
    const oemMap = new Map<string, { id: string; label: string; count: number; country?: string }>()
    
    // Ingest explicitly returned manufacturer entities
    for (const m of rawManufacturers || []) {
      const name = m.name || m.label || m.id
      if (name) {
        oemMap.set(m.id || name, {
          id: m.id || name,
          label: name,
          count: 1,
          country: m.country
        })
      }
    }

    // Ingest manufacturers from item references
    for (const item of allItems) {
      if (item.manufacturer) {
        const mName = typeof item.manufacturer === 'string' ? item.manufacturer : item.manufacturer.name
        const mId = item.manufacturer.id || item.manufacturerId || mName
        if (mName) {
          const existing = oemMap.get(mId) || oemMap.get(mName)
          if (existing) {
            existing.count++
          } else {
            oemMap.set(mId, { id: mId, label: mName, count: 1 })
          }
        }
      }
    }
    oems.value = Array.from(oemMap.values()).sort((a, b) => b.count - a.count)

    // 2. Importers / Vendors / Suppliers
    const supplierMap = new Map<string, { id: string; label: string; count: number }>()
    for (const s of rawSuppliers || []) {
      const name = s.name || s.label || s.id
      if (name) {
        supplierMap.set(s.id || name, { id: s.id || name, label: name, count: 1 })
      }
    }
    for (const item of allItems) {
      if (item.supplier) {
        const sName = typeof item.supplier === 'string' ? item.supplier : item.supplier.name
        const sId = item.supplier.id || item.supplierId || sName
        if (sName) {
          const existing = supplierMap.get(sId) || supplierMap.get(sName)
          if (existing) {
            existing.count++
          } else {
            supplierMap.set(sId, { id: sId, label: sName, count: 1 })
          }
        }
      }
    }
    importers.value = Array.from(supplierMap.values()).sort((a, b) => b.count - a.count)

    // 3. Parent PCs / Client PCs
    const pcList: ReferenceOption[] = []
    for (const pc of rawPcs || []) {
      const host = pc.hostname || pc.name || pc.id
      pcList.push({
        id: pc.id,
        label: `${host} (${pc.ipAddress || 'DHCP'})`,
        hostname: host,
        ipAddress: pc.ipAddress,
        os: pc.os || pc.operatingSystem,
        status: pc.status
      })
    }
    parentPcs.value = pcList

    // 4. Production Stations / Machines
    const stationList: ReferenceOption[] = []
    for (const mach of rawMachines || []) {
      const ident = mach.customIdentifier || mach.name || mach.id
      stationList.push({
        id: mach.id,
        label: ident,
        name: mach.name || ident,
        customIdentifier: ident
      })
    }
    stations.value = stationList

    // 5. Components / Parent Assemblies / Lateral Links
    const compList: ReferenceOption[] = []
    for (const item of allItems) {
      compList.push({
        id: item.id,
        label: `${item.name}${item.displayName ? ` (${item.displayName})` : ''}`,
        name: item.name,
        displayName: item.displayName,
        itemType: item.itemType || 'HardwareComponent',
        technology: item.technology || item.metadata?.Technology,
        parentId: item.parentId
      })
    }
    components.value = compList

    // 6. Technologies
    const techMap = new Map<string, { id: string; label: string; count: number }>()
    for (const item of allItems) {
      const tech = item.technology || item.metadata?.Technology
      if (tech && typeof tech === 'string' && tech.trim()) {
        const trimmed = tech.trim()
        const existing = techMap.get(trimmed)
        if (existing) {
          existing.count++
        } else {
          techMap.set(trimmed, { id: trimmed, label: trimmed, count: 1 })
        }
      }
    }
    // Default baseline industrial technology stacks if database has few
    const defaultTechs = [
      'Siemens SIMATIC / PROFINET',
      'Beckhoff TwinCAT 3 / EtherCAT',
      'Cognex VisionPro AI / GigE',
      'KUKA KSS / CIP Safety',
      'Siemens SINAMICS DRIVE-CLiQ',
      'IFM IO-Link COM2',
      'Rockwell Allen-Bradley / EtherNet/IP',
      'OPC UA / Kepware KEPServerEX',
      'Modbus TCP / RTU'
    ]
    for (const dt of defaultTechs) {
      if (!techMap.has(dt)) {
        techMap.set(dt, { id: dt, label: dt, count: 0 })
      }
    }
    technologies.value = Array.from(techMap.values()).sort((a, b) => b.count - a.count)

    // 7. Model Numbers
    const modelMap = new Map<string, { id: string; label: string; oem?: string }>()
    for (const item of allItems) {
      const model = item.modelNumber || item.data?.ModelNumber || item.metadata?.ModelNumber
      if (model && typeof model === 'string' && model.trim()) {
        const trimmed = model.trim()
        modelMap.set(trimmed, {
          id: trimmed,
          label: trimmed,
          oem: item.manufacturer?.name
        })
      }
    }
    modelNumbers.value = Array.from(modelMap.values())

    // 8. Metadata Keys & Values By Key Cache
    const keyMap = new Map<string, { count: number; samples: Set<string> }>()
    const valuesMap: Record<string, Set<string>> = {}

    // Baseline standard industrial spec keys
    const baselineKeys = [
      'Voltage', 'Power', 'Current', 'Resolution', 'FPS', 'Protocol',
      'IPAddress', 'Port', 'Firmware', 'PressureRange', 'CycleTime', 'Interface'
    ]
    for (const bk of baselineKeys) {
      keyMap.set(bk, { count: 0, samples: new Set() })
      valuesMap[bk] = new Set()
    }

    for (const item of allItems) {
      const meta = item.metadata || item.data || {}
      for (const [k, v] of Object.entries(meta)) {
        if (!k || ['SerialNumber', 'CostInHUF', 'ModelNumber', 'Technology'].includes(k)) continue
        
        let entry = keyMap.get(k)
        if (!entry) {
          entry = { count: 0, samples: new Set() }
          keyMap.set(k, entry)
        }
        entry.count++

        if (v !== undefined && v !== null && v !== '') {
          const strVal = String(v).trim()
          if (strVal) {
            entry.samples.add(strVal)
            if (!valuesMap[k]) valuesMap[k] = new Set()
            valuesMap[k].add(strVal)
          }
        }
      }
    }

    metadataKeys.value = Array.from(keyMap.entries()).map(([key, data]) => ({
      key,
      label: key,
      count: data.count,
      sampleValues: Array.from(data.samples).slice(0, 8)
    })).sort((a, b) => b.count - a.count)

    const finalValuesMap: Record<string, string[]> = {}
    for (const [k, set] of Object.entries(valuesMap)) {
      finalValuesMap[k] = Array.from(set)
    }
    metadataValuesByKey.value = finalValuesMap

    // 9. Responsible Teams
    const teamMap = new Map<string, ReferenceOption>()
    for (const t of rawTeams || []) {
      const tName = t.name || t.label || t.id
      if (tName) {
        teamMap.set(t.id || tName, { id: t.id || tName, label: tName, name: tName })
      }
    }
    for (const item of allItems) {
      if (Array.isArray(item.responsibleTeams)) {
        for (const t of item.responsibleTeams) {
          const tName = typeof t === 'string' ? t : (t.name || t.id)
          const tId = typeof t === 'string' ? t : (t.id || t.name)
          if (tName && !teamMap.has(tId)) {
            teamMap.set(tId, { id: tId, label: tName, name: tName })
          }
        }
      }
    }
    responsibleTeams.value = Array.from(teamMap.values())

    lastFetched.value = Date.now()
    saveToStorage()
  }

  // Fetch from APIs and build cache
  const fetchReferenceCache = async (force = false) => {
    if (!force && isInitialized.value && lastFetched.value && (Date.now() - lastFetched.value < 60000)) {
      return
    }

    isLoading.value = true
    try {
      // Fetch endpoints in parallel
      const [mRes, sRes, machRes, pcRes, invRes, teamRes] = await Promise.allSettled([
        $fetch<any[]>('/api/proxy/inventory/manufacturers'),
        $fetch<any[]>('/api/proxy/inventory/suppliers'),
        $fetch<any[]>('/api/proxy/inventory/machines'),
        $fetch<any[]>('/api/proxy/inventory/client-pcs'),
        $fetch<any[]>('/api/proxy/inventory'),
        $fetch<any[]>('/api/proxy/inventory/teams')
      ])

      const mData = mRes.status === 'fulfilled' ? mRes.value : []
      const sData = sRes.status === 'fulfilled' ? sRes.value : []
      const machData = machRes.status === 'fulfilled' ? machRes.value : []
      const pcData = pcRes.status === 'fulfilled' ? pcRes.value : []
      const invData = invRes.status === 'fulfilled' ? invRes.value : []
      const teamData = teamRes.status === 'fulfilled' ? teamRes.value : []

      ingestInventoryData(mData, sData, machData, pcData, invData, teamData)
      isInitialized.value = true
    } catch (e) {
      console.warn('Failed to refresh asset reference cache from backend, using local cache:', e)
    } finally {
      isLoading.value = false
    }
  }

  // Ingest newly typed / created asset values directly into the cache
  const registerAssetValues = (asset: any) => {
    if (!asset) return

    // 1. Ingest OEM
    if (asset.manufacturerId || asset.manufacturer) {
      const name = typeof asset.manufacturer === 'string' ? asset.manufacturer : (asset.manufacturer?.name || asset.manufacturerId)
      if (name && !oems.value.some(o => o.id === name || o.label.toLowerCase() === name.toLowerCase())) {
        oems.value.unshift({ id: name, label: name, count: 1 })
      }
    }

    // 2. Ingest Supplier / Importer
    if (asset.supplierId || asset.supplier) {
      const name = typeof asset.supplier === 'string' ? asset.supplier : (asset.supplier?.name || asset.supplierId)
      if (name && !importers.value.some(s => s.id === name || s.label.toLowerCase() === name.toLowerCase())) {
        importers.value.unshift({ id: name, label: name, count: 1 })
      }
    }

    // 3. Ingest Technology
    if (asset.technology) {
      const tech = asset.technology.trim()
      if (tech && !technologies.value.some(t => t.id.toLowerCase() === tech.toLowerCase())) {
        technologies.value.unshift({ id: tech, label: tech, count: 1 })
      }
    }

    // 4. Ingest Model Number
    if (asset.modelNumber) {
      const model = asset.modelNumber.trim()
      if (model && !modelNumbers.value.some(m => m.id.toLowerCase() === model.toLowerCase())) {
        modelNumbers.value.unshift({ id: model, label: model })
      }
    }

    // 5. Ingest Metadata keys & values
    const meta = asset.metadata || asset.data || {}
    for (const [k, v] of Object.entries(meta)) {
      if (!k || ['SerialNumber', 'CostInHUF', 'ModelNumber', 'Technology'].includes(k)) continue

      let keyEntry = metadataKeys.value.find(entry => entry.key.toLowerCase() === k.toLowerCase())
      if (!keyEntry) {
        keyEntry = { key: k, label: k, count: 1, sampleValues: [] }
        metadataKeys.value.push(keyEntry)
      } else {
        keyEntry.count++
      }

      if (v !== undefined && v !== null && v !== '') {
        const strVal = String(v).trim()
        if (strVal) {
          if (!keyEntry.sampleValues.includes(strVal)) {
            keyEntry.sampleValues.unshift(strVal)
          }
          if (!metadataValuesByKey.value[k]) {
            metadataValuesByKey.value[k] = []
          }
          if (!metadataValuesByKey.value[k].includes(strVal)) {
            metadataValuesByKey.value[k].unshift(strVal)
          }
        }
      }
    }

    saveToStorage()
  }

  // Suggestions for a specific metadata attribute key
  const getSuggestionsForKey = (key: string): string[] => {
    if (!key) return []
    return metadataValuesByKey.value[key] || []
  }

  // Initialize on first call
  if (!isInitialized.value) {
    loadFromStorage()
  }

  return {
    // Reactive Cached Collections
    oems,
    importers,
    parentPcs,
    stations,
    components,
    technologies,
    modelNumbers,
    metadataKeys,
    metadataValuesByKey,
    responsibleTeams,

    // Loading & Timing
    isLoading,
    lastFetched,

    // Actions
    fetchReferenceCache,
    registerAssetValues,
    getSuggestionsForKey,
    ingestInventoryData
  }
}
