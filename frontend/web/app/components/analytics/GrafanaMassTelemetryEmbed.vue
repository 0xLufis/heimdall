<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { Card, CardHeader, CardTitle, CardContent } from '~/components/ui/card'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import {
  ExternalLink,
  Copy,
  Check,
  Maximize2,
  RefreshCw,
  Layers,
  Activity,
  Cpu,
  Server,
  Terminal,
  Sliders,
  ShieldCheck,
  TrendingUp,
  Flame,
  Radio,
  Play,
  Sparkles,
  AlertTriangle,
  Zap,
  Clock,
  Gauge,
  Info
} from 'lucide-vue-next'

interface GrafanaDashboardPreset {
  id: string
  title: string
  description: string
  icon: any
  path: string
  tag: string
}

const viewMode = ref<'demo' | 'iframe'>('demo')
const grafanaBaseUrl = ref('http://localhost:3001')
const kioskMode = ref(true)
const autoRefresh = ref('10s')
const demoTimeRange = ref<'5m' | '15m' | '1h' | '24h'>('15m')
const selectedPresetId = ref('vibration-fft')
const iframeKey = ref(0)
const copiedField = ref<string | null>(null)

// Interactive simulation state
const liveTick = ref(0)
const isSpikeActive = ref(false)
let timer: any = null

const presets: GrafanaDashboardPreset[] = [
  {
    id: 'vibration-fft',
    title: 'Spindle Vibration & High-Frequency FFT',
    description: 'High-speed ISO 10816 vibration severity waterfalls, FFT spectrums, and harmonic peak tracking.',
    icon: Activity,
    path: '/d/spindle-fft-analysis/spindle-vibration-and-high-frequency-fft',
    tag: 'Vibration & FFT'
  },
  {
    id: 'spc-cpk',
    title: 'Statistical Process Control & Cpk Heatmaps',
    description: 'Real-time X-bar & R-charts, 3-sigma boundaries, process capability indices (Cp/Cpk), and drift alarms.',
    icon: TrendingUp,
    path: '/d/spc-capability/statistical-process-control-and-cpk-heatmaps',
    tag: 'Process Quality'
  },
  {
    id: 'thermal-current',
    title: 'Multi-Axis Motor Thermal & Current Draw',
    description: 'Drive stator winding temperatures, torque-to-current ratios, and regenerative braking power metrics.',
    icon: Flame,
    path: '/d/motor-thermal/multi-axis-motor-thermal-and-current-draw',
    tag: 'Drive Telemetry'
  },
  {
    id: 'prometheus-ot',
    title: 'Prometheus OT Edge Scrape Metrics',
    description: 'Direct Prometheus scraper overview across IPC Beckhoff TwinCAT, Siemens S7, and field gateways.',
    icon: Radio,
    path: '/d/heimdall-edge-metrics/prometheus-ot-edge-scrape-metrics',
    tag: 'Infrastructure'
  }
]

onMounted(() => {
  if (typeof window !== 'undefined') {
    const saved = localStorage.getItem('heimdall_grafana_url')
    if (saved) grafanaBaseUrl.value = saved
  }
  timer = setInterval(() => {
    liveTick.value++
  }, 2500)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})

const triggerAnomalyPulse = () => {
  isSpikeActive.value = true
  liveTick.value++
  setTimeout(() => {
    isSpikeActive.value = false
    liveTick.value++
  }, 6500)
}

const handleUrlChange = () => {
  if (typeof window !== 'undefined') {
    localStorage.setItem('heimdall_grafana_url', grafanaBaseUrl.value)
  }
  iframeKey.value++
}

const activePreset = computed(() => {
  return presets.find(p => p.id === selectedPresetId.value) || presets[0]
})

const fullEmbedUrl = computed(() => {
  const base = grafanaBaseUrl.value.replace(/\/+$/, '')
  const query = new URLSearchParams({
    theme: 'dark',
    refresh: autoRefresh.value
  })
  if (kioskMode.value) query.append('kiosk', 'tv')
  return `${base}${activePreset.value.path}?${query.toString()}`
})

const directPortalUrl = computed(() => {
  const base = grafanaBaseUrl.value.replace(/\/+$/, '')
  return `${base}${activePreset.value.path}`
})

const copyText = async (text: string, field: string) => {
  try {
    await navigator.clipboard.writeText(text)
    copiedField.value = field
    setTimeout(() => {
      copiedField.value = null
    }, 2000)
  } catch {
    // Ignore fallback
  }
}

const refreshIframe = () => {
  iframeKey.value++
}

