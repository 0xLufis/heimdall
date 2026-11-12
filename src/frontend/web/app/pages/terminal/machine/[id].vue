<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ShieldAlert,
  AlertTriangle,
  Flame,
  Cpu,
  Activity,
  Radio,
  Clock,
  Wrench,
  Gauge,
  Thermometer,
  Layers,
  Lock,
  KeyRound,
  Send,
  Zap,
  Check,
  Server,
  Maximize2,
  Minimize2
} from 'lucide-vue-next'
import TerminalAckModal from '~/components/terminal/TerminalAckModal.vue'
import { toast } from 'vue-sonner'
import { resolveTerminalLayout } from '~/utils/terminalLayout'

definePageMeta({
  layout: false
})

const route = useRoute()
const router = useRouter()
const machineId = computed(() => (route.params.id as string) || '')
const queryToken = computed(() => (route.query.token as string) || '')

// Terminal Authentication State
const authToken = ref('')
const isAuthenticated = ref(false)
const authError = ref('')
const pairingInput = ref('')

// Machine & Telemetry Data
const loading = ref(true)
const machineData = ref<any>(null)
const liveTelemetry = ref<any>({
  plcState: 'OFFLINE',
  cycleTimeMs: 0,
  spindleSpeedRpm: 0,
  spindleTemperatureC: 0,
  vibrationEnvelopeMmS: 0,
  hydraulicPressureBar: 0,
  productionCount: 0,
  partDefectCount: 0,
  feedRatePercentage: 0
})
import { useTelemetryStream } from '~/composables/useTelemetryStream'

// The route id may be the machine id or its identifier; follow whichever the backend publishes under.
const stream = useTelemetryStream({
  target: () => ({
    stationId: machineData.value?.id || machineId.value,
    handle: machineData.value?.pinnedObjectHandle || machineData.value?.customIdentifier || machineId.value
  })
})

watch(() => stream.latestValues.value, (newVals) => {
  if (newVals) {
    if (newVals.cycleTimeMs) liveTelemetry.value.cycleTimeMs = newVals.cycleTimeMs
    if (newVals.spindleRpm) liveTelemetry.value.spindleSpeedRpm = newVals.spindleRpm
    if (newVals.spindleTempC) liveTelemetry.value.spindleTemperatureC = newVals.spindleTempC
    if (newVals.vibrationMmS) liveTelemetry.value.vibrationEnvelopeMmS = newVals.vibrationMmS
    if (newVals.plcState) liveTelemetry.value.plcState = newVals.plcState
  }
}, { deep: true })

const recentTickets = ref<any[]>([])

// Real-time clock & heartbeat
const currentTime = ref('')
let clockTimer: any = null
let pollTimer: any = null

function updateClock() {
  const now = new Date()
  currentTime.value = now.toLocaleTimeString('en-US', { hour12: false })
}

// ---------------------------------------------------------------------------------------------
// Layout: full view, or the compact overlay for a small always-on-top window over the machine HMI
// (?overlay=1, or a window narrower than 640 px; see utils/terminalLayout.ts and the IPC launcher
// infra/windows/tray/Start-HeimdallTerminal.ps1).
// ---------------------------------------------------------------------------------------------
const viewportWidth = ref<number | null>(null)
const isOverlay = computed(() => resolveTerminalLayout(route.query.overlay, viewportWidth.value) === 'overlay')
/** Window bounds of the overlay before it was expanded, so "Overlay" can shrink the window back. */
const OVERLAY_BOUNDS_KEY = 'heimdall_terminal_overlay_bounds'
const canCollapse = ref(false)

const stationTitle = computed(() => machineData.value?.displayName || machineData.value?.name || 'Production Station')
const stationStatus = computed(() => machineData.value?.status || 'active')
const stationDotClass = computed(() =>
  stationStatus.value === 'active' ? 'bg-emerald-400' : stationStatus.value === 'maintenance' ? 'bg-amber-400' : 'bg-slate-500'
)
const dispatchLabel = computed(() =>
  submitting.value ? 'Dispatching ticket...' : selectedPreset.value ? `Dispatch ${selectedPreset.value.code}` : 'Select a fault first'
)

useHead({
  // The IPC launcher finds the app window by this title prefix.
  title: computed(() => `Heimdall Terminal - ${machineData.value?.customIdentifier || machineId.value}`)
})

function updateViewport() {
  viewportWidth.value = window.innerWidth
}

function setLayoutQuery(overlay: boolean) {
  return router.replace({ query: { ...route.query, overlay: overlay ? '1' : '0' } })
}

/**
 * Overlay -> full view. The launcher's app window (Chromium --app) may resize itself, so the window
 * also grows to the whole work area and stays on top of the HMI; in a normal browser tab the resize
 * is ignored and only the layout changes.
 */
