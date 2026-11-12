/**
 * Remote desktop links of a controller, resolved through the controller itself.
 *
 * The backend stores the link per controller (`system_metadata.RemoteAccess.Vnc`) and resolves it
 * in `GET /api/v1/ClientPc/{id}/remote-access`. There is no client-side default endpoint: a
 * controller without a link resolves to `vnc: null` and the viewer says so instead of opening
 * some other machine's desktop. Several controllers may be linked to the same endpoint (in a dev
 * environment every controller is typically linked to the one Windows container).
 */

export interface VncEndpoint {
  /** Null/empty: the host this web UI is served from (gateway or local dev container). */
  host: string | null
  port: number
  /** Websocket path without a leading slash. */
  path: string
  /** Force wss:// even when the page is served over http. */
  secure: boolean
}

export interface ControllerRemoteAccess {
  controllerId: string
  hostname: string | null
  ipAddress: string | null
  vnc: VncEndpoint | null
}

const HOST_PATTERN = /^(\[[0-9A-Fa-f:.]{2,45}\]|[A-Za-z0-9]([A-Za-z0-9.-]{0,251}[A-Za-z0-9])?)$/
const PATH_PATTERN = /^[A-Za-z0-9._~\-/?=&%]{0,256}$/

/** Validates and normalizes an endpoint; returns null when it is not usable. */
export function normalizeVncEndpoint(raw: unknown): VncEndpoint | null {
  if (!raw || typeof raw !== 'object') return null
  const r = raw as Record<string, unknown>
  const port = typeof r.port === 'string' && r.port.trim() !== '' ? Number(r.port) : r.port
  if (typeof port !== 'number' || !Number.isInteger(port) || port < 1 || port > 65535) return null

  const host = typeof r.host === 'string' && r.host.trim() !== '' ? r.host.trim() : null
  if (host !== null && !HOST_PATTERN.test(host)) return null

  const path = (typeof r.path === 'string' ? r.path : 'websockify').trim().replace(/^\/+/, '')
  if (!PATH_PATTERN.test(path) || path.includes('..') || path.includes('//')) return null

  return { host, port, path, secure: r.secure === true }
}

/** Normalizes the backend `ControllerRemoteAccessDto` (camelCase JSON). */
export function normalizeRemoteAccess(raw: unknown, controllerId: string): ControllerRemoteAccess {
  const r = (raw && typeof raw === 'object' ? raw : {}) as Record<string, unknown>
  return {
    controllerId: typeof r.controllerId === 'string' && r.controllerId ? r.controllerId : controllerId,
    hostname: typeof r.hostname === 'string' ? r.hostname : null,
    ipAddress: typeof r.ipAddress === 'string' ? r.ipAddress : null,
    vnc: normalizeVncEndpoint(r.vnc)
  }
}

/**
 * Websocket URL for an endpoint. `pageLocation` supplies the host for host-less endpoints and
 * the protocol (an https page may only open wss:// sockets).
 */
export function buildVncWebSocketUrl(
  endpoint: VncEndpoint,
  pageLocation: { protocol: string; hostname: string }
): string {
  const scheme = endpoint.secure || pageLocation.protocol === 'https:' ? 'wss:' : 'ws:'
  const host = endpoint.host || pageLocation.hostname
  return `${scheme}//${host}:${endpoint.port}/${endpoint.path}`
}

/** Human-readable `host:port/path` for status lines. */
export function describeVncEndpoint(endpoint: VncEndpoint | null, pageHostname = ''): string {
  if (!endpoint) return 'not linked'
  return `${endpoint.host || pageHostname || 'this host'}:${endpoint.port}/${endpoint.path}`
}

export const useControllerRemoteAccess = () => {
  const basePath = (controllerId: string) => `/api/proxy/ClientPc/${encodeURIComponent(controllerId)}/remote-access`

  /** Resolves the links of one controller. Throws when the backend cannot be reached. */
  const fetchRemoteAccess = async (controllerId: string): Promise<ControllerRemoteAccess> => {
    const data = await $fetch<unknown>(basePath(controllerId))
    return normalizeRemoteAccess(data, controllerId)
  }

  /** Links a VNC endpoint to the controller (requires endpoint-management permission). */
  const saveVncEndpoint = async (controllerId: string, endpoint: VncEndpoint): Promise<ControllerRemoteAccess> => {
    const data = await $fetch<unknown>(`${basePath(controllerId)}/vnc`, { method: 'PUT', body: endpoint })
    return normalizeRemoteAccess(data, controllerId)
  }

  const clearVncEndpoint = async (controllerId: string): Promise<void> => {
    await $fetch(`${basePath(controllerId)}/vnc`, { method: 'DELETE' })
  }

  return { fetchRemoteAccess, saveVncEndpoint, clearVncEndpoint }
}