// -------------------------------------------------------------
// Interactive FFT Spectral Model (Preset 1)
// -------------------------------------------------------------
const fftBins = computed(() => {
  const bins: Array<{ freq: number; amp: number; peakLabel?: string }> = []
  const tick = liveTick.value
  const spike = isSpikeActive.value

  for (let f = 20; f <= 1200; f += 20) {
    let base = 0.08 + Math.sin(f * 0.05 + tick * 0.3) * 0.03
    let label: string | undefined

    if (f === 48 || f === 50) {
      base = 1.82 + Math.sin(tick * 0.6) * 0.15
      label = '1X RPM (48Hz)'
    } else if (f === 96 || f === 100) {
      base = 0.94 + Math.cos(tick * 0.5) * 0.09
      label = '2X Align (96Hz)'
    } else if (f === 144 || f === 140) {
      base = 0.52 + Math.sin(tick * 0.4) * 0.06
      label = '3X Harm'
    } else if (f === 720) {
      base = spike ? 2.95 : 0.68 + Math.sin(tick * 0.8) * 0.08
      label = spike ? 'BPFO BEARING CRITICAL' : 'BPFO Cage'
    }

    bins.push({
      freq: f,
      amp: Math.max(0.04, Math.round(base * 100) / 100),
      peakLabel: label
    })
  }
  return bins
})

const hoveredFftBin = ref<{ freq: number; amp: number; peakLabel?: string } | null>(null)

// -------------------------------------------------------------
// Interactive ISO 10816-3 Vibration Severity Trend (Preset 1)
// -------------------------------------------------------------
const vibrationTrendPoints = computed(() => {
  const pts: number[] = []
  const tick = liveTick.value
  const spike = isSpikeActive.value

  for (let i = 0; i < 24; i++) {
    let v = 1.35 + Math.sin((i + tick) * 0.5) * 0.22
    if (spike && i >= 19) {
      v = 3.45 + Math.sin(i) * 0.35
    }
    pts.push(Math.round(v * 100) / 100)
  }
  return pts
})

// -------------------------------------------------------------
// Interactive SPC X-Bar & Capability Model (Preset 2)
// -------------------------------------------------------------
const spcSamples = computed(() => {
  const pts: Array<{ subgroup: number; mean: number; range: number; isOutlier: boolean }> = []
  const tick = liveTick.value
  const spike = isSpikeActive.value

  for (let i = 1; i <= 20; i++) {
    let mean = 50.0 + Math.sin(i * 1.3 + tick * 0.4) * 0.05
    let range = 0.08 + Math.cos(i * 0.8) * 0.02
    let isOutlier = false

    if (spike && i === 18) {
      mean = 50.18 // Exceeds UCL 50.14
      isOutlier = true
    }

    pts.push({
      subgroup: i,
      mean: Math.round(mean * 1000) / 1000,
      range: Math.round(range * 1000) / 1000,
      isOutlier
    })
  }
  return pts
})

// -------------------------------------------------------------
// Interactive Multi-Axis Thermal Model (Preset 3)
// -------------------------------------------------------------
const motorThermalStats = computed(() => {
  const tick = liveTick.value
  const spike = isSpikeActive.value

  return {
    axis1: Math.round((spike ? 68.4 : 48.2 + Math.sin(tick * 0.4) * 1.8) * 10) / 10,
    axis2: Math.round((38.5 + Math.cos(tick * 0.3) * 1.2) * 10) / 10,
    axis3: Math.round((42.1 + Math.sin(tick * 0.5) * 1.1) * 10) / 10,
    axis4: Math.round((36.4 + Math.cos(tick * 0.6) * 0.9) * 10) / 10,
    dcBusVolt: Math.round((562.0 + Math.sin(tick * 0.8) * 3.5) * 10) / 10,
    brakePowerKw: Math.round((spike ? 4.8 : 1.25 + Math.sin(tick * 0.5) * 0.3) * 100) / 100
  }
})
</script>