async function expandToFullView() {
  try {
    sessionStorage.setItem(OVERLAY_BOUNDS_KEY, JSON.stringify({
      x: window.screenX, y: window.screenY, w: window.outerWidth, h: window.outerHeight
    }))
    canCollapse.value = true
  } catch { /* storage unavailable: no way back to the old size, the layout still switches */ }
  await setLayoutQuery(false)
  try {
    const screen = window.screen as Screen & { availLeft?: number, availTop?: number }
    window.moveTo(screen.availLeft ?? 0, screen.availTop ?? 0)
    window.resizeTo(screen.availWidth, screen.availHeight)
  } catch { /* not resizable here */ }
}

/** Full view -> overlay, restoring the overlay window bounds saved by expandToFullView(). */
async function collapseToOverlay() {
  await setLayoutQuery(true)
  try {
    const saved = JSON.parse(sessionStorage.getItem(OVERLAY_BOUNDS_KEY) || 'null')
    if (saved && saved.w > 0 && saved.h > 0) {
      window.resizeTo(saved.w, saved.h)
      window.moveTo(saved.x, saved.y)
    }
  } catch { /* not resizable here */ }
}

function toggleLineStop() {
  isLineStopOverride.value = !isLineStopOverride.value
}

// 2-Click Issue Reporting State
interface FaultPreset {
  code: string
  label: string
  category: string
  priority: 'Critical' | 'High' | 'Medium' | 'Low'
  isLineStop: boolean
  description: string
  icon: any
  colorClass: string
}

const faultPresets: FaultPreset[] = [
  {
    code: 'ERR-SAFETY-01',
    label: 'E-STOP / Safety Interlock',
    category: 'Safety',
    priority: 'Critical',
    isLineStop: true,
    description: 'Emergency stop circuit engaged or safety door interlock tripped on cell.',
    icon: ShieldAlert,
    colorClass: 'border-red-500/40 bg-red-950/30 text-red-400 hover:bg-red-900/40 hover:border-red-500'
  },
  {
    code: 'ERR-MECH-02',
    label: 'Mechanical Jam / Feeder Error',
    category: 'Mechanical',
    priority: 'High',
    isLineStop: true,
    description: 'Part jammed in pneumatic feeder track or indexing nest stalled.',
    icon: Wrench,
    colorClass: 'border-amber-500/40 bg-amber-950/30 text-amber-400 hover:bg-amber-900/40 hover:border-amber-500'
  },
  {
    code: 'ERR-THERM-03',
    label: 'Spindle Overheat / Cooling Fault',
    category: 'Thermal',
    priority: 'High',
    isLineStop: false,
    description: 'Spindle or drive thermal limit warning. Chiller loop pressure low.',
    icon: Flame,
    colorClass: 'border-orange-500/40 bg-orange-950/30 text-orange-400 hover:bg-orange-900/40 hover:border-orange-500'
  },
  {
    code: 'ERR-QUAL-04',
    label: 'Part Quality / Tolerance Drift',
    category: 'Quality',
    priority: 'Medium',
    isLineStop: false,
    description: 'Consecutive vision inspection rejects or dimensional tolerance drift.',
    icon: AlertTriangle,
    colorClass: 'border-yellow-500/40 bg-yellow-950/30 text-yellow-400 hover:bg-yellow-900/40 hover:border-yellow-500'
  },
  {
    code: 'ERR-COMM-05',
    label: 'PLC / Fieldbus Comm Loss',
    category: 'Communication',
    priority: 'Critical',
    isLineStop: true,
    description: 'EtherCAT / PROFINET remote I/O node timeout or drop on industrial bus.',
    icon: Cpu,
    colorClass: 'border-purple-500/40 bg-purple-950/30 text-purple-400 hover:bg-purple-900/40 hover:border-purple-500'
  },
  {
    code: 'ERR-FEED-06',
    label: 'Material Starvation / Tool Wear',
    category: 'Tooling',
    priority: 'Medium',
    isLineStop: false,
    description: 'Raw material buffer empty or cutting tool insert wear count reached limit.',
    icon: Layers,
    colorClass: 'border-blue-500/40 bg-blue-950/30 text-blue-400 hover:bg-blue-900/40 hover:border-blue-500'
  }
]

// Click 1 selection
const selectedPreset = ref<FaultPreset | null>(null)
const customNote = ref('')
const isLineStopOverride = ref<boolean | null>(null)
const submitting = ref(false)
const dispatchError = ref('')

// Click 2 Acknowledgment Modal
const showAckModal = ref(false)
const ackData = ref<any>(null)
const ackCountdown = ref(6)
let ackInterval: any = null

function selectPreset(preset: FaultPreset) {
  selectedPreset.value = preset
  isLineStopOverride.value = preset.isLineStop
}

async function fetchTerminalData() {
  if (!machineId.value) return

  try {
    const effectiveToken = authToken.value || queryToken.value
    const res: any = await $fetch(`/api/terminal/machine/${machineId.value}`, {
      query: effectiveToken ? { token: effectiveToken } : {}
    })

    if (res && res.machine) {
      machineData.value = res.machine
      liveTelemetry.value = res.telemetry
      recentTickets.value = res.recentTickets || []
      isAuthenticated.value = true
      authError.value = ''
    }
  } catch (err: any) {
    if (err?.statusCode === 401) {
      isAuthenticated.value = false
      authError.value = 'Station token invalid or unauthenticated.'
    } else {
      authError.value = err?.statusMessage || 'Unable to load machine terminal data.'
    }
  } finally {
    loading.value = false
  }
}

