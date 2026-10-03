import { ref, computed } from 'vue'
import { useFeatureFlags } from '~/composables/useFeatureFlags'
import { PLATFORM_MODULE_MANIFESTS, type PlatformModuleId } from '~/types/modules'

export type PluginCategory = 'core' | 'operations' | 'integration' | 'custom'

export interface PluginLifecycleHooks {
  onInstall?: () => void | Promise<void>
  onEnable?: () => void | Promise<void>
  onDisable?: () => void | Promise<void>
  onUninstall?: () => void | Promise<void>
}

export interface HeimdallPlugin {
  id: string
  name: string
  version: string
  description: string
  author?: string
  icon?: string
  category: PluginCategory
  isRequired?: boolean
  enabled: boolean
  dependencies?: string[]
  routes?: string[]
  apiPrefixes?: string[]
  settings?: Record<string, any>
  hooks?: PluginLifecycleHooks
}

const STORAGE_KEY = 'heimdall_plugins_registry'

// Native Built-in Plugins
const INITIAL_BUILTIN_PLUGINS: HeimdallPlugin[] = [
  {
    id: 'core-platform',
    name: 'Core Platform Engine',
    version: '1.0.0',
    description: 'Fundamental infrastructure: assets, machines, stations, auth, and layout.',
    author: 'Heimdall Core Team',
    icon: 'i-lucide-layers',
    category: 'core',
    isRequired: true,
    enabled: true,
    dependencies: [],
    routes: ['/dashboard', '/dashboard/machines', '/dashboard/clients']
  },
  {
    id: 'maintenance-ticketing',
    name: 'Maintenance & Work Orders',
    version: '1.2.0',
    description: '4-tier incident handling, technician dispatch, TV monitor, and QR pickup.',
    author: 'Heimdall Operations',
    icon: 'i-lucide-wrench',
    category: 'operations',
    isRequired: false,
    enabled: true,
    dependencies: ['core-platform'],
    routes: ['/dashboard/tickets', '/dashboard/delegations', '/dashboard/tickets/monitor']
  },
  {
    id: 'inventory-assets',
    name: 'Inventory & Spare Parts',
    version: '1.1.0',
    description: 'Serialized asset tracking, WBS element mapping, salvage flows, and stocking policies.',
    author: 'Heimdall Logistics',
    icon: 'i-lucide-package',
    category: 'operations',
    isRequired: false,
    enabled: true,
    dependencies: ['core-platform'],
    routes: ['/dashboard/inventory']
  },
  {
    id: 'industrial-telemetry',
    name: 'Industrial Telemetry & Trends',
    version: '1.0.0',
    description: 'Real-time PLC signal streaming, tool wear forecasting, and dynamic KPI builder.',
    author: 'Heimdall Controls',
    icon: 'i-lucide-activity',
    category: 'operations',
    isRequired: false,
    enabled: true,
    dependencies: ['core-platform'],
    routes: ['/dashboard/telemetry']
  },
  {
    id: 'twincat-automation',
    name: 'Beckhoff TwinCAT Automation Hub',
    version: '1.0.0',
    description: 'Direct ADS communication, automated .TcDUT export, and PLC error-code mapping.',
    author: 'Beckhoff Automation Integration',
    icon: 'i-lucide-cpu',
    category: 'integration',
    isRequired: false,
    enabled: true,
    dependencies: ['core-platform', 'maintenance-ticketing']
  },
  {
    id: 'sap-pm-bridge',
    name: 'SAP Plant Maintenance (PM) Connector',
    version: '0.9.0',
    description: 'Enterprise ERP bridge for synchronization of work orders, cost centers, and equipment masters.',
    author: 'Enterprise ERP Labs',
    icon: 'i-lucide-database',
    category: 'integration',
    isRequired: false,
    enabled: false,
    dependencies: ['core-platform']
  }
]

// Global reactive state
const registeredPlugins = ref<Map<string, HeimdallPlugin>>(new Map())
let isInitialized = false

function initPluginsState() {
  if (isInitialized) return
  isInitialized = true

  const initialMap = new Map<string, HeimdallPlugin>()
  for (const p of INITIAL_BUILTIN_PLUGINS) {
    initialMap.set(p.id, { ...p })
  }

  if (typeof window !== 'undefined') {
    try {
      const saved = window.localStorage?.getItem(STORAGE_KEY)
      if (saved) {
        const parsed = JSON.parse(saved) as Record<string, Partial<HeimdallPlugin>>
        for (const [id, data] of Object.entries(parsed)) {
          if (initialMap.has(id)) {
            const existing = initialMap.get(id)!
            if (!existing.isRequired && typeof data.enabled === 'boolean') {
              existing.enabled = data.enabled
            }
            if (data.settings) {
              existing.settings = { ...existing.settings, ...data.settings }
            }
          } else if (data.name && data.category) {
            initialMap.set(id, data as HeimdallPlugin)
          }
        }
      }
    } catch {
      // Fallback
    }
  }

  registeredPlugins.value = initialMap
}