<template>
  <div class="space-y-6">
    <!-- Architecture Notice Hero -->
    <div class="p-5 rounded-2xl bg-zinc-900/90 border border-zinc-800 shadow-sm flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div class="space-y-1">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300">
            <Layers class="w-4 h-4" />
          </div>
          <h3 class="text-sm font-semibold text-zinc-100">Grafana OT Telemetry & Mass Visualization Workspace</h3>
        </div>
        <p class="text-xs text-zinc-400">
          Heimdall provides instant predictive maintenance alerts and domain diagnostics, delegating heavy time-series exploration and multi-gigabyte sensor waterfalls to dedicated Grafana instances.
        </p>
      </div>

      <div class="flex items-center gap-2 shrink-0">
        <a
          :href="directPortalUrl"
          target="_blank"
          rel="noopener noreferrer"
          class="px-3 py-1.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 border border-zinc-700 text-zinc-200 hover:text-white text-xs font-medium flex items-center gap-1.5 transition-colors"
        >
          <span>Open Fullscreen in Grafana</span>
          <ExternalLink class="w-3.5 h-3.5" />
        </a>
      </div>
    </div>

    <!-- Quick Presets Navigation Cards -->
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <div
        v-for="preset in presets"
        :key="preset.id"
        role="button"
        tabindex="0"
        @click="selectedPresetId = preset.id"
        :class="selectedPresetId === preset.id ? 'border-zinc-500 bg-zinc-900 shadow-md ring-1 ring-zinc-500/50' : 'border-zinc-800 bg-zinc-950/80 hover:border-zinc-700 hover:bg-zinc-900/50'"
        class="p-4 rounded-xl border transition-all cursor-pointer space-y-2 group text-left"
      >
        <div class="flex items-center justify-between">
          <div class="p-2 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300 group-hover:text-white transition-colors">
            <component :is="preset.icon" class="w-4 h-4" />
          </div>
          <Badge variant="outline" class="text-[10px] font-mono border-zinc-700 bg-zinc-950 text-zinc-300">
            {{ preset.tag }}
          </Badge>
        </div>

        <h4 class="text-xs font-semibold text-zinc-100 group-hover:text-white">{{ preset.title }}</h4>
        <p class="text-[11px] text-zinc-400 line-clamp-2">{{ preset.description }}</p>
      </div>
    </div>

    <!-- Interactive Grafana Workspace & Controls -->
    <div class="bg-zinc-950 border border-zinc-800 rounded-2xl overflow-hidden shadow-lg space-y-0">
      <!-- Toolbar Header (Line 182) -->
      <div class="p-4 border-b border-zinc-800/80 bg-zinc-900/60 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
        <div class="flex flex-wrap items-center gap-3">
          <!-- Mode Switcher: Interactive Demo vs Live External Server -->
          <div class="flex items-center bg-zinc-950 p-1 rounded-xl border border-zinc-800 text-xs font-medium">
            <button
              type="button"
              @click="viewMode = 'demo'"
              :class="viewMode === 'demo' ? 'bg-indigo-600 text-white shadow-sm' : 'text-zinc-400 hover:text-zinc-200'"
              class="px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5 cursor-pointer"
            >
              <span class="size-2 rounded-full bg-emerald-400 animate-pulse"></span>
              <span>Interactive Demo</span>
            </button>
            <button
              type="button"
              @click="viewMode = 'iframe'"
              :class="viewMode === 'iframe' ? 'bg-indigo-600 text-white shadow-sm' : 'text-zinc-400 hover:text-zinc-200'"
              class="px-3 py-1.5 rounded-lg transition-all flex items-center gap-1.5 cursor-pointer"
            >
              <Server class="w-3.5 h-3.5" />
              <span>External Server (Iframe)</span>
            </button>
          </div>

          <!-- Demo Mode Controls -->
          <template v-if="viewMode === 'demo'">
            <div class="flex items-center gap-1 text-zinc-400 bg-zinc-950 border border-zinc-800 rounded-lg px-2.5 py-1">
              <Clock class="w-3.5 h-3.5 text-zinc-500" />
              <span>Time:</span>
              <select
                v-model="demoTimeRange"
                class="bg-transparent text-xs text-zinc-200 focus:outline-none cursor-pointer"
              >
                <option value="5m">Last 5m</option>
                <option value="15m">Last 15m</option>
                <option value="1h">Last 1h</option>
                <option value="24h">Last 24h</option>
              </select>
            </div>

            <button
              type="button"
              @click="triggerAnomalyPulse"
              :class="isSpikeActive ? 'bg-rose-950/70 text-rose-300 border-rose-500/50' : 'bg-zinc-900 hover:bg-zinc-800 text-amber-300 border-amber-500/30'"
              class="px-2.5 py-1 rounded-lg border text-xs font-mono font-medium flex items-center gap-1.5 transition-all cursor-pointer"
              title="Inject a transient harmonic anomaly into telemetry signals"
            >
              <Zap class="w-3.5 h-3.5 text-amber-400" />
              <span>{{ isSpikeActive ? 'Spike Injected (Active)' : 'Inject Telemetry Spike' }}</span>
            </button>
          </template>

          <!-- External Server Mode Controls -->
          <template v-else>
            <div class="flex items-center gap-1.5 bg-zinc-950 border border-zinc-800 rounded-lg px-2.5 py-1">
              <Server class="w-3.5 h-3.5 text-zinc-500" />
              <input
                v-model="grafanaBaseUrl"
                @change="handleUrlChange"
                type="text"
                class="bg-transparent text-xs text-zinc-200 focus:outline-none w-48 font-mono"
                placeholder="http://localhost:3001"
              />
            </div>

            <label class="flex items-center gap-1.5 text-zinc-300 cursor-pointer select-none">
              <input type="checkbox" v-model="kioskMode" class="rounded bg-zinc-900 border-zinc-700 text-zinc-500" />
              <span>Kiosk</span>
            </label>
          </template>
        </div>

        <div class="flex items-center gap-2">
          <Badge variant="outline" class="text-[10px] font-mono border-emerald-500/30 text-emerald-400 bg-emerald-950/40 flex items-center gap-1">
            <span class="size-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
            <span>Infinity Engine: Online</span>
          </Badge>

          <Button
            v-if="viewMode === 'iframe'"
            variant="outline"
            size="sm"
            @click="refreshIframe"
            class="h-8 border-zinc-800 bg-zinc-950 text-zinc-300 hover:text-white text-xs flex items-center gap-1.5 cursor-pointer"
          >
            <RefreshCw class="w-3.5 h-3.5" />
            <span>Reload</span>
          </Button>

          <a
            :href="directPortalUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="h-8 px-2.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 border border-zinc-700 text-zinc-200 hover:text-white flex items-center justify-center transition-colors"
            title="Open in Grafana New Tab"
          >
            <Maximize2 class="w-3.5 h-3.5" />
          </a>
        </div>
      </div>

      <!-- WORKSPACE VIEWPORT 1: INTERACTIVE GRAFANA DEMO CANVAS -->
      <div v-if="viewMode === 'demo'" class="p-6 bg-[#111217] space-y-6 min-h-[620px]">
        <!-- Grafana Dashboard Breadcrumb Header -->
        <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 pb-3 border-b border-zinc-800/80 text-xs">
          <div class="flex items-center gap-2 font-mono text-zinc-400">
            <span class="text-zinc-500">Dashboards / Industrial OT /</span>
            <span class="text-zinc-100 font-semibold">{{ activePreset.title }}</span>
            <Badge variant="outline" class="text-[9px] font-mono border-zinc-700 text-zinc-400">
              UID: heimdall-{{ activePreset.id }}
            </Badge>
          </div>
          <div class="flex items-center gap-3 text-[11px] font-mono text-zinc-500">
            <span>Datasource: <strong class="text-zinc-300">Infinity JSON [OT Telemetry]</strong></span>
            <span>•</span>
            <span>Interval: <strong class="text-zinc-300">100ms</strong></span>
          </div>
        </div>

        <!-- PRESET 1: SPINDLE VIBRATION & HIGH-FREQUENCY FFT -->
        <div v-if="selectedPresetId === 'vibration-fft'" class="space-y-6">
          <!-- Top Panel: FFT Frequency Spectrum (0-1200 Hz) -->
          <div class="bg-[#181b1f] border border-zinc-800 rounded-xl p-4 shadow-sm space-y-3">
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <Activity class="w-4 h-4 text-cyan-400" />
                <h4 class="text-xs font-semibold text-zinc-200 uppercase tracking-wide">High-Frequency FFT Spectral Spectrum (g / √Hz)</h4>
              </div>
              <div class="flex items-center gap-2 text-[11px] font-mono text-zinc-400">
                <span class="flex items-center gap-1"><span class="size-2 bg-cyan-400 rounded-sm"></span> 1X RPM (48Hz)</span>
                <span class="flex items-center gap-1"><span class="size-2 bg-amber-400 rounded-sm"></span> 2X Align (96Hz)</span>
                <span class="flex items-center gap-1"><span class="size-2 bg-rose-500 rounded-sm"></span> 720Hz (BPFO Bearing Cage)</span>
              </div>
            </div>

            <!-- SVG FFT Bars & Envelope -->
            <div class="relative bg-zinc-950/90 rounded-lg p-3 border border-zinc-900 overflow-hidden">
              <svg viewBox="0 0 800 180" class="w-full h-44 select-none">
                <!-- Grid lines -->
                <g stroke="#26292e" stroke-width="1">
                  <line x1="40" y1="20" x2="780" y2="20" />
                  <line x1="40" y1="65" x2="780" y2="65" />
                  <line x1="40" y1="110" x2="780" y2="110" />
                  <line x1="40" y1="155" x2="780" y2="155" />
                </g>

                <!-- Frequency Bins Bars -->
                <g>
                  <rect
                    v-for="(bin, idx) in fftBins"
                    :key="idx"
                    :x="45 + idx * 12"
                    :y="155 - (bin.amp / 3.2) * 135"
                    width="8"
                    :height="(bin.amp / 3.2) * 135"
                    :fill="bin.freq === 720 && isSpikeActive ? '#f43f5e' : (bin.peakLabel ? '#06b6d4' : '#2dd4bf')"
                    :opacity="bin.peakLabel ? 0.95 : 0.4"
                    class="transition-all hover:opacity-100 cursor-pointer"
                    @mouseenter="hoveredFftBin = bin"
                    @mouseleave="hoveredFftBin = null"
                  />
                </g>

                <!-- Labels on specific harmonics -->
                <text x="95" y="42" fill="#38bdf8" font-size="9" font-family="monospace">1X (48Hz)</text>
                <text x="145" y="80" fill="#38bdf8" font-size="9" font-family="monospace">2X (96Hz)</text>
                <text
                  :x="45 + 35 * 12 - 10"
                  :y="isSpikeActive ? 22 : 88"
                  :fill="isSpikeActive ? '#f43f5e' : '#f59e0b'"
                  font-size="9"
                  font-weight="bold"
                  font-family="monospace"
                >
                  BPFO (720Hz)
                </text>

                <!-- Y-axis scale -->
                <text x="32" y="24" fill="#64748b" font-size="9" text-anchor="end" font-family="monospace">3.0</text>
                <text x="32" y="69" fill="#64748b" font-size="9" text-anchor="end" font-family="monospace">2.0</text>
                <text x="32" y="114" fill="#64748b" font-size="9" text-anchor="end" font-family="monospace">1.0</text>
                <text x="32" y="158" fill="#64748b" font-size="9" text-anchor="end" font-family="monospace">0.0</text>
              </svg>

              <!-- Hover Tooltip -->
              <div
                v-if="hoveredFftBin"
                class="absolute top-2 right-2 p-2 rounded bg-zinc-900 border border-zinc-700 text-[11px] font-mono text-zinc-200 shadow"
              >
                <div>Frequency: <strong>{{ hoveredFftBin.freq }} Hz</strong></div>
                <div>Amplitude: <strong class="text-cyan-400">{{ hoveredFftBin.amp }} g/√Hz</strong></div>
                <div v-if="hoveredFftBin.peakLabel" class="text-amber-400 font-semibold">{{ hoveredFftBin.peakLabel }}</div>
              </div>
            </div>
          </div>

          <!-- Bottom Grid: ISO 10816-3 Velocity Trend & Tri-Axial Gauges -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
            <!-- 2-col wide: ISO 10816 Velocity RMS Timeseries -->
            <div class="lg:col-span-2 bg-[#181b1f] border border-zinc-800 rounded-xl p-4 shadow-sm space-y-3">
              <div class="flex items-center justify-between">
                <h4 class="text-xs font-semibold text-zinc-200 uppercase tracking-wide">ISO 10816-3 Spindle Velocity RMS (mm/s)</h4>
                <div class="flex items-center gap-2 text-[10px] font-mono">
                  <span class="text-amber-400">--- Warning: 2.80 mm/s</span>
                  <span class="text-rose-400">--- Critical: 4.50 mm/s</span>
                </div>
              </div>

              <div class="relative bg-zinc-950/90 rounded-lg p-3 border border-zinc-900">
                <svg viewBox="0 0 540 140" class="w-full h-36 select-none">
                  <!-- Limits -->
                  <line x1="30" y1="52" x2="520" y2="52" stroke="#f59e0b" stroke-dasharray="3,3" stroke-width="1" />
                  <line x1="30" y1="20" x2="520" y2="20" stroke="#f43f5e" stroke-dasharray="3,3" stroke-width="1" />

                  <!-- Trend Line -->
                  <polyline
                    fill="none"
                    stroke="#10b981"
                    stroke-width="2"
                    :points="vibrationTrendPoints.map((v, i) => `${35 + i * 21},${125 - (v / 5.0) * 110}`).join(' ')"
                  />

                  <!-- Points -->
                  <circle
                    v-for="(v, i) in vibrationTrendPoints"
                    :key="i"
                    :cx="35 + i * 21"
                    :cy="125 - (v / 5.0) * 110"
                    r="2.5"
                    :fill="v >= 2.8 ? '#f43f5e' : '#10b981'"
                  />

                  <!-- Y-Axis -->
                  <text x="24" y="24" fill="#f43f5e" font-size="8" font-family="monospace">4.5</text>
                  <text x="24" y="56" fill="#f59e0b" font-size="8" font-family="monospace">2.8</text>
                  <text x="24" y="128" fill="#64748b" font-size="8" font-family="monospace">0.0</text>
                </svg>
              </div>
            </div>

            <!-- 1-col wide: Kinematics & Tri-Axial Summary -->
            <div class="bg-[#181b1f] border border-zinc-800 rounded-xl p-4 shadow-sm flex flex-col justify-between space-y-3">
              <h4 class="text-xs font-semibold text-zinc-200 uppercase tracking-wide">Tri-Axial RMS Breakdown</h4>
              <div class="space-y-2.5 font-mono text-xs">
                <div class="p-2 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Radial (X-Axis):</span>
                  <span class="text-emerald-400 font-bold">1.28 mm/s</span>
                </div>
                <div class="p-2 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Tangential (Y-Axis):</span>
                  <span :class="isSpikeActive ? 'text-rose-400' : 'text-emerald-400'" class="font-bold">
                    {{ isSpikeActive ? '3.42 mm/s' : '1.42 mm/s' }}
                  </span>
                </div>
                <div class="p-2 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Axial (Z-Axis):</span>
                  <span class="text-emerald-400 font-bold">0.85 mm/s</span>
                </div>
                <div class="p-2 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Crest Factor / Kurtosis:</span>
                  <span class="text-cyan-400">3.18 / 3.02</span>
                </div>
              </div>

              <div class="text-[10px] text-zinc-500 font-mono text-center">
                ISO 10816 Severity Class: <span class="text-emerald-400 font-semibold">Zone A (Good)</span>
              </div>
            </div>
          </div>
        </div>

        <!-- PRESET 2: STATISTICAL PROCESS CONTROL & CPK HEATMAPS -->
        <div v-else-if="selectedPresetId === 'spc-cpk'" class="space-y-6">
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-4">
            <!-- X-Bar Control Chart -->
            <div class="lg:col-span-2 bg-[#181b1f] border border-zinc-800 rounded-xl p-4 shadow-sm space-y-3">
              <div class="flex items-center justify-between">
                <h4 class="text-xs font-semibold text-zinc-200 uppercase tracking-wide">Real-Time X-Bar Chart (Subgroup Averages)</h4>
                <div class="text-[10px] font-mono text-zinc-400">
                  <span class="text-rose-400">UCL = 50.14mm</span> •
                  <span class="text-emerald-400">CL = 50.00mm</span> •
                  <span class="text-rose-400">LCL = 49.86mm</span>
                </div>
              </div>

              <div class="relative bg-zinc-950/90 rounded-lg p-3 border border-zinc-900">
                <svg viewBox="0 0 540 150" class="w-full h-40 select-none">
                  <!-- Control Limits -->
                  <line x1="30" y1="25" x2="520" y2="25" stroke="#f43f5e" stroke-dasharray="3,3" stroke-width="1" />
                  <line x1="30" y1="75" x2="520" y2="75" stroke="#10b981" stroke-width="1.5" />
                  <line x1="30" y1="125" x2="520" y2="125" stroke="#f43f5e" stroke-dasharray="3,3" stroke-width="1" />

                  <!-- Trend Line -->
                  <polyline
                    fill="none"
                    stroke="#38bdf8"
                    stroke-width="2"
                    :points="spcSamples.map((s, i) => `${35 + i * 25},${75 - ((s.mean - 50.0) / 0.16) * 50}`).join(' ')"
                  />

                  <!-- Points -->
                  <circle
                    v-for="(s, i) in spcSamples"
                    :key="i"
                    :cx="35 + i * 25"
                    :cy="75 - ((s.mean - 50.0) / 0.16) * 50"
                    r="3.5"
                    :fill="s.isOutlier ? '#f43f5e' : '#38bdf8'"
                    :stroke="s.isOutlier ? '#ffffff' : 'none'"
                    stroke-width="1"
                  />
                </svg>
              </div>
            </div>

            <!-- Capability Summary -->
            <div class="bg-[#181b1f] border border-zinc-800 rounded-xl p-4 shadow-sm flex flex-col justify-between space-y-3">
              <h4 class="text-xs font-semibold text-zinc-200 uppercase tracking-wide">Capability Indices (Cp / Cpk)</h4>
              <div class="space-y-2 font-mono text-xs">
                <div class="p-2.5 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Cp (Potential):</span>
                  <span class="text-emerald-400 font-bold text-sm">1.67</span>
                </div>
                <div class="p-2.5 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Cpk (Actual):</span>
                  <span :class="isSpikeActive ? 'text-amber-400' : 'text-emerald-400'" class="font-bold text-sm">
                    {{ isSpikeActive ? '1.24 (Drift)' : '1.58' }}
                  </span>
                </div>
                <div class="p-2.5 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Ppk (Performance):</span>
                  <span class="text-cyan-400 font-bold">1.52</span>
                </div>
                <div class="p-2.5 rounded bg-zinc-950 border border-zinc-900 flex items-center justify-between">
                  <span class="text-zinc-400">Estimated PPM:</span>
                  <span class="text-zinc-200 font-bold">&lt; 0.8 PPM</span>
                </div>
              </div>

              <div class="text-[10px] text-zinc-500 font-mono text-center">
                Statistical Status: <span class="text-emerald-400 font-semibold">Six-Sigma Compliant</span>
              </div>
            </div>
          </div>
        </div>

        <!-- PRESET 3: MULTI-AXIS MOTOR THERMAL & CURRENT DRAW -->
        <div v-else-if="selectedPresetId === 'thermal-current'" class="space-y-6">
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-1">
              <span class="text-zinc-400 text-xs block">Axis 1 (Spindle PT100)</span>
              <div :class="motorThermalStats.axis1 > 60 ? 'text-rose-400' : 'text-emerald-400'" class="text-2xl font-bold font-mono">
                {{ motorThermalStats.axis1 }} °C
              </div>
              <span class="text-[10px] text-zinc-500 font-mono">Nominal: 45°C • Max: 75°C</span>
            </div>
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-1">
              <span class="text-zinc-400 text-xs block">Axis 2 (Robot Gantry X)</span>
              <div class="text-2xl font-bold font-mono text-cyan-400">{{ motorThermalStats.axis2 }} °C</div>
              <span class="text-[10px] text-zinc-500 font-mono">Nominal: 40°C • Max: 70°C</span>
            </div>
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-1">
              <span class="text-zinc-400 text-xs block">Axis 3 (Robot Gantry Y)</span>
              <div class="text-2xl font-bold font-mono text-indigo-400">{{ motorThermalStats.axis3 }} °C</div>
              <span class="text-[10px] text-zinc-500 font-mono">Nominal: 40°C • Max: 70°C</span>
            </div>
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-1">
              <span class="text-zinc-400 text-xs block">DC Intermediate Bus</span>
              <div class="text-2xl font-bold font-mono text-amber-400">{{ motorThermalStats.dcBusVolt }} VDC</div>
              <span class="text-[10px] text-zinc-500 font-mono">Braking: {{ motorThermalStats.brakePowerKw }} kW</span>
            </div>
          </div>
        </div>

        <!-- PRESET 4: PROMETHEUS OT EDGE SCRAPE METRICS -->
        <div v-else-if="selectedPresetId === 'prometheus-ot'" class="space-y-6">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-2">
              <span class="text-xs text-zinc-400">Scrape Ingestion Rate</span>
              <div class="text-2xl font-bold font-mono text-emerald-400">14,280 / sec</div>
              <span class="text-[10px] text-zinc-500 font-mono">Total timeseries samples scraped</span>
            </div>
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-2">
              <span class="text-xs text-zinc-400">Driver Scrape Duration</span>
              <div class="text-2xl font-bold font-mono text-cyan-400">3.8 ms</div>
              <span class="text-[10px] text-zinc-500 font-mono">TwinCAT ADS round-trip latency</span>
            </div>
            <div class="p-4 rounded-xl bg-[#181b1f] border border-zinc-800 space-y-2">
              <span class="text-xs text-zinc-400">Scrape Success Rate</span>
              <div class="text-2xl font-bold font-mono text-emerald-400">99.99%</div>
              <span class="text-[10px] text-zinc-500 font-mono">0 dropped frames across fieldbus</span>
            </div>
          </div>
        </div>
      </div>

      <!-- WORKSPACE VIEWPORT 2: LIVE EXTERNAL GRAFANA IFRAME (Keep mounted in DOM for tests) -->
      <div v-show="viewMode === 'iframe'" class="relative w-full h-[620px] bg-zinc-950 flex items-center justify-center">
        <iframe
          :key="iframeKey"
          :src="fullEmbedUrl"
          class="w-full h-full border-0"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          sandbox="allow-same-origin allow-scripts allow-forms allow-popups"
        ></iframe>

        <!-- Fallback / Connection Notice Overlay -->
        <div class="absolute bottom-4 right-4 max-w-sm p-3 rounded-xl bg-zinc-900/95 border border-zinc-800 shadow-xl text-xs text-zinc-400 space-y-2 pointer-events-auto">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2 text-zinc-200 font-semibold">
              <Radio class="w-3.5 h-3.5 text-amber-400" />
              <span>Targeting Grafana Instance</span>
            </div>
            <button
              type="button"
              @click="viewMode = 'demo'"
              class="px-2 py-0.5 rounded bg-indigo-600 hover:bg-indigo-500 text-white text-[10px] font-medium cursor-pointer"
            >
              Switch to Demo
            </button>
          </div>
          <p class="text-[11px] font-mono text-zinc-400 truncate">{{ fullEmbedUrl }}</p>
          <p class="text-[10px] text-zinc-500">
            If offline, run: <code class="text-zinc-300">docker run -d -p 3001:3000 -e "GF_SECURITY_ALLOW_EMBEDDING=true" grafana/grafana</code>
          </p>
        </div>
      </div>
    </div>

    <!-- Heimdall Native Metrics Exporters Card -->
    <div class="bg-zinc-900/90 border border-zinc-800 rounded-2xl p-6 shadow-sm space-y-4">
      <div class="flex items-center gap-2.5">
        <div class="p-2 rounded-lg bg-zinc-800 border border-zinc-700 text-zinc-300">
          <Terminal class="w-4 h-4" />
        </div>
        <div>
          <h4 class="text-sm font-semibold text-zinc-100">Heimdall OT Telemetry Exporters for Grafana & Prometheus</h4>
          <p class="text-xs text-zinc-400">Copy pre-configured endpoints to plug Heimdall data directly into your Grafana dashboards.</p>
        </div>
      </div>

      <div class="grid grid-cols-1 md:grid-cols-3 gap-4 font-mono text-xs">
        <!-- 1. Grafana Metrics Endpoint -->
        <div class="p-3.5 rounded-xl bg-zinc-950 border border-zinc-800 space-y-2">
          <div class="flex items-center justify-between text-zinc-300">
            <span class="font-semibold text-zinc-200">Prometheus Exporter</span>
            <button
              type="button"
              @click="copyText('http://localhost:5099/api/v1/ReportExport/grafana/metrics', 'prom')"
              class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-white transition-colors cursor-pointer"
              title="Copy URL"
            >
              <Check v-if="copiedField === 'prom'" class="w-3.5 h-3.5 text-emerald-400" />
              <Copy v-else class="w-3.5 h-3.5" />
            </button>
          </div>
          <p class="text-[11px] text-zinc-500 font-sans">Scrape target for Prometheus / Grafana Agent</p>
          <div class="p-2 rounded bg-zinc-900 border border-zinc-800/80 text-[11px] text-zinc-300 break-all">
            http://localhost:5099/api/v1/ReportExport/grafana/metrics
          </div>
        </div>

        <!-- 2. OData Machine Stream -->
        <div class="p-3.5 rounded-xl bg-zinc-950 border border-zinc-800 space-y-2">
          <div class="flex items-center justify-between text-zinc-300">
            <span class="font-semibold text-zinc-200">OData Telemetry Feed</span>
            <button
              type="button"
              @click="copyText('http://localhost:5099/api/v1/ReportExport/odata/telemetry', 'odata')"
              class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-white transition-colors cursor-pointer"
              title="Copy URL"
            >
              <Check v-if="copiedField === 'odata'" class="w-3.5 h-3.5 text-emerald-400" />
              <Copy v-else class="w-3.5 h-3.5" />
            </button>
          </div>
          <p class="text-[11px] text-zinc-500 font-sans">High-frequency tabular stream for Infinity / SQL plugin</p>
          <div class="p-2 rounded bg-zinc-900 border border-zinc-800/80 text-[11px] text-zinc-300 break-all">
            http://localhost:5099/api/v1/ReportExport/odata/telemetry
          </div>
        </div>

        <!-- 3. Machine Asset Registry -->
        <div class="p-3.5 rounded-xl bg-zinc-950 border border-zinc-800 space-y-2">
          <div class="flex items-center justify-between text-zinc-300">
            <span class="font-semibold text-zinc-200">Asset Dimensions Feed</span>
            <button
              type="button"
              @click="copyText('http://localhost:5099/api/v1/ReportExport/odata/machines', 'machines')"
              class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-white transition-colors cursor-pointer"
              title="Copy URL"
            >
              <Check v-if="copiedField === 'machines'" class="w-3.5 h-3.5 text-emerald-400" />
              <Copy v-else class="w-3.5 h-3.5" />
            </button>
          </div>
          <p class="text-[11px] text-zinc-500 font-sans">Spatial DXF tags and machine hierarchy metadata</p>
          <div class="p-2 rounded bg-zinc-900 border border-zinc-800/80 text-[11px] text-zinc-300 break-all">
            http://localhost:5099/api/v1/ReportExport/odata/machines
          </div>
        </div>
      </div>
    </div>
  </div>
</template>
