import { defineEventHandler, readBody, getMethod, createError } from 'h3'
import { assertSimulationEnabled } from '../../utils/featureFlags'

/**
 * Dev/demo-only proxy to the fleet simulator's control API (simulators/fleet/fleet_simulator.py).
 * The simulator API is unauthenticated and bound to loopback, so browsers only reach it through
 * this route, which is gated by the simulation feature flag.
 */

/** Read-only endpoints (GET). */
const READ_ACTIONS = new Set(['status', 'stations'])
/** Scenario endpoints (POST). Anything else is rejected instead of being forwarded. */
const WRITE_ACTIONS = new Set([
  'fault',
  'clear-faults',
  'rate',
  'speed',
  'scenario',
  'recover',
  'line/stop',
  'station/fault',
  'station/drift',
  'station/maintenance'
])

/** SIMULATOR_HOST wins; otherwise a locally started simulator, then the docker compose service. */
const simulatorHosts = (): string[] => {
  const configured = process.env.SIMULATOR_HOST?.trim()
  if (configured) return [configured.replace(/\/+$/, '')]
  return ['http://127.0.0.1:5055', 'http://simulator:5055']
}

const offlineStatus = (error: string) => ({
  sim_offline: true,
  error,
  active_fleet: 0,
  active_stations: 0,
  total_dispatched: 0,
  total_errors: 0,
  fault_rate: 0,
  speed: 1,
  transport: { protocol: 'mqtt5', broker: null, connected: false, error: 'simulator offline' },
  lines: [],
  drift_profiles: [],
  scenarios: [],
  nodes: [],
  stations: []
})

export default defineEventHandler(async (event) => {
  assertSimulationEnabled()
  const method = getMethod(event)
  const raw = event.context.params?.action
  const action = (Array.isArray(raw) ? raw.join('/') : raw || 'status').replace(/^\/+|\/+$/g, '')

  const isRead = method === 'GET' && READ_ACTIONS.has(action)
  const isWrite = method === 'POST' && WRITE_ACTIONS.has(action)
  if (!isRead && !isWrite) {
    throw createError({ statusCode: 404, statusMessage: `Unknown simulator action: ${method} ${action}` })
  }

  const body = isWrite ? await readBody(event).catch(() => ({})) : undefined

  let lastError = 'simulator not reachable'
  for (const host of simulatorHosts()) {
    try {
      return await $fetch(`${host}/api/${action}`, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: isWrite ? (body ?? {}) : undefined,
        timeout: 2500,
        retry: 0
      })
    } catch (err: any) {
      const status = err?.response?.status ?? err?.statusCode
      // The simulator answered: pass its verdict on (unknown station, bad profile ...).
      if (typeof status === 'number' && status >= 400 && status < 500) {
        throw createError({
          statusCode: status,
          statusMessage: err?.data?.message || err?.response?._data?.message || 'Simulator rejected the request'
        })
      }
      lastError = err?.message || lastError
    }
  }

  // Simulator offline: reads report it explicitly (no invented fleet numbers), writes fail.
  if (isRead) return offlineStatus(lastError)
  throw createError({ statusCode: 503, statusMessage: 'Fleet simulator is offline (start it with `just start simulator`)' })
})
