import { computed, type ComputedRef } from 'vue'

export interface AppFeatureFlags {
  enableDevFeatures: boolean
  enableDebugFeatures: boolean
}

/**
 * Returns static snapshot of current application feature flags based on
 * runtime configuration or process environment.
 * - enableDevFeatures: Enabled if HEIMDALL_ENABLE_DEV === 'true' or NODE_ENV !== 'production'. In production, default is false.
 * - enableDebugFeatures: Enabled if HEIMDALL_ENABLE_DEBUG === 'true' or HEIMDALL_DEBUG === 'true'. In production, default is false.
 */
export function getAppFeatureFlags(): AppFeatureFlags {
  let devFromConfig: boolean | undefined
  let debugFromConfig: boolean | undefined

  try {
    const config = useRuntimeConfig()
    if (config?.public?.enableDevFeatures !== undefined) {
      devFromConfig = Boolean(config.public.enableDevFeatures)
    }
    if (config?.public?.enableDebugFeatures !== undefined) {
      debugFromConfig = Boolean(config.public.enableDebugFeatures)
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

  return {
    enableDevFeatures: enableDev,
    enableDebugFeatures: enableDebug
  }
}

/**
 * Vue composable providing reactive feature flags across the frontend application.
 */
export const useFeatureFlags = (): {
  enableDevFeatures: ComputedRef<boolean>
  enableDebugFeatures: ComputedRef<boolean>
  getAppFeatureFlags: () => AppFeatureFlags
} => {
  const enableDevFeatures = computed<boolean>(() => getAppFeatureFlags().enableDevFeatures)
  const enableDebugFeatures = computed<boolean>(() => getAppFeatureFlags().enableDebugFeatures)

  return {
    enableDevFeatures,
    enableDebugFeatures,
    getAppFeatureFlags
  }
}
