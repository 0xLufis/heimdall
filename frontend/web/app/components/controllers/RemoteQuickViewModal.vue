<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch, nextTick } from 'vue'
import type { IndustrialController } from '~/types/domain'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import RbacButton from '~/components/common/RbacButton.vue'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'
import { createRfbClient } from '~/composables/useRfbClient'
import {
  Monitor,
  ExternalLink,
  Copy,
  Check,
  RefreshCw,
  Maximize2,
  Lock,
  Radio,
  Sliders,
  ShieldCheck,
  Zap,
  Terminal,
  Cpu,
  Power,
  Settings2,
  AlertCircle,
  Play,
  RotateCcw,
  Keyboard,
  Minimize2,
  Smartphone
} from 'lucide-vue-next'

const props = defineProps<{
  controller: IndustrialController | null
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
}>()

const { canExecuteRemote } = useRbacPermission()

type RemoteProvider = 'vnc' | 'dameware' | 'rdp'
const activeProvider = ref<RemoteProvider>('vnc')
const isCopied = ref(false)
const isConnected = ref(false)
const isReadOnly = ref(true)
const isFullscreen = ref(false)
const canvasResolution = ref<'1024x768' | '1280x1024' | '1920x1080'>('1024x768')
const damewarePort = ref(6129)
const rdpPort = ref(3389)
const latencyMs = ref(18)
const fps = ref(30)

// VNC & RFB State
type VncMode = 'live' | 'simulation'
type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'error'
const vncMode = ref<VncMode>('live')
const connectionState = ref<ConnectionState>('disconnected')
const errorMessage = ref('')
const desktopName = ref('')
const showSettings = ref(false)

// Connection parameters
const vncHost = ref('')
const vncPort = ref(8006)
const vncPath = ref('websockify')

// DOM references
const vncContainerRef = ref<HTMLDivElement | null>(null)
const viewportWrapperRef = ref<HTMLDivElement | null>(null)

const toggleFullscreen = async () => {
  if (typeof document === 'undefined') return
  if (!document.fullscreenElement) {
    if (viewportWrapperRef.value && viewportWrapperRef.value.requestFullscreen) {
      try {
        await viewportWrapperRef.value.requestFullscreen()
        isFullscreen.value = true
        return
      } catch {
        // Fallback to CSS expanded mode
      }
    }
    isFullscreen.value = !isFullscreen.value
  } else {
    try {
      await document.exitFullscreen()
    } catch {}
    isFullscreen.value = false
  }
}
const vncCanvasRef = ref<HTMLCanvasElement | null>(null)

let rfbInstance: any = null
let animationFrameId: number | null = null

// Initialize host and port defaults based on controller
const initConnectionParams = () => {
  if (typeof window !== 'undefined') {
    vncHost.value = window.location.hostname || '127.0.0.1'
  } else {
    vncHost.value = '127.0.0.1'
  }
  // Default to port 8006 for dockurr/windows
  vncPort.value = 8006
  vncPath.value = 'websockify'
}

// Deep-link URIs
const damewareUri = computed(() => {
  const ip = props.controller?.ipAddress || vncHost.value || '127.0.0.1'
  return `dwmrc://${ip}?port=${damewarePort.value}&use_cur_creds=1`
})

const rdpDownloadContent = computed(() => {
  const ip = props.controller?.ipAddress || vncHost.value || '127.0.0.1'
  return `screen mode id:i:2\nuse multimon:i:0\ndesktopwidth:i:1920\ndesktopheight:i:1080\nsession bpp:i:32\nwinposstr:s:0,1,0,0,800,600\nfull address:s:${ip}:${rdpPort.value}\nprompt for credentials:i:1\nnegotiate security layer:i:1`
})

const handleCopyDamewareUri = async () => {
  try {
    await navigator.clipboard.writeText(damewareUri.value)
    isCopied.value = true
    setTimeout(() => {
      isCopied.value = false
    }, 2000)
  } catch {
    // Fallback
  }
}