function persistPluginsState() {
  if (typeof window === 'undefined') return
  try {
    const payload: Record<string, any> = {}
    for (const [id, plugin] of registeredPlugins.value.entries()) {
      payload[id] = {
        id: plugin.id,
        name: plugin.name,
        version: plugin.version,
        description: plugin.description,
        author: plugin.author,
        icon: plugin.icon,
        category: plugin.category,
        isRequired: plugin.isRequired,
        enabled: plugin.enabled,
        dependencies: plugin.dependencies,
        routes: plugin.routes,
        settings: plugin.settings
      }
    }
    window.localStorage?.setItem(STORAGE_KEY, JSON.stringify(payload))
  } catch {
    // Fallback
  }
}

export function usePlugins() {
  initPluginsState()
  const { setModuleEnabled } = useFeatureFlags()

  const plugins = computed(() => Array.from(registeredPlugins.value.values()))
  const enabledPlugins = computed(() => plugins.value.filter(p => p.enabled))

  const isPluginEnabled = (pluginId: string): boolean => {
    const plugin = registeredPlugins.value.get(pluginId)
    return !!plugin?.enabled
  }

  const getPlugin = (pluginId: string): HeimdallPlugin | undefined => {
    return registeredPlugins.value.get(pluginId)
  }

  const registerPlugin = (plugin: HeimdallPlugin) => {
    const copy = { ...plugin }
    registeredPlugins.value.set(plugin.id, copy)
    if (copy.hooks?.onInstall) {
      try { copy.hooks.onInstall() } catch {}
    }
    persistPluginsState()
  }

  const unregisterPlugin = (pluginId: string): boolean => {
    const plugin = registeredPlugins.value.get(pluginId)
    if (!plugin || plugin.isRequired) return false
    if (plugin.hooks?.onUninstall) {
      try { plugin.hooks.onUninstall() } catch {}
    }
    registeredPlugins.value.delete(pluginId)
    persistPluginsState()
    return true
  }

  const enablePlugin = async (pluginId: string): Promise<boolean> => {
    const plugin = registeredPlugins.value.get(pluginId)
    if (!plugin) return false

    // Check dependencies
    if (plugin.dependencies && plugin.dependencies.length > 0) {
      for (const dep of plugin.dependencies) {
        if (!isPluginEnabled(dep)) {
          console.warn(`Cannot enable plugin "${plugin.name}": dependency "${dep}" is disabled`)
          return false
        }
      }
    }

    plugin.enabled = true
    if (plugin.hooks?.onEnable) {
      try { await plugin.hooks.onEnable() } catch {}
    }

    // Sync with platform module flags if applicable
    if (pluginId === 'maintenance-ticketing') setModuleEnabled('ticketing', true)
    if (pluginId === 'inventory-assets') setModuleEnabled('inventory', true)
    if (pluginId === 'industrial-telemetry') setModuleEnabled('telemetry', true)

    persistPluginsState()
    return true
  }

  const disablePlugin = async (pluginId: string): Promise<boolean> => {
    const plugin = registeredPlugins.value.get(pluginId)
    if (!plugin || plugin.isRequired) return false

    // Check if any active plugin depends on this one
    for (const other of registeredPlugins.value.values()) {
      if (other.enabled && other.dependencies?.includes(pluginId)) {
        console.warn(`Cannot disable plugin "${plugin.name}": active plugin "${other.name}" depends on it`)
        return false
      }
    }

    plugin.enabled = false
    if (plugin.hooks?.onDisable) {
      try { await plugin.hooks.onDisable() } catch {}
    }

    // Sync with platform module flags if applicable
    if (pluginId === 'maintenance-ticketing') setModuleEnabled('ticketing', false)
    if (pluginId === 'inventory-assets') setModuleEnabled('inventory', false)
    if (pluginId === 'industrial-telemetry') setModuleEnabled('telemetry', false)

    persistPluginsState()
    return true
  }

  const togglePlugin = async (pluginId: string): Promise<boolean> => {
    const plugin = registeredPlugins.value.get(pluginId)
    if (!plugin) return false
    return plugin.enabled ? disablePlugin(pluginId) : enablePlugin(pluginId)
  }

  const resetPluginsToDefault = () => {
    const initialMap = new Map<string, HeimdallPlugin>()
    for (const p of INITIAL_BUILTIN_PLUGINS) {
      initialMap.set(p.id, { ...p })
    }
    registeredPlugins.value = initialMap
    persistPluginsState()
  }

  return {
    plugins,
    enabledPlugins,
    isPluginEnabled,
    getPlugin,
    registerPlugin,
    unregisterPlugin,
    enablePlugin,
    disablePlugin,
    togglePlugin,
    resetPluginsToDefault
  }
}
