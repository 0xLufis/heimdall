/**
 * Industrial OT Platform Module & Feature Flag Environment Resolution Utilities.
 */

export function parseEnvBoolean(val: unknown): boolean | undefined {
  if (val === undefined || val === null || val === '') return undefined
  if (typeof val === 'boolean') return val
  if (typeof val === 'number') return val !== 0

  if (typeof val === 'string') {
    const trimmed = val.trim().toLowerCase()
    if (['true', '1', 'yes', 'on', 'enabled'].includes(trimmed)) return true
    if (['false', '0', 'no', 'off', 'disabled'].includes(trimmed)) return false
  }

  return undefined
}

export interface ModuleFlagsResult {
  enableTicketing: boolean
  enableInventory: boolean
  enableTelemetry: boolean
}

/**
 * Resolves module activation status from environment variables supporting:
 * 1. Preset deployment profiles (HEIMDALL_MODULE_PRESET / HEIMDALL_PRESET):
 *    - 'all' | 'full' | 'monolith': all active
 *    - 'telemetry-ticketing': ticketing & telemetry active, inventory disabled
 *    - 'ticketing-only' | 'dispatch': ticketing active, inventory & telemetry disabled
 *    - 'telemetry-only' | 'edge': telemetry active, ticketing & inventory disabled
 *    - 'core-only' | 'lean' | 'minimal': all addons disabled
 * 2. Comma-separated inclusion list (HEIMDALL_MODULES / HEIMDALL_ENABLED_MODULES)
 * 3. Comma-separated exclusion list (HEIMDALL_DISABLED_MODULES / HEIMDALL_DISABLE_MODULES)
 * 4. Explicit individual module flags (highest priority):
 *    - HEIMDALL_MODULE_TICKETING, HEIMDALL_ENABLE_TICKETING, ENABLE_TICKETING, NUXT_PUBLIC_ENABLE_TICKETING
 *    - HEIMDALL_MODULE_INVENTORY, HEIMDALL_ENABLE_INVENTORY, ENABLE_INVENTORY, NUXT_PUBLIC_ENABLE_INVENTORY
 *    - HEIMDALL_MODULE_TELEMETRY, HEIMDALL_ENABLE_TELEMETRY, ENABLE_TELEMETRY, NUXT_PUBLIC_ENABLE_TELEMETRY
 */
export function resolveModuleFlagsFromEnv(env: Record<string, string | undefined> = typeof process !== 'undefined' ? (process.env as any) : {}): ModuleFlagsResult {
  let ticketing = true
  let inventory = true
  let telemetry = true

  if (!env) {
    return { enableTicketing: ticketing, enableInventory: inventory, enableTelemetry: telemetry }
  }

  // 1. Preset deployment profile
  const preset = (env.HEIMDALL_MODULE_PRESET || env.HEIMDALL_PRESET)?.trim().toLowerCase()
  if (preset) {
    if (preset === 'telemetry-ticketing') {
      ticketing = true
      inventory = false
      telemetry = true
    } else if (preset === 'ticketing-only' || preset === 'dispatch') {
      ticketing = true
      inventory = false
      telemetry = false
    } else if (preset === 'telemetry-only' || preset === 'edge') {
      ticketing = false
      inventory = false
      telemetry = true
    } else if (preset === 'core-only' || preset === 'lean' || preset === 'minimal') {
      ticketing = false
      inventory = false
      telemetry = false
    } else if (preset === 'all' || preset === 'full' || preset === 'monolith') {
      ticketing = true
      inventory = true
      telemetry = true
    }
  }

  // 2. Explicit inclusion list (HEIMDALL_MODULES="core,ticketing")
  const modulesList = env.HEIMDALL_MODULES || env.HEIMDALL_ENABLED_MODULES
  if (modulesList !== undefined && modulesList.trim() !== '') {
    const allowed = modulesList.split(',').map(s => s.trim().toLowerCase()).filter(Boolean)
    ticketing = allowed.includes('ticketing') || allowed.includes('tickets') || allowed.includes('maintenance-ticketing')
    inventory = allowed.includes('inventory') || allowed.includes('parts') || allowed.includes('inventory-assets')
    telemetry = allowed.includes('telemetry') || allowed.includes('industrial-telemetry')
  }

  // 3. Explicit exclusion list (HEIMDALL_DISABLED_MODULES="inventory")
  const disabledList = env.HEIMDALL_DISABLED_MODULES || env.HEIMDALL_DISABLE_MODULES
  if (disabledList !== undefined && disabledList.trim() !== '') {
    const disabled = disabledList.split(',').map(s => s.trim().toLowerCase()).filter(Boolean)
    if (disabled.includes('ticketing') || disabled.includes('tickets') || disabled.includes('maintenance-ticketing')) {
      ticketing = false
    }
    if (disabled.includes('inventory') || disabled.includes('parts') || disabled.includes('inventory-assets')) {
      inventory = false
    }
    if (disabled.includes('telemetry') || disabled.includes('industrial-telemetry')) {
      telemetry = false
    }
  }

  // 4. Individual explicit module overrides (highest precedence)
  const explicitTicketing = parseEnvBoolean(
    env.HEIMDALL_MODULE_TICKETING ??
    env.HEIMDALL_ENABLE_TICKETING ??
    env.ENABLE_TICKETING ??
    env.NUXT_PUBLIC_ENABLE_TICKETING
  )
  if (explicitTicketing !== undefined) {
    ticketing = explicitTicketing
  }

  const explicitInventory = parseEnvBoolean(
    env.HEIMDALL_MODULE_INVENTORY ??
    env.HEIMDALL_ENABLE_INVENTORY ??
    env.ENABLE_INVENTORY ??
    env.NUXT_PUBLIC_ENABLE_INVENTORY
  )
  if (explicitInventory !== undefined) {
    inventory = explicitInventory
  }

  const explicitTelemetry = parseEnvBoolean(
    env.HEIMDALL_MODULE_TELEMETRY ??
    env.HEIMDALL_ENABLE_TELEMETRY ??
    env.ENABLE_TELEMETRY ??
    env.NUXT_PUBLIC_ENABLE_TELEMETRY
  )
  if (explicitTelemetry !== undefined) {
    telemetry = explicitTelemetry
  }

  return {
    enableTicketing: ticketing,
    enableInventory: inventory,
    enableTelemetry: telemetry
  }
}