const handleDownloadRdp = () => {
  const blob = new Blob([rdpDownloadContent.value], { type: 'application/x-rdp' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `${props.controller?.name || 'controller'}_remote.rdp`
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

const handleLaunchDameware = () => {
  if (typeof window !== 'undefined') {
    window.location.href = damewareUri.value
  }
}

// ==========================================
// RFB / VNC Live Connection Handling
// ==========================================
const disconnectRfb = () => {
  if (rfbInstance) {
    try {
      rfbInstance.disconnect()
    } catch {
      // Ignore disconnect errors
    }
    rfbInstance = null
  }
  if (vncContainerRef.value) {
    vncContainerRef.value.innerHTML = ''
  }
  connectionState.value = 'disconnected'
  isConnected.value = false
}

const connectRfb = async () => {
  if (typeof window === 'undefined') return
  disconnectRfb()

  connectionState.value = 'connecting'
  errorMessage.value = ''
  desktopName.value = ''

  await nextTick()
  const container = vncContainerRef.value
  if (!container) {
    connectionState.value = 'error'
    errorMessage.value = 'Target display container not ready.'
    return
  }

  const host = vncHost.value || window.location.hostname || '127.0.0.1'
  const port = vncPort.value || 8006
  const cleanPath = vncPath.value ? vncPath.value.replace(/^\/+/, '') : 'websockify'
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
  const wsUrl = `${protocol}//${host}:${port}/${cleanPath}`

  try {
    const rfb = await createRfbClient(container, wsUrl, {
      wsProtocols: ['binary']
    })

    rfb.scaleViewport = true
    rfb.resizeSession = false
    rfb.clipViewport = false
    rfb.viewOnly = isReadOnly.value || !canExecuteRemote.value
    rfb.focusOnClick = true

    rfb.addEventListener('connect', () => {
      connectionState.value = 'connected'
      isConnected.value = true
      errorMessage.value = ''
    })

    rfb.addEventListener('disconnect', (e: any) => {
      connectionState.value = 'disconnected'
      isConnected.value = false
      if (!e.detail.clean) {
        connectionState.value = 'error'
        errorMessage.value = 'VNC stream closed or unreachable. Verify Windows agent is running on port 8006.'
      }
    })

    rfb.addEventListener('securityfailure', (e: any) => {
      connectionState.value = 'error'
      isConnected.value = false
      errorMessage.value = `VNC security negotiation failed: ${e.detail.reason || 'Authentication required'}`
    })

    rfb.addEventListener('desktopname', (e: any) => {
      desktopName.value = e.detail.name || ''
    })

    rfbInstance = rfb
  } catch (err: any) {
    connectionState.value = 'error'
    isConnected.value = false
    errorMessage.value = err?.message || 'Failed to initialize @novnc/novnc RFB client.'
  }
}

const handleSendCtrlAltDel = () => {
  if (rfbInstance && connectionState.value === 'connected') {
    rfbInstance.sendCtrlAltDel()
  }
}

const handleToggleReadOnly = () => {
  isReadOnly.value = !isReadOnly.value
  if (rfbInstance) {
    rfbInstance.viewOnly = isReadOnly.value || !canExecuteRemote.value
  }
}

const handleSwitchToSimulation = () => {
  disconnectRfb()
  vncMode.value = 'simulation'
  startLoop()
}

const handleSwitchToLive = () => {
  stopLoop()
  vncMode.value = 'live'
  connectRfb()
}

// ==========================================
// Canvas simulation for VNC viewport (Fallback)
// ==========================================
const drawVncFrame = () => {
  const canvas = vncCanvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const width = canvas.width
  const height = canvas.height

  // Background
  ctx.fillStyle = '#0c0e12'
  ctx.fillRect(0, 0, width, height)

  // Subtle grid
  ctx.strokeStyle = '#232730'
  ctx.lineWidth = 1
  for (let x = 0; x < width; x += 40) {
    ctx.beginPath()
    ctx.moveTo(x, 0)
    ctx.lineTo(x, height)
    ctx.stroke()
  }
  for (let y = 0; y < height; y += 40) {
    ctx.beginPath()
    ctx.moveTo(0, y)
    ctx.lineTo(width, y)
    ctx.stroke()
  }

  // Windows-style / TwinCAT taskbar at bottom
  ctx.fillStyle = '#15181e'
  ctx.fillRect(0, height - 32, width, 32)
  ctx.strokeStyle = '#282d37'
  ctx.beginPath()
  ctx.moveTo(0, height - 32)
  ctx.lineTo(width, height - 32)
  ctx.stroke()

  // Start button
  ctx.fillStyle = '#445847'
  ctx.fillRect(4, height - 28, 60, 24)
  ctx.fillStyle = '#ffffff'
  ctx.font = '10px Inter, sans-serif'
  ctx.fillText('START', 18, height - 12)

  // Active HMI / TwinCAT Window
  const winX = 40
  const winY = 30
  const winW = width - 80
  const winH = height - 80

  ctx.fillStyle = '#1a1e24'
  ctx.fillRect(winX, winY, winW, winH)
  ctx.strokeStyle = '#383e44'
  ctx.strokeRect(winX, winY, winW, winH)

  // Window title bar
  ctx.fillStyle = '#282d37'
  ctx.fillRect(winX, winY, winW, 26)
  ctx.fillStyle = '#e2e8f0'
  ctx.font = 'bold 11px Inter, sans-serif'
  ctx.fillText(`Beckhoff TwinCAT PLC Runtime - ${props.controller?.name || 'IPC'} [RUN MODE]`, winX + 12, winY + 18)

  // Inner viewport contents
  ctx.fillStyle = '#0e1014'
  ctx.fillRect(winX + 8, winY + 34, winW - 16, winH - 42)

  // Process status lines
  ctx.fillStyle = '#768f79'
  ctx.font = '11px monospace'
  ctx.fillText(`Host: ${props.controller?.name} | IP: ${props.controller?.ipAddress} | OS: ${props.controller?.osVersion || 'Windows 10 IoT'}`, winX + 16, winY + 56)
  ctx.fillStyle = '#4ade80'
  ctx.fillText(`Target AMS NetId: 192.168.10.101.1.1 | State: RUN | Cycle: 1.000 ms`, winX + 16, winY + 76)
  ctx.fillStyle = '#828a94'
  ctx.fillText(`IO Link Master: Channel 01..16 Active | Real-Time Latency: ${latencyMs.value} ms`, winX + 16, winY + 96)

  // Memory & task gauge bar
  ctx.fillStyle = '#232730'
  ctx.fillRect(winX + 16, winY + 115, winW - 32, 14)
  const loadFill = Math.min((winW - 32) * 0.42, winW - 32)
  ctx.fillStyle = '#57715b'
  ctx.fillRect(winX + 16, winY + 115, loadFill, 14)

  ctx.fillStyle = '#f8fafc'
  ctx.font = '9px monospace'
  ctx.fillText('Task 1 (RealTime Cycle 1ms): 42.4% CPU Load', winX + 22, winY + 126)

  // Read-only watermark badge
  if (isReadOnly.value) {
    ctx.fillStyle = 'rgba(21, 24, 30, 0.85)'
    ctx.fillRect(width - 170, 10, 160, 24)
    ctx.strokeStyle = '#383e44'
    ctx.strokeRect(width - 170, 10, 160, 24)
    ctx.fillStyle = '#f59e0b'
    ctx.font = '10px Inter, sans-serif'
    ctx.fillText('🔒 READ-ONLY VIEWPORT', width - 158, 26)
  }

  // Clock
  const now = new Date().toLocaleTimeString()
  ctx.fillStyle = '#94a3b8'
  ctx.font = '10px monospace'
  ctx.fillText(now, width - 70, height - 12)
}

const startLoop = () => {
  if (typeof window === 'undefined') return
  stopLoop()
  const loop = () => {
    drawVncFrame()
    animationFrameId = requestAnimationFrame(loop)
  }
  animationFrameId = requestAnimationFrame(loop)
}

const stopLoop = () => {
  if (animationFrameId !== null) {
    cancelAnimationFrame(animationFrameId)
    animationFrameId = null
  }
}

// Lifecycle watcher
watch(() => props.open, (newVal) => {
  if (newVal) {
    initConnectionParams()
    if (activeProvider.value === 'vnc') {
      if (vncMode.value === 'live') {
        connectRfb()
      } else {
        setTimeout(startLoop, 100)
      }
    }
  } else {
    disconnectRfb()
    stopLoop()
  }
})

watch(() => activeProvider.value, (newVal) => {
  if (newVal === 'vnc' && props.open) {
    if (vncMode.value === 'live') {
      connectRfb()
    } else {
      setTimeout(startLoop, 100)
    }
  } else {
    disconnectRfb()
    stopLoop()
  }
})

const handleFullscreenChange = () => {
  if (typeof document !== 'undefined') {
    isFullscreen.value = !!document.fullscreenElement
  }
}

onMounted(() => {
  initConnectionParams()
  if (typeof document !== 'undefined') {
    document.addEventListener('fullscreenchange', handleFullscreenChange)
  }
  if (props.open && activeProvider.value === 'vnc') {
    if (vncMode.value === 'live') {
      connectRfb()
    } else {
      startLoop()
    }
  }
})

onUnmounted(() => {
  if (typeof document !== 'undefined') {
    document.removeEventListener('fullscreenchange', handleFullscreenChange)
  }
  disconnectRfb()
  stopLoop()
})
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent
      v-if="controller"
      :class="[
        isFullscreen ? 'fixed inset-0 w-screen h-screen max-w-none max-h-none rounded-none z-50 p-0 m-0 flex flex-col' : 'w-[95vw] sm:max-w-5xl lg:max-w-6xl max-h-[92vh] flex flex-col',
        'bg-slate-950 border-slate-800 text-slate-100 overflow-hidden rounded-2xl shadow-2xl transition-all'
      ]"
    >
      <!-- Dialog Header -->
      <div class="px-6 pt-5 pb-4 border-b border-slate-800 bg-slate-900/60 flex items-center justify-between flex-wrap gap-3">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Monitor class="w-5 h-5" />
          </div>
          <div>
            <DialogTitle class="text-base font-semibold text-slate-100 flex items-center gap-2">
              <span>{{ controller.name }}</span>
              <Badge variant="outline" class="text-[10px] font-mono border-slate-700 bg-slate-800/80 text-slate-300">
                {{ controller.ipAddress || vncHost }}
              </Badge>
              <Badge
                :class="controller.isOnline ? 'bg-emerald-950/60 text-emerald-400 border-emerald-500/30' : 'bg-rose-950/60 text-rose-400 border-rose-500/30'"
                class="text-[10px] border"
              >
                {{ controller.isOnline ? 'Online' : 'Offline' }}
              </Badge>
            </DialogTitle>
            <DialogDescription class="text-xs text-slate-400 mt-0.5">
              Remote Control & In-Browser Viewport (VNC / DameWare MRC / RDP)
            </DialogDescription>
          </div>
        </div>

        <!-- Provider Switcher Tabs -->
        <div class="flex items-center bg-slate-950 p-1 rounded-lg border border-slate-800 text-xs font-medium">
          <button
            type="button"
            @click="activeProvider = 'vnc'"
            :class="activeProvider === 'vnc' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1 rounded-md transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <Zap class="w-3 h-3" />
            <span>HTML5 VNC</span>
          </button>
          <button
            type="button"
            @click="activeProvider = 'dameware'"
            :class="activeProvider === 'dameware' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1 rounded-md transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <Radio class="w-3 h-3" />
            <span>DameWare MRC</span>
          </button>
          <button
            type="button"
            @click="activeProvider = 'rdp'"
            :class="activeProvider === 'rdp' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1 rounded-md transition-all flex items-center gap-1.5 cursor-pointer"
          >
            <Terminal class="w-3 h-3" />
            <span>RDP</span>
          </button>
        </div>
      </div>

      <!-- Unauthorized Alert when user lacks canExecuteRemote -->
      <div v-if="!canExecuteRemote" class="p-4 mx-6 mt-4 rounded-xl bg-amber-950/30 border border-amber-500/30 flex items-center gap-3">
        <Lock class="w-4 h-4 text-amber-400 shrink-0" />
        <div class="text-xs text-amber-200">
          <span class="font-semibold">Restricted Operational Privilege:</span> Remote session interaction requires
          <code class="px-1 py-0.5 bg-amber-950/60 rounded text-amber-300 font-mono">canExecuteRemote</code>
          (Technician or System Administrator role). Active sessions run in read-only audit mode.
        </div>
      </div>

      <!-- Main Body Container -->
      <div class="p-6 space-y-4">
        <!-- PROVIDER 1: HTML5 VNC VIEWPORT -->
        <template v-if="activeProvider === 'vnc'">
          <!-- Viewport Controls Toolbar -->
          <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-900 border border-slate-800 text-xs text-slate-300 flex-wrap gap-2">
            <div class="flex items-center gap-3 flex-wrap">
              <!-- Live State Indicator Badge -->
              <div v-if="vncMode === 'live'" class="flex items-center gap-2">
                <template v-if="connectionState === 'connected'">
                  <span class="size-2 rounded-full bg-emerald-400 animate-pulse"></span>
                  <span class="font-mono text-emerald-400 text-[11px] font-semibold">RFB 3.8 Connected</span>
                  <span v-if="desktopName" class="text-slate-400 text-[10px] font-mono">({{ desktopName }})</span>
                </template>
                <template v-else-if="connectionState === 'connecting'">
                  <span class="size-2 rounded-full bg-amber-400 animate-pulse"></span>
                  <span class="font-mono text-amber-300 text-[11px]">Connecting ws://{{ vncHost }}:{{ vncPort }}...</span>
                </template>
                <template v-else-if="connectionState === 'error'">
                  <span class="size-2 rounded-full bg-rose-500"></span>
                  <span class="font-mono text-rose-400 text-[11px]">VNC Connection Failed</span>
                </template>
                <template v-else>
                  <span class="size-2 rounded-full bg-slate-500"></span>
                  <span class="font-mono text-slate-400 text-[11px]">Disconnected</span>
                </template>
              </div>

              <!-- Simulation Mode Indicator Badge -->
              <div v-else class="flex items-center gap-1.5">
                <span class="size-2 rounded-full bg-blue-400"></span>
                <span class="font-mono text-blue-300 text-[11px]">TwinCAT Viewport Simulation</span>
              </div>

              <div class="h-3 w-px bg-slate-800 hidden sm:block"></div>

              <!-- Live / Simulation Mode Switcher -->
              <div class="flex items-center bg-slate-950 rounded p-0.5 border border-slate-800 text-[11px]">
                <button
                  type="button"
                  @click="handleSwitchToLive"
                  :class="vncMode === 'live' ? 'bg-indigo-600 text-white font-medium shadow-xs' : 'text-slate-400 hover:text-slate-200'"
                  class="px-2 py-0.5 rounded transition-all cursor-pointer"
                >
                  Live RFB
                </button>
                <button
                  type="button"
                  @click="handleSwitchToSimulation"
                  :class="vncMode === 'simulation' ? 'bg-indigo-600 text-white font-medium shadow-xs' : 'text-slate-400 hover:text-slate-200'"
                  class="px-2 py-0.5 rounded transition-all cursor-pointer"
                >
                  Simulation
                </button>
              </div>
            </div>

            <!-- Action Controls Group -->
            <div class="flex items-center gap-2">
              <!-- Send Ctrl+Alt+Del for Windows -->
              <button
                v-if="vncMode === 'live'"
                type="button"
                :disabled="connectionState !== 'connected' || !canExecuteRemote"
                @click="handleSendCtrlAltDel"
                class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 disabled:opacity-40 disabled:pointer-events-none text-slate-200 text-xs font-medium border border-slate-700 flex items-center gap-1 transition-all cursor-pointer"
                title="Send Ctrl+Alt+Del to Windows Node"
              >
                <Keyboard class="w-3 h-3 text-indigo-400" />
                <span class="hidden sm:inline">Ctrl+Alt+Del</span>
              </button>

              <!-- Read-only toggle -->
              <button
                type="button"
                @click="handleToggleReadOnly"
                :class="isReadOnly ? 'bg-amber-950/50 text-amber-300 border-amber-500/30' : 'bg-slate-800 text-slate-300 border-slate-700'"
                class="px-2.5 py-1 rounded border text-xs font-medium flex items-center gap-1 transition-all cursor-pointer"
                :title="isReadOnly ? 'Click to make session interactive' : 'Click to lock session to read-only'"
              >
                <Lock class="w-3 h-3" />
                <span>{{ isReadOnly ? 'Read-Only' : 'Interactive' }}</span>
              </button>

              <!-- Fullscreen Toggle (16:9 HD vs Windowed) -->
              <button
                type="button"
                @click="toggleFullscreen"
                :class="isFullscreen ? 'bg-indigo-600/40 text-indigo-300 border-indigo-500/40' : 'bg-slate-800 text-slate-300 hover:text-white border-slate-700'"
                class="p-1.5 rounded border transition-colors cursor-pointer"
                :title="isFullscreen ? 'Exit Fullscreen' : '16:9 HD Fullscreen Mode'"
              >
                <Minimize2 v-if="isFullscreen" class="w-3.5 h-3.5" />
                <Maximize2 v-else class="w-3.5 h-3.5" />
              </button>

              <!-- Connection Settings Toggle -->
              <button
                type="button"
                @click="showSettings = !showSettings"
                :class="showSettings ? 'bg-indigo-600/30 text-indigo-300 border-indigo-500/40' : 'bg-slate-800 text-slate-400 hover:text-slate-200 border-slate-700'"
                class="p-1.5 rounded border transition-colors cursor-pointer"
                title="Configure VNC Host & Port"
              >
                <Settings2 class="w-3.5 h-3.5" />
              </button>

              <!-- Reconnect Button -->
              <button
                type="button"
                @click="vncMode === 'live' ? connectRfb() : startLoop()"
                class="p-1.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-slate-200 border border-slate-700 transition-colors cursor-pointer"
                title="Reconnect / Refresh Stream"
              >
                <RefreshCw class="w-3.5 h-3.5" :class="connectionState === 'connecting' ? 'animate-spin' : ''" />
              </button>
            </div>
          </div>

          <!-- Mobile / Phone responsive touch helper bar -->
          <div class="sm:hidden px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 text-[11px] text-slate-400 flex items-center justify-between">
            <span class="flex items-center gap-1.5 text-indigo-300">
              <Smartphone class="w-3.5 h-3.5" />
              <span>Mobile Touch Optimized (Pinch to zoom / tap to click)</span>
            </span>
            <span class="font-mono text-[10px] text-slate-500">Auto-scale</span>
          </div>

          <!-- Connection Settings Dropdown / Panel -->
          <div v-if="showSettings" class="p-3 bg-slate-900/90 border border-slate-800 rounded-xl space-y-2 animate-in fade-in duration-200">
            <div class="flex items-center justify-between text-xs font-semibold text-slate-300 border-b border-slate-800 pb-2">
              <span class="flex items-center gap-1.5">
                <Sliders class="w-3.5 h-3.5 text-indigo-400" />
                VNC WebSocket Connection Parameters
              </span>
              <span class="text-[10px] text-slate-500">Windows Docker Container: port 8006, path /websockify</span>
            </div>
            <div class="grid grid-cols-1 sm:grid-cols-4 gap-3 pt-1">
              <div>
                <label class="block text-[10px] text-slate-400 font-mono mb-1">Host / IP</label>
                <input
                  v-model="vncHost"
                  type="text"
                  placeholder="127.0.0.1"
                  class="w-full bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200 font-mono focus:outline-none focus:border-indigo-500"
                />
              </div>
              <div>
                <label class="block text-[10px] text-slate-400 font-mono mb-1">Port</label>
                <input
                  v-model.number="vncPort"
                  type="number"
                  placeholder="8006"
                  class="w-full bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200 font-mono focus:outline-none focus:border-indigo-500"
                />
              </div>
              <div>
                <label class="block text-[10px] text-slate-400 font-mono mb-1">WebSocket Path</label>
                <input
                  v-model="vncPath"
                  type="text"
                  placeholder="websockify"
                  class="w-full bg-slate-950 border border-slate-700 rounded px-2 py-1 text-xs text-slate-200 font-mono focus:outline-none focus:border-indigo-500"
                />
              </div>
              <div class="flex items-end">
                <button
                  type="button"
                  @click="connectRfb(); showSettings = false"
                  class="w-full py-1 px-3 bg-indigo-600 hover:bg-indigo-500 text-white rounded text-xs font-semibold flex items-center justify-center gap-1 transition-all cursor-pointer"
                >
                  <RefreshCw class="w-3 h-3" />
                  <span>Apply & Connect</span>
                </button>
              </div>
            </div>
          </div>

          <!-- Main Viewport Canvas Container -->
          <div
            ref="viewportWrapperRef"
            :class="[
              isFullscreen
                ? 'fixed inset-0 z-50 w-screen h-screen bg-black rounded-none border-none flex items-center justify-center p-0 m-0'
                : 'relative rounded-xl overflow-hidden border border-slate-800 bg-black aspect-video flex items-center justify-center'
            ]"
          >
            <!-- Fullscreen Exit Floating Button -->
            <button
              v-if="isFullscreen"
              type="button"
              @click="toggleFullscreen"
              class="absolute top-4 right-4 z-50 px-3 py-1.5 rounded-lg bg-slate-900/90 hover:bg-slate-800 text-slate-200 border border-slate-700 backdrop-blur-xs transition-all cursor-pointer shadow-xl text-xs flex items-center gap-1.5"
              title="Exit Fullscreen (Esc)"
            >
              <Minimize2 class="w-3.5 h-3.5 text-indigo-400" />
              <span>Exit Fullscreen</span>
            </button>

            <!-- LIVE NO-VNC CONTAINER (RFB injects its canvas here) -->
            <div
              v-show="vncMode === 'live'"
              ref="vncContainerRef"
              class="w-full h-full flex items-center justify-center overflow-hidden [&_canvas]:max-w-full [&_canvas]:max-h-full [&_canvas]:object-contain [&_canvas]:cursor-crosshair"
            ></div>

            <!-- SIMULATION CANVAS FALLBACK -->
            <canvas
              v-show="vncMode === 'simulation'"
              ref="vncCanvasRef"
              width="800"
              height="450"
              class="w-full h-full object-contain cursor-crosshair select-none"
            ></canvas>

            <!-- Error Overlay with Action Buttons -->
            <div
              v-if="vncMode === 'live' && connectionState === 'error'"
              class="absolute inset-0 bg-slate-950/85 backdrop-blur-xs flex flex-col items-center justify-center p-6 text-center space-y-4 animate-in fade-in"
            >
              <div class="p-3 rounded-full bg-rose-950/50 border border-rose-500/30 text-rose-400">
                <AlertCircle class="w-8 h-8" />
              </div>
              <div class="space-y-1 max-w-md">
                <h4 class="text-sm font-semibold text-slate-200">Unable to Connect to Live VNC Stream</h4>
                <p class="text-xs text-slate-400">
                  {{ errorMessage || `Could not establish WebSocket connection to ws://${vncHost}:${vncPort}/${vncPath}` }}
                </p>
                <p class="text-[11px] text-slate-500 mt-2">
                  Ensure the Windows Docker container is running (<code class="text-slate-400 font-mono">docker compose up windows-agent</code>).
                </p>
              </div>
              <div class="flex items-center gap-3 pt-2">
                <button
                  type="button"
                  @click="connectRfb"
                  class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-lg flex items-center gap-1.5 transition-all shadow-md cursor-pointer"
                >
                  <RefreshCw class="w-3.5 h-3.5" />
                  <span>Retry Connection</span>
                </button>
                <button
                  type="button"
                  @click="handleSwitchToSimulation"
                  class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all cursor-pointer"
                >
                  <Play class="w-3.5 h-3.5 text-blue-400" />
                  <span>Switch to Simulated Viewport</span>
                </button>
              </div>
            </div>

            <!-- Connecting Overlay -->
            <div
              v-if="vncMode === 'live' && connectionState === 'connecting'"
              class="absolute inset-0 bg-black/70 flex flex-col items-center justify-center gap-3"
            >
              <div class="size-8 rounded-full border-2 border-indigo-500 border-t-transparent animate-spin"></div>
              <span class="text-xs font-mono text-slate-300">Connecting to RFB 3.8 WebSocket...</span>
            </div>
          </div>
        </template>

        <!-- PROVIDER 2: DAMEWARE MRC DEEP-LINK -->
        <template v-else-if="activeProvider === 'dameware'">
          <div class="p-6 rounded-xl bg-slate-900/80 border border-slate-800 space-y-5">
            <div class="flex items-start gap-4">
              <div class="p-3 rounded-xl bg-teal-950/40 border border-teal-600/30 text-teal-300 shrink-0">
                <Radio class="w-6 h-6" />
              </div>
              <div class="space-y-1">
                <h4 class="text-sm font-semibold text-slate-100">DameWare Mini Remote Control (MRC) Integration</h4>
                <p class="text-xs text-slate-400 leading-relaxed">
                  Launch an optimized, native DameWare MRC session using registered deep-link handler URI scheme (<code class="font-mono text-teal-300 text-[11px]">dwmrc://</code>).
                  Seamlessly bypasses browser sandbox limitations with zero-install native client bridging.
                </p>
              </div>
            </div>

            <!-- Endpoint Configuration Box -->
            <div class="p-4 rounded-lg bg-slate-950 border border-slate-800 space-y-3 font-mono text-xs">
              <div class="flex items-center justify-between text-slate-400">
                <span>DameWare URI Scheme:</span>
                <span class="text-emerald-400 font-sans text-[11px] flex items-center gap-1">
                  <span class="size-1.5 rounded-full bg-emerald-400"></span> DWRCS Service Listening (Port {{ damewarePort }})
                </span>
              </div>
              <div class="p-2.5 rounded bg-slate-900 border border-slate-800 text-teal-300 flex items-center justify-between overflow-x-auto gap-2">
                <span class="select-all truncate">{{ damewareUri }}</span>
                <button
                  type="button"
                  @click="handleCopyDamewareUri"
                  class="shrink-0 p-1.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white transition-colors flex items-center gap-1 text-[11px] cursor-pointer"
                >
                  <Check v-if="isCopied" class="w-3.5 h-3.5 text-emerald-400" />
                  <Copy v-else class="w-3.5 h-3.5" />
                  <span>{{ isCopied ? 'Copied' : 'Copy' }}</span>
                </button>
              </div>
            </div>

            <!-- Action buttons -->
            <div class="flex items-center gap-3 pt-2">
              <RbacButton
                :has-permission="canExecuteRemote"
                capability="canExecuteRemote"
                @click="handleLaunchDameware"
                class="bg-zinc-700 hover:bg-zinc-600 text-white text-xs font-semibold px-4 py-2 rounded-lg flex items-center gap-2 transition-all shadow-sm border border-zinc-600/50 cursor-pointer"
              >
                <ExternalLink class="w-3.5 h-3.5" />
                <span>Launch DameWare Client</span>
              </RbacButton>

              <button
                type="button"
                @click="handleCopyDamewareUri"
                class="bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium px-3.5 py-2 rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all cursor-pointer"
              >
                <Copy class="w-3.5 h-3.5" />
                <span>Copy Launch Link</span>
              </button>
            </div>
          </div>
        </template>

        <!-- PROVIDER 3: RDP CONNECTION -->
        <template v-else-if="activeProvider === 'rdp'">
          <div class="p-6 rounded-xl bg-slate-900/80 border border-slate-800 space-y-5">
            <div class="flex items-start gap-4">
              <div class="p-3 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 shrink-0">
                <Terminal class="w-6 h-6" />
              </div>
              <div class="space-y-1">
                <h4 class="text-sm font-semibold text-slate-100">Microsoft Remote Desktop Protocol (RDP)</h4>
                <p class="text-xs text-slate-400 leading-relaxed">
                  Download a pre-configured <code class="font-mono text-indigo-300 text-[11px]">.rdp</code> configuration file for high-performance Windows Terminal Services session with multi-monitor support.
                </p>
              </div>
            </div>

            <div class="p-4 rounded-lg bg-slate-950 border border-slate-800 space-y-2 font-mono text-xs">
              <div class="flex items-center justify-between text-slate-400">
                <span>RDP Host Address:</span>
                <span class="text-slate-200">{{ controller.ipAddress || vncHost }}:{{ rdpPort }}</span>
              </div>
              <div class="flex items-center justify-between text-slate-400">
                <span>Target Architecture:</span>
                <span class="text-slate-200">{{ controller.osVersion || 'Windows 10 Enterprise LTSC' }}</span>
              </div>
            </div>

            <div class="flex items-center gap-3 pt-2">
              <RbacButton
                :has-permission="canExecuteRemote"
                capability="canExecuteRemote"
                @click="handleDownloadRdp"
                class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold px-4 py-2 rounded-lg flex items-center gap-2 transition-all shadow-md cursor-pointer"
              >
                <ExternalLink class="w-3.5 h-3.5" />
                <span>Download .RDP Profile</span>
              </RbacButton>
            </div>
          </div>
        </template>
      </div>

      <!-- Dialog Footer -->
      <div class="px-6 py-3 border-t border-slate-800 bg-slate-900/40 flex items-center justify-between text-xs text-slate-400 flex-wrap gap-2">
        <div class="flex items-center gap-2">
          <ShieldCheck class="w-3.5 h-3.5 text-emerald-400" />
          <span>Sessions are cryptographically audited with operator token binding.</span>
        </div>
        <Button
          variant="outline"
          size="sm"
          @click="emit('update:open', false)"
          class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 hover:text-white text-xs cursor-pointer"
        >
          Close Session
        </Button>
      </div>
    </DialogContent>
  </Dialog>
</template>