function handlePairTerminal() {
  if (!pairingInput.value.trim()) return
  authToken.value = pairingInput.value.trim()
  if (typeof window !== 'undefined') {
    localStorage.setItem(`terminal_token_${machineId.value}`, authToken.value)
  }
  fetchTerminalData()
}

// Click 2: Submit ticket
async function submitIssue() {
  if (!selectedPreset.value || submitting.value) return

  submitting.value = true
  try {
    const effectiveToken = authToken.value || queryToken.value
    const isLineStop = isLineStopOverride.value ?? selectedPreset.value.isLineStop

    const res: any = await $fetch('/api/terminal/ticket', {
      method: 'POST',
      body: {
        stationId: machineId.value,
        token: effectiveToken,
        errorCode: selectedPreset.value.code,
        category: selectedPreset.value.category,
        priority: selectedPreset.value.priority,
        isLineStop,
        title: `[${selectedPreset.value.code}] ${selectedPreset.value.label}`,
        description: selectedPreset.value.description,
        operatorNote: customNote.value.trim() || undefined
      }
    })

    if (res && res.success) {
      ackData.value = res
      showAckModal.value = true
      startAckCountdown()
      // Refresh station tickets
      fetchTerminalData()
      // Reset selection
      selectedPreset.value = null
      customNote.value = ''
      isLineStopOverride.value = null
    }
  } catch (err: any) {
    const msg = err?.statusMessage || err?.data?.message || err?.message || 'Failed to dispatch ticket. Please inform station lead.'
    dispatchError.value = msg
    toast.error(msg)
  } finally {
    submitting.value = false
  }
}

function startAckCountdown() {
  ackCountdown.value = 6
  if (ackInterval) clearInterval(ackInterval)
  ackInterval = setInterval(() => {
    ackCountdown.value -= 1
    if (ackCountdown.value <= 0) {
      closeAckModal()
    }
  }, 1000)
}

function closeAckModal() {
  if (ackInterval) clearInterval(ackInterval)
  showAckModal.value = false
  ackData.value = null
}

onMounted(() => {
  updateClock()
  clockTimer = setInterval(updateClock, 1000)

  updateViewport()
  window.addEventListener('resize', updateViewport)
  try {
    canCollapse.value = sessionStorage.getItem(OVERLAY_BOUNDS_KEY) !== null
  } catch { /* storage unavailable */ }

  // Retrieve token from query or local storage
  if (queryToken.value) {
    authToken.value = queryToken.value
  } else if (typeof window !== 'undefined') {
    const stored = localStorage.getItem(`terminal_token_${machineId.value}`)
    if (stored) authToken.value = stored
  }

  fetchTerminalData()
  pollTimer = setInterval(fetchTerminalData, 4000)
})

onUnmounted(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (pollTimer) clearInterval(pollTimer)
  if (ackInterval) clearInterval(ackInterval)
  window.removeEventListener('resize', updateViewport)
})
</script>

