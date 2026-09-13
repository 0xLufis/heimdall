<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import type { IndustrialController } from '~/types/domain'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import RbacButton from '~/components/common/RbacButton.vue'
import RbacTooltip from '~/components/common/RbacTooltip.vue'
import { useRbacPermission, RBAC_TOOLTIPS } from '~/composables/useRbacPermission'
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
  Power
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
const isConnected = ref(true)
const isReadOnly = ref(true)
const canvasResolution = ref<'1024x768' | '1280x1024' | '1920x1080'>('1024x768')
const damewarePort = ref(6129)
const rdpPort = ref(3389)
const latencyMs = ref(18)
const fps = ref(30)
const vncCanvasRef = ref<HTMLCanvasElement | null>(null)
let animationFrameId: number | null = null

// Deep-link URIs
const damewareUri = computed(() => {
  const ip = props.controller?.ipAddress || '127.0.0.1'
  return `dwmrc://${ip}?port=${damewarePort.value}&use_cur_creds=1`
})

const rdpDownloadContent = computed(() => {
  const ip = props.controller?.ipAddress || '127.0.0.1'
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

// Canvas simulation for VNC viewport
const drawVncFrame = () => {
  const canvas = vncCanvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const width = canvas.width
  const height = canvas.height

  // Background
  ctx.fillStyle = '#090d16'
  ctx.fillRect(0, 0, width, height)

  // Subtle grid
  ctx.strokeStyle = '#1e293b'
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
  ctx.fillStyle = '#0f172a'
  ctx.fillRect(0, height - 32, width, 32)
  ctx.strokeStyle = '#334155'
  ctx.beginPath()
  ctx.moveTo(0, height - 32)
  ctx.lineTo(width, height - 32)
  ctx.stroke()

  // Start button
  ctx.fillStyle = '#4f46e5'
  ctx.fillRect(4, height - 28, 60, 24)
  ctx.fillStyle = '#ffffff'
  ctx.font = '10px Inter, sans-serif'
  ctx.fillText('START', 18, height - 12)

  // Active HMI / TwinCAT Window
  const winX = 40
  const winY = 30
  const winW = width - 80
  const winH = height - 80

  ctx.fillStyle = '#1e293b'
  ctx.fillRect(winX, winY, winW, winH)
  ctx.strokeStyle = '#475569'
  ctx.strokeRect(winX, winY, winW, winH)

  // Window title bar
  ctx.fillStyle = '#334155'
  ctx.fillRect(winX, winY, winW, 26)
  ctx.fillStyle = '#e2e8f0'
  ctx.font = 'bold 11px Inter, sans-serif'
  ctx.fillText(`Beckhoff TwinCAT PLC Runtime - ${props.controller?.name || 'IPC'} [RUN MODE]`, winX + 12, winY + 18)

  // Inner viewport contents
  ctx.fillStyle = '#020617'
  ctx.fillRect(winX + 8, winY + 34, winW - 16, winH - 42)

  // Process status lines
  ctx.fillStyle = '#38bdf8'
  ctx.font = '11px monospace'
  ctx.fillText(`Host: ${props.controller?.name} | IP: ${props.controller?.ipAddress} | OS: ${props.controller?.osVersion || 'Windows 10 IoT'}`, winX + 16, winY + 56)
  ctx.fillStyle = '#4ade80'
  ctx.fillText(`Target AMS NetId: 192.168.10.101.1.1 | State: RUN | Cycle: 1.000 ms`, winX + 16, winY + 76)
  ctx.fillStyle = '#94a3b8'
  ctx.fillText(`IO Link Master: Channel 01..16 Active | Real-Time Latency: ${latencyMs.value} ms`, winX + 16, winY + 96)

  // Memory & task gauge bar
  ctx.fillStyle = '#1e293b'
  ctx.fillRect(winX + 16, winY + 115, winW - 32, 14)
  const loadFill = Math.min((winW - 32) * 0.42, winW - 32)
  ctx.fillStyle = '#6366f1'
  ctx.fillRect(winX + 16, winY + 115, loadFill, 14)

  ctx.fillStyle = '#f8fafc'
  ctx.font = '9px monospace'
  ctx.fillText('Task 1 (RealTime Cycle 1ms): 42.4% CPU Load', winX + 22, winY + 126)

  // Read-only watermark badge
  if (isReadOnly.value) {
    ctx.fillStyle = 'rgba(15, 23, 42, 0.75)'
    ctx.fillRect(width - 170, 10, 160, 24)
    ctx.strokeStyle = '#475569'
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

watch(() => props.open, (newVal) => {
  if (newVal) {
    setTimeout(() => {
      startLoop()
    }, 100)
  } else {
    stopLoop()
  }
})

onMounted(() => {
  if (props.open) {
    startLoop()
  }
})

onUnmounted(() => {
  stopLoop()
})
</script>

<template>
  <Dialog :open="open" @update:open="emit('update:open', $event)">
    <DialogContent
      v-if="controller"
      class="max-w-4xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-2xl shadow-2xl"
    >
      <!-- Dialog Header -->
      <div class="px-6 pt-5 pb-4 border-b border-slate-800 bg-slate-900/60 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-lg bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Monitor class="w-5 h-5" />
          </div>
          <div>
            <DialogTitle class="text-base font-semibold text-slate-100 flex items-center gap-2">
              <span>{{ controller.name }}</span>
              <Badge variant="outline" class="text-[10px] font-mono border-slate-700 bg-slate-800/80 text-slate-300">
                {{ controller.ipAddress }}
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
            class="px-3 py-1 rounded-md transition-all flex items-center gap-1.5"
          >
            <Zap class="w-3 h-3" />
            <span>HTML5 VNC</span>
          </button>
          <button
            type="button"
            @click="activeProvider = 'dameware'"
            :class="activeProvider === 'dameware' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1 rounded-md transition-all flex items-center gap-1.5"
          >
            <Radio class="w-3 h-3" />
            <span>DameWare MRC</span>
          </button>
          <button
            type="button"
            @click="activeProvider = 'rdp'"
            :class="activeProvider === 'rdp' ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-slate-200'"
            class="px-3 py-1 rounded-md transition-all flex items-center gap-1.5"
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
          <div class="flex items-center justify-between p-2.5 rounded-lg bg-slate-900 border border-slate-800 text-xs text-slate-300">
            <div class="flex items-center gap-4">
              <div class="flex items-center gap-1.5">
                <span class="size-2 rounded-full bg-emerald-400 animate-pulse"></span>
                <span class="font-mono text-emerald-400 text-[11px]">RFB 3.8 WebSocket</span>
              </div>
              <div class="flex items-center gap-1 text-slate-400 font-mono text-[11px]">
                <span>Latency:</span>
                <span class="text-slate-200">{{ latencyMs }} ms</span>
              </div>
              <div class="flex items-center gap-1 text-slate-400 font-mono text-[11px]">
                <span>Rate:</span>
                <span class="text-slate-200">{{ fps }} FPS</span>
              </div>
            </div>

            <div class="flex items-center gap-2">
              <!-- Read-only toggle -->
              <button
                type="button"
                @click="isReadOnly = !isReadOnly"
                :class="isReadOnly ? 'bg-amber-950/50 text-amber-300 border-amber-500/30' : 'bg-slate-800 text-slate-300 border-slate-700'"
                class="px-2.5 py-1 rounded border text-xs font-medium flex items-center gap-1 transition-all"
              >
                <Lock class="w-3 h-3" />
                <span>{{ isReadOnly ? 'Read-Only' : 'Interactive' }}</span>
              </button>

              <button
                type="button"
                @click="latencyMs = Math.floor(14 + Math.random() * 8)"
                class="p-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-slate-200 transition-colors"
                title="Reconnect / Refresh Stream"
              >
                <RefreshCw class="w-3.5 h-3.5" />
              </button>
            </div>
          </div>

          <!-- Interactive HTML5 VNC Canvas Container -->
          <div class="relative rounded-xl overflow-hidden border border-slate-800 bg-black aspect-video flex items-center justify-center">
            <canvas
              ref="vncCanvasRef"
              width="800"
              height="450"
              class="w-full h-full object-contain cursor-crosshair select-none"
            ></canvas>
          </div>
        </template>

        <!-- PROVIDER 2: DAMEWARE MRC DEEP-LINK -->
        <template v-else-if="activeProvider === 'dameware'">
          <div class="p-6 rounded-xl bg-slate-900/80 border border-slate-800 space-y-5">
            <div class="flex items-start gap-4">
              <div class="p-3 rounded-xl bg-sky-500/10 border border-sky-500/20 text-sky-400 shrink-0">
                <Radio class="w-6 h-6" />
              </div>
              <div class="space-y-1">
                <h4 class="text-sm font-semibold text-slate-100">DameWare Mini Remote Control (MRC) Integration</h4>
                <p class="text-xs text-slate-400 leading-relaxed">
                  Launch an optimized, native DameWare MRC session using registered deep-link handler URI scheme (<code class="font-mono text-sky-300 text-[11px]">dwmrc://</code>).
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
              <div class="p-2.5 rounded bg-slate-900 border border-slate-800 text-sky-300 flex items-center justify-between overflow-x-auto gap-2">
                <span class="select-all truncate">{{ damewareUri }}</span>
                <button
                  type="button"
                  @click="handleCopyDamewareUri"
                  class="shrink-0 p-1.5 rounded bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white transition-colors flex items-center gap-1 text-[11px]"
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
                class="bg-sky-600 hover:bg-sky-500 text-white text-xs font-semibold px-4 py-2 rounded-lg flex items-center gap-2 transition-all shadow-md"
              >
                <ExternalLink class="w-3.5 h-3.5" />
                <span>Launch DameWare Client</span>
              </RbacButton>

              <button
                type="button"
                @click="handleCopyDamewareUri"
                class="bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium px-3.5 py-2 rounded-lg border border-slate-700 flex items-center gap-1.5 transition-all"
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
                <span class="text-slate-200">{{ controller.ipAddress }}:{{ rdpPort }}</span>
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
                class="bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold px-4 py-2 rounded-lg flex items-center gap-2 transition-all shadow-md"
              >
                <ExternalLink class="w-3.5 h-3.5" />
                <span>Download .RDP Profile</span>
              </RbacButton>
            </div>
          </div>
        </template>
      </div>

      <!-- Dialog Footer -->
      <div class="px-6 py-3 border-t border-slate-800 bg-slate-900/40 flex items-center justify-between text-xs text-slate-400">
        <div class="flex items-center gap-2">
          <ShieldCheck class="w-3.5 h-3.5 text-emerald-400" />
          <span>Sessions are cryptographically audited with operator token binding.</span>
        </div>
        <Button
          variant="outline"
          size="sm"
          @click="emit('update:open', false)"
          class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 hover:text-white text-xs"
        >
          Close Session
        </Button>
      </div>
    </DialogContent>
  </Dialog>
</template>
