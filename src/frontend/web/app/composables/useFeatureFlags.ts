import { ref, computed, type ComputedRef } from 'vue'
import { resolveModuleFlagsFromEnv, parseEnvBoolean } from '~/utils/moduleFlags'

export type PlatformModuleId = 'core' | 'ticketing' | 'inventory' | 'telemetry'

export interface AppFeatureFlags {
  enableDevFeatures: boolean
  enableDebugFeatures: boolean
  enableSimulation: boolean
  enableTicketing: boolean
  enableInventory: boolean
  enableTelemetry: boolean
}

// Client-side overrides for dynamic module activation
const clientModuleOverrides = ref<Partial<Record<PlatformModuleId, boolean>>>({})
let isModuleConfigLoaded = false

function loadClientModuleOverrides() {
  if (isModuleConfigLoaded || typeof window === 'undefined') return
  try {
    const raw = window.localStorage?.getItem('heimdall_modules_config')
    if (raw) {
      const parsed = JSON.parse(raw)
      if (typeof parsed === 'object' && parsed !== null) {
        clientModuleOverrides.value = parsed
      }
    }
  } catch {
    // Graceful fallback for test or sandbox environments
  }
  isModuleConfigLoaded = true
}

/**
 * Returns static snapshot of current application feature flags based on
 * runtime configuration or process environment.
 * - enableDevFeatures: Enabled if HEIMDALL_ENABLE_DEV === 'true' or NODE_ENV !== 'production'. In production, default is false.
 * - enableDebugFeatures: Enabled if HEIMDALL_ENABLE_DEBUG === 'true' or HEIMDALL_DEBUG === 'true'. In production, default is false.
 * - enableSimulation: Enabled if HEIMDALL_ENABLE_SIMULATION === 'true' or (enableDevFeatures && HEIMDALL_ENABLE_SIMULATION !== 'false').
 * - enableTicketing, enableInventory, enableTelemetry: Resolved from environment variables, presets, or runtime config.
 */
export function getAppFeatureFlags(): AppFeatureFlags {
  let devFromConfig: boolean | undefined
  let debugFromConfig: boolean | undefined
  let simFromConfig: boolean | undefined
  let ticketingFromConfig: boolean | undefined
  let inventoryFromConfig: boolean | undefined
  let telemetryFromConfig: boolean | undefined

  try {
    const config = useRuntimeConfig()
    if (config?.public?.enableDevFeatures !== undefined) {
      devFromConfig = Boolean(config.public.enableDevFeatures)
    }
    if (config?.public?.enableDebugFeatures !== undefined) {
      debugFromConfig = Boolean(config.public.enableDebugFeatures)
    }
    if (config?.public?.enableSimulation !== undefined) {
      simFromConfig = Boolean(config.public.enableSimulation)
    }
    if (config?.public?.enableTicketing !== undefined) {
      ticketingFromConfig = Boolean(config.public.enableTicketing)
    }
    if (config?.public?.enableInventory !== undefined) {
      inventoryFromConfig = Boolean(config.public.enableInventory)
    }
    if (config?.public?.enableTelemetry !== undefined) {
      telemetryFromConfig = Boolean(config.public.enableTelemetry)
    }
  } catch {
    // Runtime config may not be initialized in vitest / standalone contexts
  }

  const isProduction = typeof process !== 'undefined'
    ? process.env.NODE_ENV === 'production'
    : false

  const enableDev = typeof process !== 'undefined' && process.env.HEIMDALL_ENABLE_DEV !== undefined
    ? (parseEnvBoolean(process.env.HEIMDALL_ENABLE_DEV) ?? false)
    : (devFromConfig !== undefined ? devFromConfig : (!isProduction && (typeof process === 'undefined' || process.env.HEIMDALL_ENABLE_DEV !== 'false')))

  const enableDebug = typeof process !== 'undefined' && (process.env.HEIMDALL_ENABLE_DEBUG !== undefined || process.env.HEIMDALL_DEBUG !== undefined)
    ? (parseEnvBoolean(process.env.HEIMDALL_ENABLE_DEBUG) ?? parseEnvBoolean(process.env.HEIMDALL_DEBUG) ?? false)
    : (debugFromConfig !== undefined ? debugFromConfig : false)

  const enableSimulation = typeof process !== 'undefined' && process.env.HEIMDALL_ENABLE_SIMULATION !== undefined
    ? (parseEnvBoolean(process.env.HEIMDALL_ENABLE_SIMULATION) ?? false)
    : (simFromConfig !== undefined ? simFromConfig : (enableDev && (typeof process === 'undefined' || process.env.HEIMDALL_ENABLE_SIMULATION !== 'false')))

  const env = typeof process !== 'undefined' ? (process.env as any) : {}
  const moduleFlags = resolveModuleFlagsFromEnv(env)

  const hasEnvModuleVars = typeof process !== 'undefined' && (
    process.env.HEIMDALL_MODULE_TICKETING !== undefined ||
    process.env.HEIMDALL_ENABLE_TICKETING !== undefined ||
    process.env.ENABLE_TICKETING !== undefined ||
    process.env.NUXT_PUBLIC_ENABLE_TICKETING !== undefined ||
    process.env.HEIMDALL_MODULE_INVENTORY !== undefined ||
    process.env.HEIMDALL_ENABLE_INVENTORY !== undefined ||
    process.env.ENABLE_INVENTORY !== undefined ||
    process.env.NUXT_PUBLIC_ENABLE_INVENTORY !== undefined ||
    process.env.HEIMDALL_MODULE_TELEMETRY !== undefined ||
    process.env.HEIMDALL_ENABLE_TELEMETRY !== undefined ||
    process.env.ENABLE_TELEMETRY !== undefined ||
    process.env.NUXT_PUBLIC_ENABLE_TELEMETRY !== undefined ||
    process.env.HEIMDALL_MODULE_PRESET !== undefined ||
    process.env.HEIMDALL_PRESET !== undefined ||
    process.env.HEIMDALL_MODULES !== undefined ||
    process.env.HEIMDALL_ENABLED_MODULES !== undefined ||
    process.env.HEIMDALL_DISABLED_MODULES !== undefined ||
    process.env.HEIMDALL_DISABLE_MODULES !== undefined
  )

  const enableTicketing = hasEnvModuleVars
    ? moduleFlags.enableTicketing
    : (ticketingFromConfig !== undefined ? ticketingFromConfig : moduleFlags.enableTicketing)

  const enableInventory = hasEnvModuleVars
    ? moduleFlags.enableInventory
    : (inventoryFromConfig !== undefined ? inventoryFromConfig : moduleFlags.enableInventory)

  const enableTelemetry = hasEnvModuleVars
    ? moduleFlags.enableTelemetry
    : (telemetryFromConfig !== undefined ? telemetryFromConfig : moduleFlags.enableTelemetry)

  return {
    enableDevFeatures: enableDev,
    enableDebugFeatures: enableDebug,
    enableSimulation,
    enableTicketing,
    enableInventory,
    enableTelemetry
  }
}

