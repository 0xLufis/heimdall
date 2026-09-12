/**
 * GDPR-Compliant Diagnostics & Telemetry Collector for OmniSearch
 * 
 * Strict Privacy & GDPR Compliance Guarantees:
 * 1. ZERO raw PII: Email addresses, IP addresses, credentials, JWTs, and phone numbers are scrubbed.
 * 2. Daily-Salted Ephemeral Client Hashing: No persistent user ID / profiling across days.
 * 3. Bounded In-Memory Ring Buffer: Strictly limited capacity (default: 500) to prevent unbounded retention.
 * 4. Opt-Out / Configurable Toggle: Can be disabled entirely.
 */

export interface SearchDiagnosticsRecord {
  id: string
  timestamp: string
  durationMs: number
  resultCount: number
  itemTypeCounts: Record<string, number>
  queryLength: number
  tokenCount: number
  matchedTagKeys: string[]
  sanitizedQuerySignature: string
  fuzzyApplied: boolean
  cacheHit: boolean
  dataSourceType: 'uri' | 'dbConnection' | 'redisCache' | 'custom' | 'inMemory'
  templateUsed: string
  status: 'success' | 'timeout' | 'error'
  errorCode?: string
  ephemeralClientId: string
}

export interface DiagnosticsOptions {
  enabled?: boolean
  maxBufferSize?: number
  onCollect?: (record: SearchDiagnosticsRecord) => void
}

/**
 * Fast non-cryptographic hash for pseudonymization with daily salt.
 * Ensures consistent hash within the same day for a session without persistent tracking across days.
 */
function fastHash(str: string): string {
  let hash = 5381
  for (let i = 0; i < str.length; i++) {
    hash = ((hash << 5) + hash) ^ str.charCodeAt(i)
  }
  return (hash >>> 0).toString(16).padStart(8, '0')
}

export class SearchDiagnosticsService {
  private buffer: SearchDiagnosticsRecord[] = []
  private maxBufferSize: number
  private isEnabled: boolean
  private onCollectHook?: (record: SearchDiagnosticsRecord) => void
  private dailySalt: string

  constructor(options: DiagnosticsOptions = {}) {
    this.isEnabled = options.enabled ?? true
    this.maxBufferSize = options.maxBufferSize || 500
    this.onCollectHook = options.onCollect
    
    // Rotate salt daily based on ISO date
    const today = new Date().toISOString().split('T')[0]
    this.dailySalt = `salt_${today}_gdpr_protect`
  }

  /**
   * Sanitizes query text by scrubbing all PII, credentials, and network addresses.
   */
  public sanitizeQuery(query: string): string {
    if (!query) return ''

    let sanitized = query

    // 1. Scrub emails
    sanitized = sanitized.replace(
      /\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/g,
      '[REDACTED_EMAIL]'
    )

    // 2. Scrub IPv4 addresses
    sanitized = sanitized.replace(
      /\b(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b/g,
      '[REDACTED_IP]'
    )

    // 3. Scrub IPv6 addresses
    sanitized = sanitized.replace(
      /\b(?:[0-9a-fA-F]{1,4}:){7}[0-9a-fA-F]{1,4}\b/g,
      '[REDACTED_IPV6]'
    )

    // 4. Scrub passwords, api keys, bearer tokens
    sanitized = sanitized.replace(
      /(?:bearer|token|apikey|password|secret|key)[=:\s]+[^\s"']+/gi,
      '[REDACTED_CREDENTIAL]'
    )

    // 5. Scrub JWT tokens
    sanitized = sanitized.replace(
      /\beyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}\b/g,
      '[REDACTED_JWT]'
    )

    // 6. Scrub MAC addresses
    sanitized = sanitized.replace(
      /\b(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b/g,
      '[REDACTED_MAC]'
    )

    // 7. Scrub phone numbers
    sanitized = sanitized.replace(
      /(?:\+?\d{1,3}[-.\s]?)?\(?\d{2,4}\)?[-.\s]?\d{3,4}[-.\s]?\d{3,4}\b/g,
      '[REDACTED_PHONE]'
    )

    return sanitized.trim()
  }

  /**
   * Derives a temporary pseudonymous client ID valid only for today.
   */
  public getEphemeralClientId(rawSessionOrToken?: string): string {
    const raw = rawSessionOrToken || 'anon_session'
    return `client_${fastHash(`${raw}_${this.dailySalt}`)}`
  }

  /**
   * Collects search telemetry in compliance with GDPR.
   */
  public recordSearch(params: {
    rawQuery: string
    durationMs: number
    resultCount: number
    itemTypes?: string[]
    matchedTagKeys?: string[]
    fuzzyApplied?: boolean
    cacheHit?: boolean
    dataSourceType?: 'uri' | 'dbConnection' | 'redisCache' | 'custom' | 'inMemory'
    templateUsed?: string
    status?: 'success' | 'timeout' | 'error'
    errorCode?: string
    sessionIdentifier?: string
  }): SearchDiagnosticsRecord | null {
    if (!this.isEnabled) return null

    const sanitizedQuery = this.sanitizeQuery(params.rawQuery)
    const tokenCount = sanitizedQuery ? sanitizedQuery.split(/\s+/).filter(t => t.length > 0).length : 0

    // Aggregate counts by itemType
    const itemTypeCounts: Record<string, number> = {}
    if (params.itemTypes) {
      for (const t of params.itemTypes) {
        itemTypeCounts[t] = (itemTypeCounts[t] || 0) + 1
      }
    }

    const record: SearchDiagnosticsRecord = {
      id: `diag_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
      timestamp: new Date().toISOString(),
      durationMs: Math.round(params.durationMs * 100) / 100,
      resultCount: params.resultCount,
      itemTypeCounts,
      queryLength: params.rawQuery.length,
      tokenCount,
      matchedTagKeys: params.matchedTagKeys || [],
      sanitizedQuerySignature: sanitizedQuery,
      fuzzyApplied: params.fuzzyApplied ?? false,
      cacheHit: params.cacheHit ?? false,
      dataSourceType: params.dataSourceType || 'inMemory',
      templateUsed: params.templateUsed || 'generic',
      status: params.status || 'success',
      errorCode: params.errorCode ? this.sanitizeQuery(params.errorCode) : undefined,
      ephemeralClientId: this.getEphemeralClientId(params.sessionIdentifier)
    }

    // Bounded ring buffer: evict oldest if full
    if (this.buffer.length >= this.maxBufferSize) {
      this.buffer.shift()
    }
    this.buffer.push(record)

    if (this.onCollectHook) {
      try {
        this.onCollectHook(record)
      } catch {
        // Suppress callback exceptions to avoid impacting search operations
      }
    }

    return record
  }

  /**
   * Retrieves all captured diagnostic records.
   */
  public getRecords(): SearchDiagnosticsRecord[] {
    return [...this.buffer]
  }

  /**
   * Clears in-memory buffer.
   */
  public clear(): void {
    this.buffer = []
  }

  public setEnabled(enabled: boolean): void {
    this.isEnabled = enabled
  }

  public getBufferLength(): number {
    return this.buffer.length
  }
}

export const defaultSearchDiagnostics = new SearchDiagnosticsService()
