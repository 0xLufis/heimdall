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
 */
export function generateQrUri(params: QrActionParams): string {
  const base =
    typeof window !== 'undefined'
      ? `${window.location.origin}/mobile/action`
      : '/mobile/action'

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
    if (urlStr.startsWith('heimdall://')) {
      urlStr = urlStr.replace('heimdall://', 'http://localhost/')
    }
    const url = new URL(urlStr, 'http://localhost')
    const action = url.searchParams.get('action') as QrAction
    if (!action) return null
    return {
      action,
      stationId: url.searchParams.get('stationId') || undefined,
      machineType: url.searchParams.get('machineType') || undefined,
      groupId: url.searchParams.get('groupId') || undefined,
      ticketId: url.searchParams.get('ticketId') || undefined,
      prefillTitle: url.searchParams.get('prefillTitle') || undefined,
      prefillPriority: (url.searchParams.get('prefillPriority') as any) || undefined
    }
  } catch {
    return null
  }
}
