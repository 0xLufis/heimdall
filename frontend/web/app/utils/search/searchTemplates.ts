import type { SearchResultItem, TagPill } from '~/types/search'

export interface SearchTemplateDef<T = any> {
  name: string
  searchableFields: string[]
  extractSearchItem: (raw: T) => SearchResultItem
  extractKvFields?: (raw: T) => Record<string, string | string[] | undefined>
  defaultTagKeys?: string[]
}

/**
 * 1. Machine Template
 * Normalizes ProductionStation objects.
 */
export const machineTemplate: SearchTemplateDef = {
  name: 'machine',
  searchableFields: ['name', 'displayName', 'customIdentifier', 'machineType', 'groupId', 'id'],
  defaultTagKeys: ['line', 'tech', 'status', 'station', 'mfr'],
  extractSearchItem: (raw: any): SearchResultItem => {
    const name = raw.name || raw.displayName || 'Station'
    const subtitleParts = []
    if (raw.groupId) subtitleParts.push(`Line: ${raw.groupId}`)
    if (raw.controllers && Array.isArray(raw.controllers)) {
      subtitleParts.push(`Controllers: ${raw.controllers.length}`)
    }

    return {
      id: raw.id || raw.customIdentifier || 'station-unknown',
      name,
      displayName: raw.displayName || name,
      itemType: 'Machine',
      typeLabel: raw.machineType || 'Station',
      subtitle: subtitleParts.join(' • ') || (raw.customIdentifier ? `ID: ${raw.customIdentifier}` : undefined),
      status: raw.status || (raw.isOnline !== undefined ? (raw.isOnline ? 'online' : 'offline') : 'online'),
      sourceTable: 'machines',
      isCrossTable: false,
      link: raw.id ? `/dashboard/machines?id=${raw.id}` : '/dashboard/machines',
      metadata: raw.metadata || {}
    }
  },
  extractKvFields: (raw: any) => ({
    line: raw.groupId,
    station: raw.customIdentifier || raw.name,
    status: raw.status || (raw.isOnline !== undefined ? (raw.isOnline ? 'online' : 'offline') : undefined),
    tech: raw.technology || raw.tech
  })
}

/**
 * 2. Endpoint / Client-PC Template
 * Normalizes IndustrialController and Protobuf SystemInfoRequest objects.
 */
export const endpointTemplate: SearchTemplateDef = {
  name: 'endpoint',
  searchableFields: [
    'hostname',
    'name',
    'displayName',
    'macAddress',
    'mac_address',
    'ipAddress',
    'machineIdentifier',
    'machine_identifier',
    'id'
  ],
  defaultTagKeys: ['ip', 'mac', 'status', 'station'],
  extractSearchItem: (raw: any): SearchResultItem => {
    const hostname = raw.hostname || raw.name || 'Node'
    const mac = raw.macAddress || raw.mac_address || 'N/A'
    const ip = raw.ipAddress || (raw.telemetry?.ipAddress) || 'DHCP'
    const isOnline = raw.lastSeen || raw.last_online || raw.isOnline

    return {
      id: raw.id || raw.machineIdentifier || raw.machine_identifier || raw.hostname || 'node-unknown',
      name: hostname,
      displayName: raw.displayName || hostname,
      itemType: 'ClientPc',
      typeLabel: raw.controllerType || 'Industrial PC',
      subtitle: `IP: ${ip} • MAC: ${mac}`,
      status: isOnline ? 'online' : 'offline',
      sourceTable: 'nodes',
      isCrossTable: false,
      link: '/dashboard/clients',
      metadata: raw.metadata || raw.systemMetadata || {}
    }
  },
  extractKvFields: (raw: any) => ({
    ip: raw.ipAddress || raw.telemetry?.ipAddress,
    mac: raw.macAddress || raw.mac_address,
    status: raw.lastSeen || raw.last_online || raw.isOnline ? 'online' : 'offline',
    station: raw.machineIdentifier || raw.machine_identifier
  })
}

/**
 * 3. Inventory Template
 * Normalizes BaseInventoryItem and Protobuf InventoryComponent objects.
 */
