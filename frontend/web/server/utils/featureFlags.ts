import { createError } from 'h3'

export interface ServerFeatureFlags {
  enableDevFeatures: boolean
  enableDebugFeatures: boolean
}

/**
 * Evaluates feature flags for the Nitro server / BFF layer.
 * - enableDevFeatures: Enabled if HEIMDALL_ENABLE_DEV === 'true' or NODE_ENV !== 'production'. In production, default is false.
 * - enableDebugFeatures: Enabled if HEIMDALL_ENABLE_DEBUG === 'true' or HEIMDALL_DEBUG === 'true'. In production, default is false.
 */
export function getFeatureFlags(): ServerFeatureFlags {
  const isProduction = process.env.NODE_ENV === 'production'
  const enableDevFeatures =
    process.env.HEIMDALL_ENABLE_DEV === 'true' ||
    (!isProduction && process.env.HEIMDALL_ENABLE_DEV !== 'false')
  const enableDebugFeatures =
    process.env.HEIMDALL_ENABLE_DEBUG === 'true' ||
    process.env.HEIMDALL_DEBUG === 'true'

  return {
    enableDevFeatures,
    enableDebugFeatures
  }
}

export const featureFlags = {
  get enableDevFeatures(): boolean {
    return getFeatureFlags().enableDevFeatures
  },
  get enableDebugFeatures(): boolean {
    return getFeatureFlags().enableDebugFeatures
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