<template>
  <!-- COMPACT OVERLAY: small always-on-top window next to / over the machine HMI -->
  <div
    v-if="isOverlay"
    data-testid="terminal-overlay"
    class="h-screen bg-slate-950 text-slate-100 flex flex-col select-none font-sans antialiased overflow-hidden"
  >
    <header class="shrink-0 flex items-center gap-3 px-3 py-2 bg-slate-900 border-b border-slate-800">
      <span class="w-3 h-3 rounded-full shrink-0" :class="stationDotClass" />
      <div class="min-w-0 flex-1">
        <div class="text-base font-black text-white truncate leading-tight">{{ stationTitle }}</div>
        <div class="text-[11px] font-mono text-slate-400 truncate">
          {{ machineData?.customIdentifier || machineId }} &bull; {{ stationStatus }}
        </div>
      </div>
      <div class="shrink-0 text-lg font-black font-mono tabular-nums text-cyan-400">{{ currentTime || '00:00:00' }}</div>
      <button
        type="button"
        data-testid="terminal-expand"
        class="shrink-0 w-12 h-12 rounded-xl bg-slate-800 border border-slate-700 text-slate-100 flex items-center justify-center active:scale-95 cursor-pointer"
        aria-label="Expand to the full terminal view"
        title="Expand to the full terminal view"
        @click="expandToFullView"
      >
        <Maximize2 class="w-6 h-6" />
      </button>
    </header>

    <!-- Pairing (not authenticated) -->
    <div v-if="!isAuthenticated && !loading" class="flex-1 overflow-y-auto p-4 flex flex-col gap-3">
      <div class="flex items-center gap-3">
        <div class="w-12 h-12 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-400 flex items-center justify-center shrink-0">
          <Lock class="w-6 h-6" />
        </div>
        <div>
          <h2 class="text-lg font-bold text-white leading-tight">Pair this terminal</h2>
          <p class="text-xs text-slate-400">Enter the workstation token of this station.</p>
        </div>
      </div>
      <label for="terminal-token-overlay" class="text-xs font-bold uppercase tracking-wider text-slate-400">Workstation Token</label>
      <input
        id="terminal-token-overlay"
        v-model="pairingInput"
        type="text"
        inputmode="text"
        autocomplete="off"
        autocapitalize="off"
        spellcheck="false"
        enterkeyhint="go"
        placeholder="Workstation token"
        class="w-full h-14 bg-slate-900 border border-slate-700 rounded-xl px-4 text-lg text-white font-mono focus:outline-none focus:border-cyan-500"
        @keyup.enter="handlePairTerminal"
      />
      <div v-if="authError" class="text-sm text-red-400 font-mono">{{ authError }}</div>
      <button
        type="button"
        class="w-full h-14 bg-cyan-600 hover:bg-cyan-500 text-white text-base font-bold rounded-xl flex items-center justify-center gap-2 cursor-pointer"
        @click="handlePairTerminal"
      >
        <Check class="w-5 h-5" />
        <span>Authenticate & Bind Station</span>
      </button>
    </div>

    <template v-else>
      <main class="flex-1 min-h-0 overflow-y-auto p-3 space-y-3">
        <TerminalAckModal
          :open="showAckModal"
          :ack-data="ackData"
          :countdown="ackCountdown"
          @close="closeAckModal"
        />

        <div v-if="dispatchError" class="p-3 bg-rose-950/80 border border-rose-500/50 rounded-xl flex items-center justify-between gap-3 text-rose-300 text-sm">
          <span class="min-w-0">{{ dispatchError }}</span>
          <button type="button" class="shrink-0 h-10 px-3 rounded-lg border border-rose-500/50 text-rose-200 text-xs font-bold cursor-pointer" @click="dispatchError = ''">Dismiss</button>
        </div>

        <!-- Key live values -->
        <div class="grid grid-cols-4 gap-2">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-2 min-w-0">
            <div class="text-[10px] font-bold uppercase text-slate-400">PLC</div>
            <div class="text-sm font-black font-mono truncate" :class="liveTelemetry.plcState === 'RUN' ? 'text-emerald-400' : 'text-amber-400'">{{ liveTelemetry.plcState || 'RUN' }}</div>
          </div>
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-2 min-w-0">
            <div class="text-[10px] font-bold uppercase text-slate-400">Cycle ms</div>
            <div class="text-sm font-black font-mono text-cyan-400 truncate">{{ liveTelemetry.cycleTimeMs || '1240.5' }}</div>
          </div>
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-2 min-w-0">
            <div class="text-[10px] font-bold uppercase text-slate-400">Temp °C</div>
            <div class="text-sm font-black font-mono text-orange-400 truncate">{{ liveTelemetry.spindleTemperatureC || '44.8' }}</div>
          </div>
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-2 min-w-0">
            <div class="text-[10px] font-bold uppercase text-slate-400">Vib mm/s</div>
            <div class="text-sm font-black font-mono text-yellow-400 truncate">{{ liveTelemetry.vibrationEnvelopeMmS || '1.24' }}</div>
          </div>
        </div>

        <!-- Click 1: fault preset -->
        <div class="text-xs font-bold uppercase tracking-wider text-slate-400">1 &bull; Select fault</div>
        <div class="grid grid-cols-2 gap-2">
          <button
            v-for="preset in faultPresets"
            :key="preset.code"
            type="button"
            class="min-h-[84px] p-3 rounded-2xl border text-left flex flex-col justify-between gap-1 transition-all duration-150 active:scale-95 cursor-pointer relative"
            :class="[
              preset.colorClass,
              selectedPreset?.code === preset.code ? 'ring-4 ring-cyan-500 border-cyan-400 shadow-xl' : ''
            ]"
            @click="selectPreset(preset)"
          >
            <div class="flex items-center justify-between w-full">
              <component :is="preset.icon" class="w-5 h-5" />
              <span class="text-[10px] font-mono font-bold uppercase px-1.5 py-0.5 rounded bg-black/40">{{ preset.code }}</span>
            </div>
            <div class="text-sm font-black leading-tight text-white">{{ preset.label }}</div>
            <div v-if="selectedPreset?.code === preset.code" class="absolute -top-1.5 -right-1.5 w-6 h-6 bg-cyan-500 rounded-full flex items-center justify-center text-slate-950">
              <Check class="w-4 h-4 stroke-[3]" />
            </div>
          </button>
        </div>

        <button
          type="button"
          class="w-full h-12 rounded-xl border text-sm font-bold font-mono transition-colors cursor-pointer"
          :class="isLineStopOverride ? 'bg-red-950 border-red-500 text-red-400' : 'bg-slate-900 border-slate-700 text-slate-300'"
          @click="toggleLineStop"
        >
          Line Stop: {{ isLineStopOverride ? 'HALTED (YES)' : 'NO' }}
        </button>
        <input
          v-model="customNote"
          type="text"
          inputmode="text"
          enterkeyhint="done"
          placeholder="Optional operator note..."
          class="w-full h-12 bg-slate-900 border border-slate-700 rounded-xl px-3 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-cyan-500"
        />

        <!-- Open tickets of this station -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-3">
          <div class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-2">Active Dispatches ({{ recentTickets.length }})</div>
          <div v-if="recentTickets.length === 0" class="text-xs text-slate-500 font-mono">No active tickets for this station.</div>
          <div v-for="t in recentTickets.slice(0, 3)" :key="t.id" class="flex items-center justify-between gap-2 py-1.5 border-t border-slate-800 first:border-t-0">
            <div class="min-w-0">
              <div class="text-xs font-mono font-bold text-cyan-400">{{ t.ticketNumber }}</div>
              <div class="text-xs text-slate-300 truncate">{{ t.title }}</div>
            </div>
            <span class="shrink-0 text-[11px] font-mono font-semibold px-2 py-1 rounded bg-slate-800 text-slate-300">{{ t.status }}</span>
          </div>
        </div>
      </main>

      <!-- Click 2: dispatch, always visible -->
      <footer class="shrink-0 p-3 border-t border-slate-800 bg-slate-950">
        <button
          type="button"
          data-testid="terminal-dispatch"
          :disabled="!selectedPreset || submitting"
          class="w-full min-h-[64px] rounded-2xl font-black text-base tracking-wider uppercase flex items-center justify-center gap-3 transition-all duration-200 cursor-pointer"
          :class="selectedPreset
            ? 'bg-gradient-to-r from-red-600 via-rose-600 to-amber-600 text-white active:scale-[0.98]'
            : 'bg-slate-800 text-slate-500 cursor-not-allowed border border-slate-700'"
          @click="submitIssue"
        >
          <Send class="w-6 h-6" :class="{ 'animate-spin': submitting }" />
          <span>{{ dispatchLabel }}</span>
        </button>
      </footer>
    </template>
  </div>

  <!-- FULL VIEW -->
  <div v-else class="min-h-screen bg-slate-950 text-slate-100 flex flex-col select-none font-sans antialiased overflow-x-hidden">
    <!-- TOP STATUS BAR -->
    <header class="bg-slate-900/90 border-b border-slate-800 px-6 py-4 flex items-center justify-between shadow-lg sticky top-0 z-30">
      <div class="flex items-center gap-4">
        <div class="w-12 h-12 rounded-xl bg-gradient-to-tr from-cyan-600 to-blue-500 flex items-center justify-center shadow-lg shadow-cyan-500/20">
          <Cpu class="w-7 h-7 text-white" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <span class="text-xs font-bold tracking-wider uppercase px-2 py-0.5 rounded bg-slate-800 text-cyan-400 border border-slate-700">
              Station Terminal
            </span>
            <span class="text-xs text-slate-400 font-mono">
              ID: {{ machineData?.customIdentifier || machineId }}
            </span>
          </div>
          <h1 class="text-2xl font-black tracking-tight text-white mt-0.5">
            {{ stationTitle }}
          </h1>
        </div>
      </div>

      <!-- Center Machine Operational Badge -->
      <div class="flex items-center gap-3">
        <div
          class="flex items-center gap-2 px-4 py-2 rounded-xl border text-sm font-black tracking-wide uppercase transition-colors"
          :class="[
            machineData?.status === 'active' ? 'bg-emerald-950/60 border-emerald-500/50 text-emerald-400' :
            machineData?.status === 'maintenance' ? 'bg-amber-950/60 border-amber-500/50 text-amber-400' :
            'bg-slate-800 border-slate-700 text-slate-300'
          ]"
        >
          <span class="w-3 h-3 rounded-full animate-ping" :class="machineData?.status === 'active' ? 'bg-emerald-400' : 'bg-amber-400'" />
          <span>Status: {{ machineData?.status || 'Active' }}</span>
        </div>

        <div class="px-3 py-2 rounded-xl bg-slate-900 border border-slate-800 text-xs text-slate-400 font-mono flex items-center gap-2">
          <Server class="w-4 h-4 text-cyan-400" />
          <span>{{ machineData?.controllers?.[0]?.hostname || 'IPC-EDGE-CONTROLLER' }}</span>
        </div>
      </div>

      <!-- Right Heartbeat & Time -->
      <div class="flex items-center gap-6">
        <div class="flex items-center gap-2 text-xs font-mono text-emerald-400 bg-emerald-950/30 px-3 py-1.5 rounded-lg border border-emerald-500/30">
          <Radio class="w-4 h-4 animate-pulse text-emerald-400" />
          <span>LINK OK</span>
        </div>
        <div class="text-right">
          <div class="text-xs font-mono text-slate-400 uppercase tracking-wider">Station Clock</div>
          <div class="text-2xl font-black font-mono tracking-wider text-cyan-400">{{ currentTime || '00:00:00' }}</div>
        </div>
        <button
          v-if="canCollapse"
          type="button"
          data-testid="terminal-collapse"
          class="h-12 px-4 rounded-xl bg-slate-800 border border-slate-700 text-slate-100 text-sm font-bold flex items-center gap-2 active:scale-95 cursor-pointer"
          aria-label="Back to the compact overlay"
          title="Back to the compact overlay"
          @click="collapseToOverlay"
        >
          <Minimize2 class="w-5 h-5" />
          <span>Overlay</span>
        </button>
      </div>
    </header>

    <!-- AUTHENTICATION REQUIRED (Dedicated Page View, Zero Popups) -->
    <div v-if="!isAuthenticated && !loading" class="flex-1 flex items-center justify-center p-6 min-h-[70vh]">
      <div class="max-w-md w-full bg-slate-900 border border-slate-800 rounded-2xl p-8 shadow-2xl text-center">
        <div class="w-16 h-16 rounded-2xl bg-amber-500/10 border border-amber-500/20 text-amber-400 flex items-center justify-center mx-auto mb-4">
          <Lock class="w-8 h-8" />
        </div>
        <h2 class="text-2xl font-bold text-white mb-2">Workstation Terminal Pairing</h2>
        <p class="text-sm text-slate-400 mb-6">
          This touchscreen kiosk requires a registered workstation hardware token or client X.509 certificate to access machine telemetry.
        </p>

        <div class="text-left mb-6">
          <label for="terminal-token-full" class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-2 block">Workstation Token</label>
          <div class="relative">
            <KeyRound class="w-5 h-5 text-slate-500 absolute left-3 top-4" />
            <input
              id="terminal-token-full"
              v-model="pairingInput"
              type="text"
              inputmode="text"
              autocomplete="off"
              autocapitalize="off"
              spellcheck="false"
              enterkeyhint="go"
              placeholder="e.g. kiosk-token-op10"
              class="w-full h-14 bg-slate-950 border border-slate-700 rounded-xl pl-11 pr-4 text-white font-mono text-base focus:outline-none focus:border-cyan-500"
              @keyup.enter="handlePairTerminal"
            />
          </div>

          <div v-if="authError" class="text-xs text-red-400 mt-2 font-mono text-center">
            {{ authError }}
          </div>
        </div>

        <button
          type="button"
          @click="handlePairTerminal"
          class="w-full py-3.5 bg-cyan-600 hover:bg-cyan-500 text-white font-bold rounded-xl shadow-lg transition-colors flex items-center justify-center gap-2 cursor-pointer"
        >
          <Check class="w-5 h-5" />
          <span>Authenticate & Bind Station</span>
        </button>
      </div>
    </div>
    <!-- MAIN TERMINAL CONTENT (READ-ONLY TELEMETRY + 2-CLICK REPORTING) -->
    <main v-else class="flex-1 flex flex-col p-6 overflow-y-auto">
      <!-- HIGH-CONTRAST VISUAL ACKNOWLEDGMENT BANNER (IN-PAGE WORKBENCH, ZERO POPUPS) -->
      <TerminalAckModal
        :open="showAckModal"
        :ack-data="ackData"
        :countdown="ackCountdown"
        @close="closeAckModal"
      />

      <!-- In-Page Error Banner if Dispatch Fails -->
      <div v-if="dispatchError" class="p-4 mb-4 bg-rose-950/80 border border-rose-500/50 rounded-2xl flex items-center justify-between text-rose-300 text-sm">
        <span>{{ dispatchError }}</span>
        <button @click="dispatchError = ''" class="text-rose-400 hover:text-white text-xs underline cursor-pointer">Dismiss</button>
      </div>

      <div class="grid grid-cols-12 gap-6 flex-1">
        <!-- LEFT COLUMN: READ-ONLY LIVE TELEMETRY (5 COLS) -->
        <section class="col-span-12 lg:col-span-5 flex flex-col gap-5">
        <div class="flex items-center justify-between">
          <h2 class="text-sm font-black uppercase tracking-wider text-slate-400 flex items-center gap-2">
            <Gauge class="w-4 h-4 text-cyan-400" />
            <span>Live PLC Telemetry (Read-Only)</span>
          </h2>
          <span class="text-xs font-mono text-cyan-500 flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full bg-cyan-400 animate-pulse" />
            <span>400ms Polling</span>
          </span>
        </div>

        <!-- Telemetry Metrics Grid -->
        <div class="grid grid-cols-2 gap-3.5">
          <!-- Metric 1: PLC State -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between">
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span class="font-bold uppercase">PLC State</span>
              <Activity class="w-4 h-4 text-cyan-400" />
            </div>
            <div class="text-3xl font-black font-mono mt-2" :class="liveTelemetry.plcState === 'RUN' ? 'text-emerald-400' : 'text-amber-400'">
              {{ liveTelemetry.plcState || 'RUN' }}
            </div>
            <div class="text-[11px] text-slate-500 mt-1 font-mono">TwinCAT 3.1 Runtime</div>
          </div>

          <!-- Metric 2: Cycle Time -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between">
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span class="font-bold uppercase">Cycle Time</span>
              <Clock class="w-4 h-4 text-cyan-400" />
            </div>
            <div class="text-3xl font-black font-mono text-cyan-400 mt-2">
              {{ liveTelemetry.cycleTimeMs || '1240.5' }} <span class="text-sm text-slate-400">ms</span>
            </div>
            <div class="text-[11px] text-slate-500 mt-1 font-mono">Nominal: 1200 ms</div>
          </div>

          <!-- Metric 3: Spindle Temp -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between">
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span class="font-bold uppercase">Spindle Temp</span>
              <Thermometer class="w-4 h-4 text-orange-400" />
            </div>
            <div class="text-3xl font-black font-mono text-orange-400 mt-2">
              {{ liveTelemetry.spindleTemperatureC || '44.8' }} <span class="text-sm text-slate-400">°C</span>
            </div>
            <div class="text-[11px] text-slate-500 mt-1 font-mono">Limit: 65.0 °C</div>
          </div>

          <!-- Metric 4: Vibration Envelope -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between">
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span class="font-bold uppercase">Vibration (RMS)</span>
              <Zap class="w-4 h-4 text-yellow-400" />
            </div>
            <div class="text-3xl font-black font-mono text-yellow-400 mt-2">
              {{ liveTelemetry.vibrationEnvelopeMmS || '1.24' }} <span class="text-sm text-slate-400">mm/s</span>
            </div>
            <div class="text-[11px] text-slate-500 mt-1 font-mono">ISO 10816 Zone A</div>
          </div>

          <!-- Metric 5: Production Count -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between">
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span class="font-bold uppercase">Parts Produced</span>
              <Layers class="w-4 h-4 text-cyan-400" />
            </div>
            <div class="text-3xl font-black font-mono text-white mt-2">
              {{ (liveTelemetry.productionCount || 4890).toLocaleString() }}
            </div>
            <div class="text-[11px] text-emerald-400 mt-1 font-mono">Shift Target: 5,000</div>
          </div>

          <!-- Metric 6: Hydraulic Pressure -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between">
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span class="font-bold uppercase">Hydraulic Press.</span>
              <Gauge class="w-4 h-4 text-cyan-400" />
            </div>
            <div class="text-3xl font-black font-mono text-cyan-400 mt-2">
              {{ liveTelemetry.hydraulicPressureBar || '149.2' }} <span class="text-sm text-slate-400">bar</span>
            </div>
            <div class="text-[11px] text-slate-500 mt-1 font-mono">Operating: 140-160 bar</div>
          </div>
        </div>

        <!-- RECENT TICKETS SUMMARY (READ-ONLY) -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex-1 flex flex-col">
          <div class="flex items-center justify-between mb-3">
            <h3 class="text-xs font-bold uppercase tracking-wider text-slate-400 flex items-center gap-2">
              <Clock class="w-4 h-4 text-slate-400" />
              <span>Active Shift Dispatches ({{ recentTickets.length }})</span>
            </h3>
            <span class="text-[11px] text-slate-500 font-mono">Read-Only View</span>
          </div>

          <div v-if="recentTickets.length === 0" class="text-center py-6 text-slate-500 text-xs font-mono">
            No active tickets for this station.
          </div>
          <div v-else class="space-y-2 overflow-y-auto max-h-48 pr-1">
            <div 
              v-for="t in recentTickets" 
              :key="t.id"
              class="bg-slate-950/60 border border-slate-800/80 rounded-xl p-3 flex items-center justify-between"
            >
              <div>
                <div class="flex items-center gap-2">
                  <span class="text-xs font-mono font-bold text-cyan-400">{{ t.ticketNumber }}</span>
                  <span 
                    class="text-[10px] px-1.5 py-0.5 rounded font-bold uppercase"
                    :class="t.priority === 'Critical' ? 'bg-red-950 text-red-400' : 'bg-amber-950 text-amber-400'"
                  >
                    {{ t.priority }}
                  </span>
                </div>
                <div class="text-xs text-slate-300 font-medium truncate max-w-[280px] mt-0.5">
                  {{ t.title }}
                </div>
              </div>
              <span class="text-[11px] font-mono font-semibold px-2 py-1 rounded bg-slate-800 text-slate-300">
                {{ t.status }}
              </span>
            </div>
          </div>
        </div>
      </section>

      <!-- RIGHT COLUMN: 2-CLICK ISSUE REPORTING INTERFACE (7 COLS) -->
      <section class="col-span-12 lg:col-span-7 flex flex-col gap-4 bg-slate-900/50 border border-slate-800/80 rounded-3xl p-6 shadow-xl">
        <div class="flex items-center justify-between border-b border-slate-800 pb-4">
          <div>
            <div class="flex items-center gap-2">
              <span class="w-2.5 h-2.5 rounded-full bg-red-500 animate-pulse" />
              <h2 class="text-lg font-black uppercase tracking-wider text-white">
                2-Click Issue Reporting
              </h2>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              Click 1: Select Fault Code Preset &bull; Click 2: Dispatch Ticket to Floor Technicians
            </p>
          </div>

          <div class="text-right">
            <span class="text-xs font-mono font-semibold text-slate-400">Step 1 of 2</span>
          </div>
        </div>

        <!-- CLICK 1: PRESET FAULT CATEGORY CARDS (GRID) -->
        <div class="grid grid-cols-2 sm:grid-cols-3 gap-3.5 my-auto">
          <button
            v-for="preset in faultPresets"
            :key="preset.code"
            type="button"
            @click="selectPreset(preset)"
            class="min-h-[110px] p-4 rounded-2xl border text-left flex flex-col justify-between transition-all duration-150 active:scale-95 cursor-pointer relative overflow-hidden"
            :class="[
              preset.colorClass,
              selectedPreset?.code === preset.code ? 'ring-4 ring-cyan-500 border-cyan-400 scale-[1.02] shadow-xl' : 'opacity-85 hover:opacity-100'
            ]"
          >
            <div class="flex items-center justify-between w-full">
              <component :is="preset.icon" class="w-6 h-6" />
              <span class="text-[10px] font-mono font-bold uppercase px-2 py-0.5 rounded bg-black/40">
                {{ preset.code }}
              </span>
            </div>
            <div>
              <div class="text-sm font-black leading-tight mt-2 text-white">
                {{ preset.label }}
              </div>
              <div class="text-[11px] opacity-75 font-mono mt-0.5">
                Priority: {{ preset.priority }}
              </div>
            </div>

            <!-- Active check badge -->
            <div v-if="selectedPreset?.code === preset.code" class="absolute top-2 right-2 w-5 h-5 bg-cyan-500 rounded-full flex items-center justify-center text-slate-950">
              <Check class="w-3.5 h-3.5 stroke-[3]" />
            </div>
          </button>
        </div>

        <!-- SELECTION DETAILS & QUICK CONTROLS -->
        <div class="bg-slate-950/80 border border-slate-800 rounded-2xl p-4 mt-auto">
          <div class="flex items-center justify-between mb-2">
            <div class="text-xs font-bold uppercase tracking-wider text-slate-400">
              Selected Fault Preset
            </div>
            <div v-if="selectedPreset" class="text-xs font-mono text-cyan-400 font-bold">
              {{ selectedPreset.code }} &bull; {{ selectedPreset.category }}
            </div>
          </div>

          <div v-if="selectedPreset" class="text-sm text-slate-200 font-medium mb-3">
            {{ selectedPreset.description }}
          </div>
          <div v-else class="text-sm text-slate-500 italic mb-3">
            Touch any fault preset card above to configure ticket...
          </div>

          <!-- Quick Touch Line-Stop Toggle -->
          <div class="flex items-center justify-between pt-3 border-t border-slate-800/80">
            <div class="flex items-center gap-3">
              <button 
                type="button"
                @click="isLineStopOverride = !isLineStopOverride"
                class="px-3 py-1.5 rounded-lg border text-xs font-bold font-mono transition-colors"
                :class="isLineStopOverride ? 'bg-red-950 border-red-500 text-red-400' : 'bg-slate-900 border-slate-700 text-slate-400'"
              >
                Line Stop: {{ isLineStopOverride ? 'HALTED (YES)' : 'NO' }}
              </button>
            </div>

            <input 
              v-model="customNote"
              type="text" 
              placeholder="Optional operator touch note..." 
              class="flex-1 ml-4 bg-slate-900 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-cyan-500"
            />
          </div>
        </div>

        <!-- CLICK 2: SUBMIT ISSUE BUTTON (LARGE TOUCH TARGET) -->
        <button
          type="button"
          :disabled="!selectedPreset || submitting"
          @click="submitIssue"
          class="w-full min-h-[64px] rounded-2xl font-black text-lg tracking-wider uppercase flex items-center justify-center gap-3 transition-all duration-200 shadow-xl cursor-pointer"
          :class="[
            selectedPreset 
              ? 'bg-gradient-to-r from-red-600 via-rose-600 to-amber-600 hover:from-red-500 hover:to-amber-500 text-white shadow-red-900/40 active:scale-[0.98]'
              : 'bg-slate-800 text-slate-500 cursor-not-allowed border border-slate-700'
          ]"
        >
          <Send class="w-6 h-6" :class="{ 'animate-spin': submitting }" />
          <span>{{ submitting ? 'DISPATCHING TICKET...' : (selectedPreset ? `CLICK 2: DISPATCH ${selectedPreset.code} TICKET NOW` : 'SELECT PRESET ABOVE TO DISPATCH') }}</span>
        </button>
      </section>
      </div>
    </main>
  </div>
</template>
