import { computed, type ComputedRef } from 'vue'

export interface AppFeatureFlags {
  enableDevFeatures: boolean
  enableDebugFeatures: boolean
  enableSimulation: boolean
}

/**
 * Returns static snapshot of current application feature flags based on
 * runtime configuration or process environment.
 * - enableDevFeatures: Enabled if HEIMDALL_ENABLE_DEV === 'true' or NODE_ENV !== 'production'. In production, default is false.
 * - enableDebugFeatures: Enabled if HEIMDALL_ENABLE_DEBUG === 'true' or HEIMDALL_DEBUG === 'true'. In production, default is false.
 * - enableSimulation: Enabled if HEIMDALL_ENABLE_SIMULATION === 'true' or (enableDevFeatures && HEIMDALL_ENABLE_SIMULATION !== 'false').
 */
export function getAppFeatureFlags(): AppFeatureFlags {
  let devFromConfig: boolean | undefined
  let debugFromConfig: boolean | undefined
  let simFromConfig: boolean | undefined

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
  } catch {
    // Runtime config may not be initialized in vitest / standalone contexts
  }

  const isProduction = typeof process !== 'undefined'
    ? process.env.NODE_ENV === 'production'
    : false

  const enableDev = typeof process !== 'undefined' && process.env.HEIMDALL_ENABLE_DEV !== undefined
    ? process.env.HEIMDALL_ENABLE_DEV === 'true'
    : (devFromConfig !== undefined ? devFromConfig : (!isProduction && (typeof process === 'undefined' || process.env.HEIMDALL_ENABLE_DEV !== 'false')))

  const enableDebug = typeof process !== 'undefined' && (process.env.HEIMDALL_ENABLE_DEBUG !== undefined || process.env.HEIMDALL_DEBUG !== undefined)
    ? (process.env.HEIMDALL_ENABLE_DEBUG === 'true' || process.env.HEIMDALL_DEBUG === 'true')
    : (debugFromConfig !== undefined ? debugFromConfig : false)

  const enableSimulation = typeof process !== 'undefined' && process.env.HEIMDALL_ENABLE_SIMULATION !== undefined
    ? process.env.HEIMDALL_ENABLE_SIMULATION === 'true'
    : (simFromConfig !== undefined ? simFromConfig : (enableDev && (typeof process === 'undefined' || process.env.HEIMDALL_ENABLE_SIMULATION !== 'false')))

  return {
    enableDevFeatures: enableDev,
    enableDebugFeatures: enableDebug,
    enableSimulation
  }
}

/**
 * Vue composable providing reactive feature flags across the frontend application.
 */
export const useFeatureFlags = (): {
  enableDevFeatures: ComputedRef<boolean>
  enableDebugFeatures: ComputedRef<boolean>
  enableSimulation: ComputedRef<boolean>
  getAppFeatureFlags: () => AppFeatureFlags
} => {
  const enableDevFeatures = computed<boolean>(() => getAppFeatureFlags().enableDevFeatures)
  const enableDebugFeatures = computed<boolean>(() => getAppFeatureFlags().enableDebugFeatures)
  const enableSimulation = computed<boolean>(() => getAppFeatureFlags().enableSimulation)

  return {
    enableDevFeatures,
    enableDebugFeatures,
    enableSimulation,
    getAppFeatureFlags
  }
}