export const inventoryTemplate: SearchTemplateDef = {
  name: 'inventory',
  searchableFields: [
    'name',
    'displayName',
    'customIdentifier',
    'serialNumber',
    'itemType',
    'technology',
    'storageLocation'
  ],
  defaultTagKeys: ['type', 'isstock', 'mfr', 'manufacturer', 'serial', 'location', 'tech', 'category', 'status'],
  extractSearchItem: (raw: any): SearchResultItem => {
    const name = raw.name || raw.customIdentifier || 'Asset'
    const mfrName = raw.manufacturer?.name || raw.manufacturerName || (typeof raw.manufacturer === 'string' ? raw.manufacturer : undefined)
    
    let subtitle: string | undefined
    if (raw.serialNumber) {
      subtitle = `SN: ${raw.serialNumber}`
    } else if (raw.customIdentifier) {
      subtitle = `ID: ${raw.customIdentifier}`
    } else if (raw.isStockItem && raw.stockQuantity !== undefined) {
      subtitle = `Qty: ${raw.stockQuantity}`
    }

    return {
      id: raw.id || raw.customIdentifier || 'asset-unknown',
      name,
      displayName: raw.displayName || name,
      itemType: raw.itemType || 'Asset',
      typeLabel: raw.typeLabel || raw.itemType || (raw.isStockItem ? 'Bulk Stock' : 'Component'),
      manufacturerName: mfrName,
      subtitle,
      status: raw.equipmentStatus || raw.status || (raw.isOnline !== undefined ? (raw.isOnline ? 'online' : 'offline') : undefined),
      sourceTable: 'inventory',
      isCrossTable: false,
      link: raw.id ? `/dashboard/inventory/${raw.id}` : '/dashboard/inventory',
      metadata: raw.metadata || {}
    }
  },
  extractKvFields: (raw: any) => ({
    type: raw.itemType,
    status: raw.equipmentStatus || raw.status || (raw.isOnline !== undefined ? (raw.isOnline ? 'online' : 'offline') : undefined),
    isstock: raw.isStockItem !== undefined ? String(raw.isStockItem) : undefined,
    mfr: raw.manufacturer?.name || raw.manufacturerName || (typeof raw.manufacturer === 'string' ? raw.manufacturer : undefined),
    manufacturer: raw.manufacturer?.name || raw.manufacturerName || (typeof raw.manufacturer === 'string' ? raw.manufacturer : undefined),
    serial: raw.serialNumber,
    tech: raw.technology,
    location: raw.storageLocation,
    category: raw.category
  })
}

/**
 * 4. User Template
 * Normalizes PlantUser / DemoPersona objects.
 */
export const userTemplate: SearchTemplateDef = {
  name: 'user',
  searchableFields: ['name', 'displayName', 'email', 'role', 'department', 'title'],
  defaultTagKeys: ['role', 'dept', 'title'],
  extractSearchItem: (raw: any): SearchResultItem => {
    const name = raw.name || 'User'
    return {
      id: raw.id || 'user-unknown',
      name,
      displayName: raw.displayName || name,
      itemType: 'User',
      typeLabel: raw.title || raw.role || 'Personnel',
      subtitle: `${raw.department || 'Operations'} • ${raw.role || 'Staff'}`,
      status: 'online',
      sourceTable: 'users',
      isCrossTable: false,
      link: '/dashboard/settings',
      metadata: { email: raw.email, department: raw.department, role: raw.role }
    }
  },
  extractKvFields: (raw: any) => ({
    role: raw.role,
    dept: raw.department,
    title: raw.title
  })
}

/**
 * 5. Telemetry Template
 * Normalizes Protobuf TelemetryDataPoint objects.
 */
export const telemetryTemplate: SearchTemplateDef = {
  name: 'telemetry',
  searchableFields: ['canonical_key', 'canonicalKey', 'point_id', 'pointId', 'description'],
  defaultTagKeys: ['quality', 'unit', 'key'],
  extractSearchItem: (raw: any): SearchResultItem => {
    const pointId = raw.point_id || raw.pointId || 'pt'
    const key = raw.canonical_key || raw.canonicalKey || pointId
    const unit = raw.type_descriptor?.unit || raw.unit || ''
    const quality = raw.quality !== undefined ? String(raw.quality) : 'QUALITY_GOOD'

    return {
      id: pointId,
      name: key,
      displayName: raw.type_descriptor?.description || key,
      itemType: 'Telemetry',
      typeLabel: raw.type_descriptor?.classifier !== undefined ? String(raw.type_descriptor.classifier) : 'Sensor Point',
      subtitle: unit ? `Unit: ${unit} • Quality: ${quality}` : `Quality: ${quality}`,
      status: quality === '0' || quality === 'QUALITY_GOOD' ? 'online' : 'warning',
      sourceTable: 'telemetry',
      isCrossTable: false,
      link: '/dashboard/telemetry',
      metadata: raw
    }
  },
  extractKvFields: (raw: any) => ({
    quality: raw.quality !== undefined ? String(raw.quality) : undefined,
    unit: raw.type_descriptor?.unit || raw.unit,
    key: raw.canonical_key || raw.canonicalKey
  })
}

const TEMPLATE_REGISTRY: Record<string, SearchTemplateDef> = {
  machine: machineTemplate,
  machines: machineTemplate,
  station: machineTemplate,
  endpoint: endpointTemplate,
  endpoints: endpointTemplate,
  'client-pc': endpointTemplate,
  clientpc: endpointTemplate,
  nodes: endpointTemplate,
  clients: endpointTemplate,
  inventory: inventoryTemplate,
  parts: inventoryTemplate,
  stock: inventoryTemplate,
  user: userTemplate,
  users: userTemplate,
  personas: userTemplate,
  telemetry: telemetryTemplate
}

/**
 * Resolves a template definition by name or returns a custom definition.
 */
export function resolveSearchTemplate(nameOrDef: string | SearchTemplateDef = 'inventory'): SearchTemplateDef {
  if (typeof nameOrDef === 'object' && nameOrDef !== null && nameOrDef.extractSearchItem) {
    return nameOrDef
  }
  const key = (typeof nameOrDef === 'string' ? nameOrDef : 'inventory').toLowerCase().trim()
  return TEMPLATE_REGISTRY[key] || inventoryTemplate
}
