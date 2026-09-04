import { ref, computed, watch, unref } from 'vue'
import { generateQrDataUrl } from '~/utils/qrSvgRenderer'
import {
  generateQrUri,
  generateHeimdallDeepLink,
  parseQrUri,
  type QrAction,
  type QrActionParams
} from '~/utils/qrActionGenerator'

export interface UseActionQrOptions {
  initialParams?: Partial<QrActionParams>
  qrWidth?: number
  qrDarkColor?: string
  qrLightColor?: string
  protocol?: 'web' | 'heimdall'
}

export function useActionQr(options: UseActionQrOptions = {}) {
  const action = ref<QrAction>(options.initialParams?.action || 'report-incident')
  const stationId = ref<string>(options.initialParams?.stationId || '')
  const stationName = ref<string>('')
  const machineType = ref<string>(options.initialParams?.machineType || '')
  const groupId = ref<string>(options.initialParams?.groupId || '')
  const ticketId = ref<string>(options.initialParams?.ticketId || '')
  const prefillTitle = ref<string>(options.initialParams?.prefillTitle || '')
  const prefillPriority = ref<'Low' | 'Medium' | 'High' | 'Critical' | undefined>(options.initialParams?.prefillPriority)

  const protocol = ref<'web' | 'heimdall'>(options.protocol || 'web')
  const qrWidth = ref<number>(options.qrWidth || 280)
  const qrDarkColor = ref<string>(options.qrDarkColor || '#e2e8f0')
  const qrLightColor = ref<string>(options.qrLightColor || '#0f172a')

  const qrDataUrl = ref<string>('')
  const isRendering = ref<boolean>(false)
  const renderError = ref<string | null>(null)
  const isCopied = ref<boolean>(false)

  const currentParams = computed<QrActionParams>(() => ({
    action: action.value,
    stationId: stationId.value || undefined,
    machineType: machineType.value || undefined,
    groupId: groupId.value || undefined,
    ticketId: ticketId.value || undefined,
    prefillTitle: prefillTitle.value || undefined,
    prefillPriority: prefillPriority.value || undefined
  }))

  const webUri = computed(() => generateQrUri(currentParams.value))
  const heimdallDeepLink = computed(() => generateHeimdallDeepLink(currentParams.value))

  const activeUri = computed(() => {
    return protocol.value === 'heimdall' ? heimdallDeepLink.value : webUri.value
  })

  async function refreshQr() {
    isRendering.value = true
    renderError.value = null
    try {
      qrDataUrl.value = generateQrDataUrl(activeUri.value, {
        width: qrWidth.value,
        margin: 2,
        darkColor: qrDarkColor.value,
        lightColor: qrLightColor.value
      })
    } catch (svgErr) {
      try {
        const qrcodeMod = await import('qrcode')
        const qrcode = (qrcodeMod as any).default || qrcodeMod
        qrDataUrl.value = await qrcode.toDataURL(activeUri.value, {
          width: qrWidth.value,
          margin: 2,
          color: {
            dark: qrDarkColor.value,
            light: qrLightColor.value
          }
        })
      } catch (err: any) {
        console.error('Failed to render action QR code:', err)
        renderError.value = err?.message || 'QR render error'
      }
    } finally {
      isRendering.value = false
    }
  }

  // Auto re-render QR code whenever params or styling changes
  watch([activeUri, qrWidth, qrDarkColor, qrLightColor], () => {
    refreshQr()
  }, { immediate: true })

  function setMachine(machine: {
    id?: string
    name?: string
    displayName?: string
    customIdentifier?: string
    machineType?: string
    groupId?: string
  }) {
    stationId.value = machine.customIdentifier || machine.name || machine.id || ''
    stationName.value = machine.displayName || machine.name || stationId.value
    if (machine.machineType) machineType.value = machine.machineType
    if (machine.groupId) groupId.value = machine.groupId
  }

  function setTicket(ticket: {
    id: string
    ticketNumber?: string
    title?: string
    stationId?: string
    stationName?: string
    priority?: any
  }) {
    action.value = 'view-ticket'
    ticketId.value = ticket.id
    if (ticket.stationId) stationId.value = ticket.stationId
    if (ticket.stationName) stationName.value = ticket.stationName
    if (ticket.title) prefillTitle.value = ticket.title
    if (ticket.priority) prefillPriority.value = ticket.priority
  }

  function setGroup(group: {
    id: string
    name?: string
    machineTypes?: string[]
  }) {
    groupId.value = group.id
    if (group.machineTypes && group.machineTypes.length > 0) {
      machineType.value = group.machineTypes[0]
    }
  }

  function parseFromCode(raw: string): QrActionParams | null {
    const parsed = parseQrUri(raw)
    if (parsed) {
      action.value = parsed.action
      stationId.value = parsed.stationId || ''
      machineType.value = parsed.machineType || ''
      groupId.value = parsed.groupId || ''
      ticketId.value = parsed.ticketId || ''
      prefillTitle.value = parsed.prefillTitle || ''
      prefillPriority.value = parsed.prefillPriority
    }
    return parsed
  }

  async function copyUri(): Promise<boolean> {
    try {
      if (typeof navigator !== 'undefined' && navigator.clipboard) {
        await navigator.clipboard.writeText(activeUri.value)
        isCopied.value = true
        setTimeout(() => { isCopied.value = false }, 2500)
        return true
      }
      return false
    } catch {
      return false
    }
  }

  function printQr(title: string = 'Floor Incident Dispatching QR Voucher') {
    if (typeof window === 'undefined') return
    const printWindow = window.open('', '_blank', 'width=600,height=700')
    if (!printWindow) return

    const html = `
      <!DOCTYPE html>
      <html>
        <head>
          <title>${title}</title>
          <style>
            body { font-family: system-ui, -apple-system, sans-serif; padding: 40px; text-align: center; color: #0f172a; }
            .card { border: 2px solid #334155; border-radius: 24px; padding: 32px; max-width: 440px; margin: 0 auto; }
            h2 { margin: 0 0 8px; text-transform: uppercase; font-size: 20px; letter-spacing: 0.05em; }
            p { margin: 4px 0 16px; font-size: 13px; color: #64748b; }
            img { width: 280px; height: 280px; margin: 16px 0; }
            .uri { font-family: monospace; font-size: 10px; word-break: break-all; background: #f1f5f9; padding: 8px; border-radius: 8px; }
            .badge { display: inline-block; padding: 4px 12px; background: #4f46e5; color: white; border-radius: 9999px; font-size: 11px; font-weight: bold; text-transform: uppercase; margin-bottom: 12px; }
          </style>
        </head>
        <body>
          <div class="card">
            <span class="badge">Heimdall Action QR</span>
            <h2>${stationName.value || stationId.value || 'Industrial Station'}</h2>
            <p>Action: <strong>${action.value}</strong> ${machineType.value ? '• ' + machineType.value : ''}</p>
            <img src="${qrDataUrl.value}" alt="QR Code" />
            <div class="uri">${activeUri.value}</div>
          </div>
          <script>
            window.onload = () => { window.print(); window.close(); }
          </script>
        </body>
      </html>
    `
    printWindow.document.write(html)
    printWindow.document.close()
  }

  function downloadPng(filename?: string) {
    if (!qrDataUrl.value || typeof document === 'undefined') return
    const link = document.createElement('a')
    link.download = filename || `heimdall-qr-${stationId.value || action.value}-${Date.now()}.png`
    link.href = qrDataUrl.value
    link.click()
  }

  return {
    action,
    stationId,
    stationName,
    machineType,
    groupId,
    ticketId,
    prefillTitle,
    prefillPriority,
    protocol,
    qrWidth,
    qrDarkColor,
    qrLightColor,
    qrDataUrl,
    isRendering,
    renderError,
    isCopied,
    currentParams,
    webUri,
    heimdallDeepLink,
    activeUri,
    refreshQr,
    setMachine,
    setTicket,
    setGroup,
    parseFromCode,
    copyUri,
    printQr,
    downloadPng
  }
}
