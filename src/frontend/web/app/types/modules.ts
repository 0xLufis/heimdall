export type PlatformModuleId = 'core' | 'ticketing' | 'inventory' | 'telemetry'

export interface PlatformModuleManifest {
  id: PlatformModuleId
  name: string
  displayNameKey: string
  description: string
  displayDescKey: string
  version: string
  isRequired: boolean
  defaultEnabled: boolean
  dependencies: PlatformModuleId[]
  routes: string[]
  apiPrefixes: string[]
  icon: string
}

export interface PlatformModulesConfig {
  enableTicketing: boolean
  enableInventory: boolean
  enableTelemetry: boolean
}

export const PLATFORM_MODULE_MANIFESTS: Record<PlatformModuleId, PlatformModuleManifest> = {
  core: {
    id: 'core',
    name: 'Core Platform',
    displayNameKey: 'modules.core',
    description: 'Essential asset registry, machine stations, user auth, and plant maps. Always enabled.',
    displayDescKey: 'modules.coreDesc',
    version: '1.0.0',
    isRequired: true,
    defaultEnabled: true,
    dependencies: [],
    routes: [
      '/dashboard',
      '/dashboard/machines',
      '/dashboard/machine-groups',
      '/dashboard/clients',
      '/dashboard/map',
      '/dashboard/users',
      '/dashboard/organizations',
      '/dashboard/security-groups',
      '/dashboard/admin/system-settings',
      '/dashboard/settings',
      '/dashboard/help'
    ],
    apiPrefixes: [
      '/api/machines',
      '/api/users',
      '/api/organizations',
      '/api/auth'
    ],
    icon: 'i-lucide-layers'
  },
  ticketing: {
    id: 'ticketing',
    name: 'Maintenance Ticketing',
    displayNameKey: 'modules.ticketing',
    description: 'Work order ticketing, technician dispatch, TV monitor mode, and TwinCAT DUT export.',
    displayDescKey: 'modules.ticketingDesc',
    version: '1.2.0',
    isRequired: false,
    defaultEnabled: true,
    dependencies: ['core'],
    routes: [
      '/dashboard/tickets',
      '/dashboard/delegations'
    ],
    apiPrefixes: [
      '/api/tickets',
      '/api/delegations'
    ],
    icon: 'i-lucide-wrench'
  },
  inventory: {
    id: 'inventory',
    name: 'Inventory & Spare Parts',
    displayNameKey: 'modules.inventory',
    description: 'Serialized part tracking, warehouse bin allocation, WBS element linkage, and salvage flows.',
    displayDescKey: 'modules.inventoryDesc',
    version: '1.1.0',
    isRequired: false,
    defaultEnabled: true,
    dependencies: ['core'],
    routes: [
      '/dashboard/inventory'
    ],
    apiPrefixes: [
      '/api/inventory',
      '/api/parts'
    ],
    icon: 'i-lucide-package'
  },
  telemetry: {
    id: 'telemetry',
    name: 'Industrial Telemetry',
    displayNameKey: 'modules.telemetry',
    description: 'Edge signal streaming, metric trends, recipe tool wear forecasting, and dynamic KPI builder.',
    displayDescKey: 'modules.telemetryDesc',
    version: '1.3.0',
    isRequired: false,
    defaultEnabled: true,
    dependencies: ['core'],
    routes: [
      '/dashboard/telemetry',
      '/dashboard/analytics'
    ],
    apiPrefixes: [
      '/api/telemetry',
      '/api/kpi'
    ],
    icon: 'i-lucide-activity'
  }
}
