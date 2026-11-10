/**
 * Generates action URIs for QR codes used on the factory floor.
 * The URI scheme encodes an intent so that a mobile device scanning
 * the code can immediately open the right screen in the Heimdall PWA.
 */

export type QrAction =
  | 'report-incident'
  | 'view-station'
  | 'check-in'
  | 'view-ticket'
  | 'inspect-machine'
  | 'verify-pm'

export const QR_ACTIONS: readonly QrAction[] = [
  'report-incident',
  'view-station',
  'check-in',
  'view-ticket',
  'inspect-machine',
  'verify-pm'
] as const

const QR_PRIORITIES = ['Low', 'Medium', 'High', 'Critical'] as const

export function isQrAction(value: unknown): value is QrAction {
  return typeof value === 'string' && (QR_ACTIONS as readonly string[]).includes(value)
}

function asPriority(value: string | null): QrActionParams['prefillPriority'] {
  return value && (QR_PRIORITIES as readonly string[]).includes(value)
    ? (value as QrActionParams['prefillPriority'])
    : undefined
}

export interface QrActionParams {
  action: QrAction
  stationId?: string
  machineType?: string
  groupId?: string
  ticketId?: string
  prefillTitle?: string
  prefillPriority?: 'Low' | 'Medium' | 'High' | 'Critical'
}

/**
 * Build a fully-qualified Heimdall action URI that encodes all params
 * as query-string values. The base path is always `/mobile/action`.
 * Optional hostOverride allows specifying LAN IP/custom host so mobile phone cameras can reach it.
 */
export function generateQrUri(params: QrActionParams, hostOverride?: string): string {
  let base: string
  // Only accept http(s) host overrides so a typo/paste cannot produce e.g. a javascript: payload
  if (hostOverride && /^https?:\/\/[^\s/?#]+/i.test(hostOverride.trim())) {
    base = `${hostOverride.trim().replace(/\/+$/, '')}/mobile/action`
  } else if (typeof window !== 'undefined' && window.location.origin) {
    base = `${window.location.origin}/mobile/action`
  } else {
    base = '/mobile/action'
  }

  const query = new URLSearchParams()
  query.set('action', params.action)
  if (params.stationId) query.set('stationId', params.stationId)
  if (params.machineType) query.set('machineType', params.machineType)
  if (params.groupId) query.set('groupId', params.groupId)
  if (params.ticketId) query.set('ticketId', params.ticketId)
  if (params.prefillTitle) query.set('prefillTitle', params.prefillTitle)
  if (params.prefillPriority) query.set('prefillPriority', params.prefillPriority)

  return `${base}?${query.toString()}`
}

/**
 * Build a deep-link URI using the native heimdall:// scheme.
 */
export function generateHeimdallDeepLink(params: QrActionParams): string {
  const query = new URLSearchParams()
  query.set('action', params.action)
  if (params.stationId) query.set('stationId', params.stationId)
  if (params.machineType) query.set('machineType', params.machineType)
  if (params.groupId) query.set('groupId', params.groupId)
  if (params.ticketId) query.set('ticketId', params.ticketId)
  if (params.prefillTitle) query.set('prefillTitle', params.prefillTitle)
  if (params.prefillPriority) query.set('prefillPriority', params.prefillPriority)

  return `heimdall://action?${query.toString()}`
}

/**
 * Parse an action URI (web URL or heimdall:// scheme) into structured params.
 */
export function parseQrUri(raw: string): QrActionParams | null {
  try {
    let urlStr = raw.trim()
    if (!urlStr) return null

    if (urlStr.startsWith('heimdall://')) {
      urlStr = urlStr.replace('heimdall://', 'http://localhost/')
    }
    
    // Quick validation: must be a URL or schema URI containing action params
    if (!urlStr.includes('http://') && !urlStr.includes('https://') && !urlStr.startsWith('/')) {
      return null
    }

    const url = new URL(urlStr, 'http://localhost')
    
    // 1. Explicit action query param
    const rawAction = url.searchParams.get('action')
    if (rawAction && !isQrAction(rawAction)) return null
    const action = rawAction as QrAction | null
    if (action) {
      return {
        action,
        stationId: url.searchParams.get('stationId') || undefined,
        machineType: url.searchParams.get('machineType') || undefined,
        groupId: url.searchParams.get('groupId') || undefined,
        ticketId: url.searchParams.get('ticketId') || undefined,
        prefillTitle: url.searchParams.get('prefillTitle') || undefined,
        prefillPriority: asPriority(url.searchParams.get('prefillPriority'))
      }
    }

    // 2. Direct dashboard URLs
    if (url.pathname.includes('/tickets/new')) {
      return {
        action: 'report-incident',
        stationId: url.searchParams.get('stationId') || undefined,
        machineType: url.searchParams.get('machineType') || undefined,
        groupId: url.searchParams.get('groupId') || undefined,
        prefillTitle: url.searchParams.get('title') || url.searchParams.get('prefillTitle') || undefined,
        prefillPriority: asPriority(url.searchParams.get('priority'))
      }
    }

    const ticketMatch = url.pathname.match(/\/tickets\/([^/?#]+)/)
    if (ticketMatch && ticketMatch[1] && ticketMatch[1] !== 'new' && ticketMatch[1] !== 'templates') {
      return {
        action: 'view-ticket',
        ticketId: decodeURIComponent(ticketMatch[1])
      }
    }

    if (url.pathname.includes('/machines')) {
      return {
        action: 'view-station',
        stationId: url.searchParams.get('stationId') || undefined
      }
    }

    return null
  } catch {
    return null
  }
}
