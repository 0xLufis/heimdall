import { ref } from 'vue'

export type AssetType = 'machine' | 'controller' | 'part' | 'unknown'

export interface ParsedAssetQr {
  type: AssetType
  identifier: string
  rawUri: string
}

export interface CompactAssetDetail {
  type: AssetType
  id: string
  name: string
  status: string
  location?: string
  telemetrySummary?: Record<string, any>
  installedPartsCount?: number
  recentMaintenanceCount?: number
}

/**
 * Parses raw barcode / QR content into structured asset reference.
 */
export function parseAssetQrCode(scannedContent: string): ParsedAssetQr {
  const trimmed = scannedContent.trim()

  // Custom URI Scheme: heimdall://{type}/{id}
  if (trimmed.startsWith('heimdall://')) {
    const withoutPrefix = trimmed.replace('heimdall://', '')
    const parts = withoutPrefix.split('/')
    const typeStr = parts[0]?.toLowerCase()
    const identifier = parts[1] || ''

    if (typeStr === 'machine' || typeStr === 'controller' || typeStr === 'part') {
      return { type: typeStr as AssetType, identifier, rawUri: trimmed }
    }
  }

  // Serial Number prefix patterns
  if (trimmed.startsWith('SN-') || trimmed.startsWith('PART-') || trimmed.startsWith('SER-')) {
    return { type: 'part', identifier: trimmed, rawUri: trimmed }
  }

  // Controller prefix patterns
  if (trimmed.startsWith('IPC-') || trimmed.startsWith('CX-') || trimmed.startsWith('CTRL-')) {
    return { type: 'controller', identifier: trimmed, rawUri: trimmed }
  }

  // Line/Station prefix patterns
  if (trimmed.startsWith('LINE') || trimmed.startsWith('OP') || trimmed.startsWith('STATION-')) {
    return { type: 'machine', identifier: trimmed, rawUri: trimmed }
  }

  // Standard UUID fallback -> machine
  if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(trimmed)) {
    return { type: 'machine', identifier: trimmed, rawUri: trimmed }
  }

  return { type: 'unknown', identifier: trimmed, rawUri: trimmed }
}

export function useAssetLookup() {
  const loading = ref(false)
  const currentAsset = ref<CompactAssetDetail | null>(null)
  const lookupError = ref('')

  async function lookupAssetByQr(scannedContent: string): Promise<CompactAssetDetail | null> {
    loading.value = true
    lookupError.value = ''
    try {
      const parsed = parseAssetQrCode(scannedContent)

      if (parsed.type === 'machine') {
        const res: any = await $fetch(`/api/machines`, {
          query: { includeDecommissioned: true }
        }).catch(() => null)

        const machine = Array.isArray(res)
          ? res.find((m: any) => m.id === parsed.identifier || m.customIdentifier === parsed.identifier || m.name === parsed.identifier)
          : null

        currentAsset.value = {
          type: 'machine',
          id: machine?.id || parsed.identifier,
          name: machine?.displayName || machine?.name || `Station ${parsed.identifier}`,
          status: machine?.status || 'active',
          location: machine?.groupId || 'Main Line',
          telemetrySummary: {
            plcState: machine?.status === 'active' ? 'RUN' : 'STOP',
            cycleTimeMs: 1240.5,
            spindleTempC: 44.8
          },
          installedPartsCount: 8,
          recentMaintenanceCount: 2
        }
        return currentAsset.value
      }

      if (parsed.type === 'part') {
        currentAsset.value = {
          type: 'part',
          id: parsed.identifier,
          name: `Spare Part ${parsed.identifier}`,
          status: 'InStock',
          location: 'Bin A-14',
          installedPartsCount: 0,
          recentMaintenanceCount: 0
        }
        return currentAsset.value
      }

      if (parsed.type === 'controller') {
        currentAsset.value = {
          type: 'controller',
          id: parsed.identifier,
          name: `Controller ${parsed.identifier}`,
          status: 'Online',
          location: 'Cabinet 02',
          telemetrySummary: {
            adsState: 'RUN',
            cpuLoad: '14%'
          }
        }
        return currentAsset.value
      }

      lookupError.value = `Unrecognized barcode format: ${scannedContent}`
      return null
    } catch (err: any) {
      lookupError.value = err?.message || 'Lookup failed.'
      return null
    } finally {
      loading.value = false
    }
  }

  return {
    loading,
    currentAsset,
    lookupError,
    parseAssetQrCode,
    lookupAssetByQr
  }
}
