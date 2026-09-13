<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
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
  Radio
} from 'lucide-vue-next'

interface GrafanaDashboardPreset {
  id: string
  title: string
  description: string
  icon: any
  path: string
  tag: string
}

const grafanaBaseUrl = ref('http://localhost:3001')
const kioskMode = ref(true)
const autoRefresh = ref('10s')
const selectedPresetId = ref('vibration-fft')
const iframeKey = ref(0)
const copiedField = ref<string | null>(null)

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
})

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
      <!-- Toolbar Header -->
      <div class="p-4 border-b border-zinc-800/80 bg-zinc-900/60 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs">
        <div class="flex flex-wrap items-center gap-3">
          <!-- Server URL Input -->
          <div class="flex items-center gap-1.5 bg-zinc-950 border border-zinc-800 rounded-lg px-2.5 py-1">
            <Server class="w-3.5 h-3.5 text-zinc-500" />
            <input
              v-model="grafanaBaseUrl"
              @change="handleUrlChange"
              type="text"
              class="bg-transparent text-xs text-zinc-200 focus:outline-none w-52 font-mono"
              placeholder="http://localhost:3001"
            />
          </div>

          <!-- Kiosk Toggle -->
          <label class="flex items-center gap-1.5 text-zinc-300 cursor-pointer select-none">
            <input type="checkbox" v-model="kioskMode" class="rounded bg-zinc-900 border-zinc-700 text-zinc-500" />
            <span>Kiosk Mode (Clean UI)</span>
          </label>

          <!-- Refresh Rate -->
          <div class="flex items-center gap-1 text-zinc-400">
            <span>Refresh:</span>
            <select
              v-model="autoRefresh"
              class="bg-zinc-950 border border-zinc-800 rounded px-2 py-0.5 text-xs text-zinc-200 focus:outline-none"
            >
              <option value="5s">5s</option>
              <option value="10s">10s</option>
              <option value="30s">30s</option>
              <option value="1m">1m</option>
              <option value="off">Off</option>
            </select>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            @click="refreshIframe"
            class="h-8 border-zinc-800 bg-zinc-950 text-zinc-300 hover:text-white text-xs flex items-center gap-1.5"
          >
            <RefreshCw class="w-3.5 h-3.5" />
            <span>Reload Canvas</span>
          </Button>

          <a
            :href="directPortalUrl"
            target="_blank"
            rel="noopener noreferrer"
            class="h-8 px-2.5 rounded-lg bg-zinc-800 hover:bg-zinc-700 border border-zinc-700 text-zinc-200 hover:text-white flex items-center justify-center transition-colors"
            title="Open Fullscreen in New Window"
          >
            <Maximize2 class="w-3.5 h-3.5" />
          </a>
        </div>
      </div>

      <!-- Live Grafana Iframe Workspace -->
      <div class="relative w-full h-[620px] bg-zinc-950 flex items-center justify-center">
        <iframe
          :key="iframeKey"
          :src="fullEmbedUrl"
          class="w-full h-full border-0"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
          sandbox="allow-same-origin allow-scripts allow-forms allow-popups"
        ></iframe>

        <!-- Fallback / Connection Notice Overlay if connection is refused locally -->
        <div class="absolute bottom-4 right-4 max-w-sm p-3 rounded-xl bg-zinc-900/95 border border-zinc-800 shadow-xl text-xs text-zinc-400 space-y-1.5 pointer-events-auto">
          <div class="flex items-center gap-2 text-zinc-200 font-semibold">
            <Radio class="w-3.5 h-3.5 text-amber-400" />
            <span>Targeting Grafana Instance</span>
          </div>
          <p class="text-[11px] font-mono text-zinc-400 truncate">{{ fullEmbedUrl }}</p>
          <p class="text-[10px] text-zinc-500">
            If iframe display is blocked by frame-ancestors headers, set <code class="text-zinc-300">allow_embedding = true</code> in your Grafana configuration.
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
              class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-white transition-colors"
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
              class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-white transition-colors"
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
              class="p-1 rounded hover:bg-zinc-800 text-zinc-400 hover:text-white transition-colors"
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
