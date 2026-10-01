import { createError } from 'h3'

export interface ServerFeatureFlags {
  enableDevFeatures: boolean
  enableDebugFeatures: boolean
  enableSimulation: boolean
  enableTicketing: boolean
  enableInventory: boolean
  enableTelemetry: boolean
}

/**
 * Evaluates feature flags for the Nitro server / BFF layer.
 * - enableDevFeatures: Enabled if HEIMDALL_ENABLE_DEV === 'true' or NODE_ENV !== 'production'. In production, default is false.
 * - enableDebugFeatures: Enabled if HEIMDALL_ENABLE_DEBUG === 'true' or HEIMDALL_DEBUG === 'true'. In production, default is false.
 * - enableSimulation: Enabled if HEIMDALL_ENABLE_SIMULATION === 'true' or (!isProduction && enableDevFeatures && HEIMDALL_ENABLE_SIMULATION !== 'false').
 * - enableTicketing: Enabled unless HEIMDALL_MODULE_TICKETING === 'false'.
 * - enableInventory: Enabled unless HEIMDALL_MODULE_INVENTORY === 'false'.
 * - enableTelemetry: Enabled unless HEIMDALL_MODULE_TELEMETRY === 'false'.
 */
export function getFeatureFlags(): ServerFeatureFlags {
  const isProduction = process.env.NODE_ENV === 'production'
  const enableDevFeatures =
    process.env.HEIMDALL_ENABLE_DEV === 'true' ||
    (!isProduction && process.env.HEIMDALL_ENABLE_DEV !== 'false')
  const enableDebugFeatures =
    process.env.HEIMDALL_ENABLE_DEBUG === 'true' ||
    process.env.HEIMDALL_DEBUG === 'true'
  const enableSimulation =
    process.env.HEIMDALL_ENABLE_SIMULATION === 'true' ||
    (enableDevFeatures && process.env.HEIMDALL_ENABLE_SIMULATION !== 'false')

  const enableTicketing = process.env.HEIMDALL_MODULE_TICKETING !== 'false'
  const enableInventory = process.env.HEIMDALL_MODULE_INVENTORY !== 'false'
  const enableTelemetry = process.env.HEIMDALL_MODULE_TELEMETRY !== 'false'

  return {
    enableDevFeatures,
    enableDebugFeatures,
    enableSimulation,
    enableTicketing,
    enableInventory,
    enableTelemetry
  }
}

export const featureFlags = {
  get enableDevFeatures(): boolean {
    return getFeatureFlags().enableDevFeatures
  },
  get enableDebugFeatures(): boolean {
    return getFeatureFlags().enableDebugFeatures
  },
  get enableSimulation(): boolean {
    return getFeatureFlags().enableSimulation
  },
  get enableTicketing(): boolean {
    return getFeatureFlags().enableTicketing
  },
  get enableInventory(): boolean {
    return getFeatureFlags().enableInventory
  },
  get enableTelemetry(): boolean {
    return getFeatureFlags().enableTelemetry
  }
}

/**
 * Throws a 403 Forbidden error if development features are disabled.
 */
export function assertDevFeaturesEnabled(): void {
  if (!getFeatureFlags().enableDevFeatures) {
    throw createError({
      statusCode: 403,
      statusMessage: 'Forbidden: Development features are disabled in this environment.'
    })
  }
}

/**
 * Throws a 403 Forbidden error if debug features are disabled.
 */
export function assertDebugFeaturesEnabled(): void {
  if (!getFeatureFlags().enableDebugFeatures) {
    throw createError({
      statusCode: 403,
      statusMessage: 'Forbidden: Debug features are disabled in this environment.'
    })
  }
}

/**
 * Throws a 403 Forbidden error if simulation features are disabled.
 */
export function assertSimulationEnabled(): void {
  if (!getFeatureFlags().enableSimulation || !getFeatureFlags().enableDevFeatures) {
    throw createError({
      statusCode: 403,
      statusMessage: 'Forbidden: Simulation features are disabled in this environment.'
    })
  }
}

/**
 * Throws a 503 Service Unavailable error if the specified platform module is disabled.
 */
export function assertModuleEnabled(module: 'core' | 'ticketing' | 'inventory' | 'telemetry'): void {
  if (module === 'core') return
  const flags = getFeatureFlags()
  if (module === 'ticketing' && !flags.enableTicketing) {
    throw createError({
      statusCode: 503,
      statusMessage: "Service Unavailable: Module 'ticketing' is currently disabled on this instance."
    })
  }
  if (module === 'inventory' && !flags.enableInventory) {
    throw createError({
      statusCode: 503,
      statusMessage: "Service Unavailable: Module 'inventory' is currently disabled on this instance."
    })
  }
  if (module === 'telemetry' && !flags.enableTelemetry) {
    throw createError({
      statusCode: 503,
      statusMessage: "Service Unavailable: Module 'telemetry' is currently disabled on this instance."
    })
  }
}