/**
 * Vue composable providing reactive feature flags across the frontend application.
 */
export const useFeatureFlags = () => {
  loadClientModuleOverrides()

  const staticFlags = getAppFeatureFlags()

  const isModuleEnabled = (moduleId: PlatformModuleId): boolean => {
    if (moduleId === 'core') return true
    if (clientModuleOverrides.value[moduleId] !== undefined) {
      return Boolean(clientModuleOverrides.value[moduleId])
    }
    const flags = getAppFeatureFlags()
    if (moduleId === 'ticketing') return flags.enableTicketing
    if (moduleId === 'inventory') return flags.enableInventory
    if (moduleId === 'telemetry') return flags.enableTelemetry
    return true
  }

  const setModuleEnabled = (moduleId: PlatformModuleId, enabled: boolean): void => {
    if (moduleId === 'core') return // Core platform cannot be disabled
    clientModuleOverrides.value = {
      ...clientModuleOverrides.value,
      [moduleId]: enabled
    }
    if (typeof window !== 'undefined') {
      try {
        window.localStorage?.setItem('heimdall_modules_config', JSON.stringify(clientModuleOverrides.value))
      } catch {
        // Safe fallback
      }
    }
  }

  const resetModulesToDefault = (): void => {
    clientModuleOverrides.value = {}
    if (typeof window !== 'undefined') {
      try {
        window.localStorage?.removeItem('heimdall_modules_config')
      } catch {
        // Safe fallback
      }
    }
  }

  const enableDevFeatures = computed<boolean>(() => getAppFeatureFlags().enableDevFeatures)
  const enableDebugFeatures = computed<boolean>(() => getAppFeatureFlags().enableDebugFeatures)
  const enableSimulation = computed<boolean>(() => getAppFeatureFlags().enableSimulation)

  const enableTicketing = computed<boolean>(() => isModuleEnabled('ticketing'))
  const enableInventory = computed<boolean>(() => isModuleEnabled('inventory'))
  const enableTelemetry = computed<boolean>(() => isModuleEnabled('telemetry'))

  const isMinimalMode = computed<boolean>(() => {
    return !enableTicketing.value || !enableInventory.value || !enableTelemetry.value
  })

  return {
    enableDevFeatures,
    enableDebugFeatures,
    enableSimulation,
    enableTicketing,
    enableInventory,
    enableTelemetry,
    isMinimalMode,
    isModuleEnabled,
    setModuleEnabled,
    resetModulesToDefault,
    getAppFeatureFlags
  }
}
