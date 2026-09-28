import { createError } from 'h3'

export interface ServerFeatureFlags {
  enableDevFeatures: boolean
  enableDebugFeatures: boolean
  enableSimulation: boolean
}

/**
 * Evaluates feature flags for the Nitro server / BFF layer.
 * - enableDevFeatures: Enabled if HEIMDALL_ENABLE_DEV === 'true' or NODE_ENV !== 'production'. In production, default is false.
 * - enableDebugFeatures: Enabled if HEIMDALL_ENABLE_DEBUG === 'true' or HEIMDALL_DEBUG === 'true'. In production, default is false.
 * - enableSimulation: Enabled if HEIMDALL_ENABLE_SIMULATION === 'true' or (!isProduction && enableDevFeatures && HEIMDALL_ENABLE_SIMULATION !== 'false').
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

  return {
    enableDevFeatures,
    enableDebugFeatures,
    enableSimulation
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
