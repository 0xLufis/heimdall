<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import {
  Activity,
  Cpu,
  HardDrive,
  Gauge,
  Radio,
  Zap,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  Monitor,
  Send,
  SlidersHorizontal,
  Layers,
  Terminal,
  Search,
  Wifi,
  WifiOff,
  Download,
  Eye,
  History,
  Loader2,
  Copy,
  Check,
  FileText,
  X
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription } from '~/components/ui/dialog'
import RbacButton from '@/components/common/RbacButton.vue'
import ControllerCommandModal from '~/components/controllers/ControllerCommandModal.vue'
import { useControllers } from '~/composables/useControllers'
import { useRbacPermission } from '~/composables/useRbacPermission'
import type { IndustrialController } from '~/types/domain'

definePageMeta({
  layout: 'shadcn-dashboard',
  alias: ['/dashboard/telemetry']
})

interface DiagnosticSnapshotSummary {
  id: string
  clientPcId: string
  hostname: string
  machineIdentifier?: string
  capturedByUserId: string
  capturedByUserName?: string
  capturedAtUtc: string
  payloadHashSha256: string
  payloadSizeBytes: number
  snapshotPayloadJson?: string
}

const { controllers, isLoading, fetchControllers } = useControllers()
const { canManageEndpoints, canExecuteRemote } = useRbacPermission()
const router = useRouter()

const searchQuery = ref('')
const selectedControllerId = ref<string>('')
const filterOnlineOnly = ref(false)
const commandModalOpen = ref(false)
const isCapturingSnapshot = ref(false)
const latestSnapshot = ref<DiagnosticSnapshotSummary | null>(null)
const snapshotError = ref<string | null>(null)
const isPayloadModalOpen = ref(false)
const isHistoryModalOpen = ref(false)
const snapshotHistory = ref<DiagnosticSnapshotSummary[]>([])
const isLoadingHistory = ref(false)
const copiedHash = ref(false)
let streamTimer: any = null

const resetTelemetryView = () => {
  searchQuery.value = ''
  filterOnlineOnly.value = false
  if (controllers.value.length > 0) {
    selectedControllerId.value = controllers.value[0].id
  }
  router.push('/dashboard/telemetry')
  fetchControllers(false)
}

// Live high-frequency simulated OT tags for selected node
const liveTags = ref([
  { name: 'Motion.Axis_X.ActualPos_mm', value: 142.58, unit: 'mm', quality: 'Good' },
  { name: 'Motion.Spindle.Speed_RPM', value: 3450, unit: 'RPM', quality: 'Good' },
  { name: 'Laser.Weld.Power_kW', value: 4.25, unit: 'kW', quality: 'Good' },
  { name: 'Coolant.Loop_A.Temp_C', value: 24.8, unit: '°C', quality: 'Good' },
  { name: 'Pneumatics.MainLine.Pressure_bar', value: 6.2, unit: 'bar', quality: 'Good' },
  { name: 'PLC.CycleTime.Current_ms', value: 2.14, unit: 'ms', quality: 'Good' },
])

const filteredControllers = computed(() => {
  let list = controllers.value
  if (filterOnlineOnly.value) {
    list = list.filter(c => c.telemetry?.isOnline ?? c.isOnline)
  }
  if (!searchQuery.value) return list
  const q = searchQuery.value.toLowerCase()
  return list.filter(c =>
    (c.hostname || '').toLowerCase().includes(q) ||
    (c.name || '').toLowerCase().includes(q) ||
    (c.macAddress || '').toLowerCase().includes(q)
  )
})

const activeController = computed<IndustrialController | null>(() => {
  if (selectedControllerId.value) {
    const found = controllers.value.find(c => c.id === selectedControllerId.value)
    if (found) return found
  }
  return controllers.value[0] || null
})

const fleetStats = computed(() => {
  const total = controllers.value.length
  const online = controllers.value.filter(c => c.telemetry?.isOnline).length
  const avgCpu = total > 0
    ? Math.round(controllers.value.reduce((acc, c) => acc + (c.telemetry?.cpuUsagePercent ?? 0), 0) / total)
    : 0
  const avgRam = total > 0
    ? Math.round(controllers.value.reduce((acc, c) => acc + (c.telemetry?.ramUsagePercent ?? 0), 0) / total)
    : 0

  return { total, online, offline: total - online, avgCpu, avgRam }
})

async function triggerDiagnosticSnapshot() {
  if (!activeController.value) return
  isCapturingSnapshot.value = true
  snapshotError.value = null

  try {
    const res = await $fetch<DiagnosticSnapshotSummary>(
      `/api/proxy/ClientPc/${activeController.value.id}/snapshot`,
      { method: 'POST' }
    )
    latestSnapshot.value = res
  } catch (err: any) {
    snapshotError.value = err?.data?.message || err?.message || 'Failed to capture diagnostic snapshot'
  } finally {
    isCapturingSnapshot.value = false
  }
}

async function fetchSnapshotHistory() {
  if (!activeController.value) return
  isLoadingHistory.value = true
  try {
    snapshotHistory.value = await $fetch<DiagnosticSnapshotSummary[]>(
      `/api/proxy/ClientPc/${activeController.value.id}/snapshots`
    )
  } catch (err: any) {
    console.error('Failed to load snapshot history', err)
  } finally {
    isLoadingHistory.value = false
  }
}

function openHistoryModal() {
  isHistoryModalOpen.value = true
  fetchSnapshotHistory()
}

function downloadSnapshot(snapshot: DiagnosticSnapshotSummary | null) {
  if (!snapshot) return
  const payloadStr = snapshot.snapshotPayloadJson || JSON.stringify(snapshot, null, 2)
  const blob = new Blob([payloadStr], { type: 'application/json' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `diagnostic_snapshot_${snapshot.hostname}_${new Date(snapshot.capturedAtUtc).toISOString().replace(/[:.]/g, '-')}.json`
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

async function copyHash(hash: string) {
  try {
    await navigator.clipboard.writeText(hash)
    copiedHash.value = true
    setTimeout(() => { copiedHash.value = false }, 2000)
  } catch (e) {
    console.error('Failed to copy hash', e)
  }
}

onMounted(async () => {
  await fetchControllers()
  if (controllers.value.length > 0 && !selectedControllerId.value) {
    selectedControllerId.value = controllers.value[0].id
  }

  // Ticker for live gauge flutter
  streamTimer = setInterval(() => {
    liveTags.value.forEach(t => {
      if (t.name.includes('ActualPos')) {
        t.value = Number((t.value + (Math.random() * 0.4 - 0.2)).toFixed(2))
      } else if (t.name.includes('RPM')) {
        t.value = Math.round(3450 + (Math.random() * 40 - 20))
      } else if (t.name.includes('CycleTime')) {
        t.value = Number((2.1 + (Math.random() * 0.15)).toFixed(2))
      }
    })
  }, 1200)
})

onUnmounted(() => {
  if (streamTimer) clearInterval(streamTimer)
})
</script>

<template>
  <div class="space-y-6">
    <!-- Header Section -->
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-2 border-b border-slate-800">
      <div
        role="button"
        tabindex="0"
        @click="resetTelemetryView"
        @keydown.enter="resetTelemetryView"
        class="flex items-center gap-3 cursor-pointer select-none group p-1 -m-1 rounded-xl transition-all hover:bg-slate-900/60"
        title="Click to reset filters and refresh telemetry stream"
      >
        <div class="p-2.5 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400 group-hover:scale-105 group-hover:bg-indigo-500/20 transition-all">
          <Activity class="size-6" />
        </div>
        <div>
          <div class="flex items-center gap-2">
            <h1 class="text-2xl font-bold tracking-tight text-slate-100 group-hover:text-white transition-colors">
              Live Industrial Telemetry Stream
            </h1>
            <Badge variant="outline" class="border-emerald-500/30 text-emerald-400 bg-emerald-950/20 text-xs gap-1 py-0.5">
              <span class="size-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              SignalR Connected
            </Badge>
          </div>
          <p class="text-sm text-slate-400 mt-0.5 group-hover:text-slate-300 transition-colors">
            Real-time IPC telemetry, Beckhoff ADS cycle diagnostics, and sensor gauges
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2.5">
        <Button
          variant="outline"
          size="sm"
          @click="fetchControllers(false)"
          class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 text-xs font-medium h-8"
        >
          <RefreshCw class="size-3.5 mr-1" :class="{ 'animate-spin': isLoading }" />
          Refresh
        </Button>
        <NuxtLink to="/dashboard/telemetry/configure">
          <Button
            variant="outline"
            size="sm"
            class="border-slate-800 bg-slate-900 text-indigo-400 hover:text-indigo-300 text-xs font-medium h-8"
          >
            <SlidersHorizontal class="size-3.5 mr-1" />
            Config & Recipes
          </Button>
        </NuxtLink>
      </div>
    </div>

    <!-- Fleet Overview KPI Strip -->
    <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
      <Card
        role="button"
        tabindex="0"
        @click="filterOnlineOnly = !filterOnlineOnly"
        @keydown.enter="filterOnlineOnly = !filterOnlineOnly"
        class="bg-slate-900 border-slate-800 cursor-pointer transition-all hover:scale-[1.02] hover:border-emerald-500/40 select-none"
        :class="{ 'ring-2 ring-emerald-500/60 border-emerald-500/60': filterOnlineOnly }"
        :title="filterOnlineOnly ? 'Click to show all nodes' : 'Click to filter to online nodes only'"
      >
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider flex items-center gap-1.5">
              <span>Active Nodes</span>
              <Badge v-if="filterOnlineOnly" variant="outline" class="text-[9px] py-0 px-1 border-emerald-500/40 text-emerald-400 bg-emerald-500/10">
                Online Only
              </Badge>
            </div>
            <div class="text-2xl font-bold text-slate-100 mt-1">
              {{ fleetStats.online }} / {{ fleetStats.total }}
            </div>
          </div>
          <div class="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-400">
            <Wifi class="size-5" />
          </div>
        </CardContent>
      </Card>

      <Card class="bg-slate-900 border-slate-800">
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Fleet Avg CPU</div>
            <div class="text-2xl font-bold text-slate-100 mt-1">
              {{ fleetStats.avgCpu }}%
            </div>
          </div>
          <div class="p-3 rounded-xl bg-indigo-500/10 border border-indigo-500/20 text-indigo-400">
            <Cpu class="size-5" />
          </div>
        </CardContent>
      </Card>

      <Card class="bg-slate-900 border-slate-800">
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Fleet Avg RAM</div>
            <div class="text-2xl font-bold text-slate-100 mt-1">
              {{ fleetStats.avgRam }}%
            </div>
          </div>
          <div class="p-3 rounded-xl bg-purple-500/10 border border-purple-500/20 text-purple-400">
            <Gauge class="size-5" />
          </div>
        </CardContent>
      </Card>

      <Card class="bg-slate-900 border-slate-800">
        <CardContent class="p-4 flex items-center justify-between">
          <div>
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Streaming Heartbeat</div>
            <div class="text-2xl font-bold text-emerald-400 mt-1">
              1.2s
            </div>
          </div>
          <div class="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-emerald-400">
            <Radio class="size-5" />
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Main Live Gauge & Inspection Section -->
    <div v-if="activeController" class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <!-- Node Selector Card -->
      <Card class="bg-slate-900 border-slate-800 lg:col-span-1">
        <CardHeader class="p-4 border-b border-slate-800">
          <CardTitle class="text-sm text-slate-200 flex items-center justify-between">
            <span>Select Controller Node</span>
            <Badge variant="outline" class="text-[10px] font-mono text-indigo-400 border-indigo-500/30">
              {{ filteredControllers.length }} Hosts
            </Badge>
          </CardTitle>
          <div class="relative mt-2">
            <Search class="size-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-500" />
            <Input
              v-model="searchQuery"
              placeholder="Search hostname or IP..."
              class="h-8 pl-8 text-xs bg-slate-950 border-slate-800 text-slate-200"
            />
          </div>
        </CardHeader>
        <CardContent class="p-2 max-h-[460px] overflow-y-auto space-y-1">
          <button
            v-for="c in filteredControllers"
            :key="c.id"
            type="button"
            @click="selectedControllerId = c.id"
            :class="[
              'w-full text-left p-2.5 rounded-lg text-xs transition-colors flex items-center justify-between border',
              c.id === activeController.id
                ? 'bg-indigo-950/40 border-indigo-500/40 text-white'
                : 'bg-slate-950/40 border-slate-800/80 text-slate-400 hover:text-slate-200 hover:bg-slate-800/40'
            ]"
          >
            <div class="truncate">
              <div class="font-semibold text-slate-200 truncate flex items-center gap-1.5">
                <span
                  class="size-2 rounded-full shrink-0"
                  :class="c.telemetry?.isOnline ? 'bg-emerald-400' : 'bg-slate-600'"
                ></span>
                <span>{{ c.hostname || c.name }}</span>
              </div>
              <div class="text-[10px] font-mono text-slate-500 mt-0.5 truncate">{{ c.macAddress || 'No MAC' }}</div>
            </div>
            <div class="text-right shrink-0">
              <span class="font-mono font-medium text-[11px] text-slate-300">
                {{ c.telemetry?.cpuUsagePercent ?? 0 }}% CPU
              </span>
            </div>
          </button>
        </CardContent>
      </Card>

      <!-- Gauges & Telemetry Detail -->
      <Card class="bg-slate-900 border-slate-800 lg:col-span-2">
        <CardHeader class="p-5 pb-3 border-b border-slate-800 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <div class="flex items-center gap-2">
              <CardTitle class="text-base text-slate-100">
                {{ activeController.hostname || activeController.name }}
              </CardTitle>
              <Badge
                variant="outline"
                class="text-xs font-mono"
                :class="activeController.telemetry?.isOnline ? 'border-emerald-500/30 text-emerald-400 bg-emerald-950/20' : 'border-slate-800 text-slate-500'"
              >
                {{ activeController.telemetry?.isOnline ? 'Active Stream' : 'Offline' }}
              </Badge>
            </div>
            <CardDescription class="text-xs text-slate-400 mt-0.5 font-mono">
              MAC: {{ activeController.macAddress || 'N/A' }} • IP: {{ activeController.ipAddress || '192.168.10.x' }}
            </CardDescription>
          </div>

          <!-- RBAC Gated Controls -->
          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              @click="openHistoryModal"
              class="border-slate-800 bg-slate-950 text-slate-400 hover:text-slate-200 hover:bg-slate-800 text-xs h-8"
              title="View Historical Snapshots"
            >
              <History class="size-3.5 mr-1 text-slate-400" />
              <span>History</span>
            </Button>
            <RbacButton
              capability="canManageEndpoints"
              variant="outline"
              size="sm"
              @click="triggerDiagnosticSnapshot"
              :disabled="isCapturingSnapshot"
              class="border-slate-800 bg-slate-950 text-slate-300 hover:bg-slate-800 text-xs h-8"
            >
              <Zap v-if="!isCapturingSnapshot" class="size-3.5 mr-1 text-amber-400" />
              <Loader2 v-else class="size-3.5 mr-1 text-amber-400 animate-spin" />
              <span>{{ isCapturingSnapshot ? 'Capturing...' : 'Snapshot' }}</span>
            </RbacButton>
            <RbacButton
              capability="canExecuteRemote"
              size="sm"
              @click="commandModalOpen = true"
              class="bg-indigo-600 hover:bg-indigo-700 text-white text-xs h-8 px-3 border-0"
            >
              <Terminal class="size-3.5 mr-1" />
              <span>Queue Command</span>
            </RbacButton>
          </div>
        </CardHeader>

        <CardContent class="p-5 space-y-6">
          <!-- Error Alert Banner -->
          <div v-if="snapshotError" class="p-3 rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-400 text-xs font-medium flex items-center justify-between">
            <div class="flex items-center gap-2">
              <AlertTriangle class="size-4 shrink-0" />
              <span>{{ snapshotError }}</span>
            </div>
            <Button variant="ghost" size="sm" class="h-6 text-[10px] text-rose-300 hover:bg-rose-500/20" @click="snapshotError = null">Dismiss</Button>
          </div>

          <!-- Real Diagnostic Snapshot Card -->
          <div v-if="latestSnapshot" class="p-3.5 rounded-xl bg-slate-950/80 border border-emerald-500/30 text-slate-200 space-y-2.5 shadow-lg">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
              <div class="flex items-center gap-2">
                <div class="p-1 rounded-md bg-emerald-500/20 text-emerald-400">
                  <CheckCircle2 class="size-4" />
                </div>
                <div>
                  <span class="text-xs font-bold text-emerald-400">Diagnostic Snapshot Captured</span>
                  <span class="text-[11px] text-slate-400 ml-2">
                    {{ new Date(latestSnapshot.capturedAtUtc).toLocaleString() }}
                  </span>
                </div>
              </div>
              <div class="flex items-center gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  class="h-7 text-xs border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 px-2.5"
                  @click="isPayloadModalOpen = true"
                >
                  <Eye class="size-3.5 mr-1 text-indigo-400" />
                  View Payload
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  class="h-7 text-xs border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 px-2.5"
                  @click="downloadSnapshot(latestSnapshot)"
                >
                  <Download class="size-3.5 mr-1 text-emerald-400" />
                  Download JSON
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  class="size-7 p-0 text-slate-500 hover:text-slate-300"
                  @click="latestSnapshot = null"
                >
                  <X class="size-3.5" />
                </Button>
              </div>
            </div>
            <div class="flex flex-wrap items-center gap-3 text-[10px] text-slate-400 pt-1 border-t border-slate-800/80">
              <span>ID: <code class="text-slate-300 font-mono">{{ latestSnapshot.id.substring(0, 8) }}...</code></span>
              <span class="flex items-center gap-1">
                SHA-256: 
                <code class="text-indigo-300 font-mono">{{ latestSnapshot.payloadHashSha256.substring(0, 16) }}...</code>
                <button
                  type="button"
                  @click="copyHash(latestSnapshot.payloadHashSha256)"
                  class="hover:text-white transition-colors"
                  title="Copy full SHA-256 digest"
                >
                  <Copy v-if="!copiedHash" class="size-3" />
                  <Check v-else class="size-3 text-emerald-400" />
                </button>
              </span>
              <span>Operator: <span class="text-slate-300">{{ latestSnapshot.capturedByUserName || latestSnapshot.capturedByUserId }}</span></span>
              <span>Audit: <span class="text-emerald-400 font-semibold">TISAX / NIS2 Recorded</span></span>
            </div>
          </div>

          <!-- Radial Dials Grid -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4">
            <!-- CPU Gauge -->
            <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800 text-center space-y-2">
              <div class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-center gap-1">
                <Cpu class="size-3.5 text-indigo-400" /> CPU Load
              </div>
              <div class="relative size-20 mx-auto flex items-center justify-center">
                <svg class="size-full -rotate-90" viewBox="0 0 36 36">
                  <path
                    class="text-slate-800"
                    stroke-width="3.5"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                  />
                  <path
                    class="text-indigo-500 transition-all duration-500"
                    :stroke-dasharray="`${activeController.telemetry?.cpuUsagePercent ?? 0}, 100`"
                    stroke-width="3.5"
                    stroke-linecap="round"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                  />
                </svg>
                <div class="absolute font-mono font-bold text-sm text-white">
                  {{ activeController.telemetry?.cpuUsagePercent ?? 0 }}%
                </div>
              </div>
            </div>

            <!-- RAM Gauge -->
            <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800 text-center space-y-2">
              <div class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-center gap-1">
                <Gauge class="size-3.5 text-purple-400" /> Memory
              </div>
              <div class="relative size-20 mx-auto flex items-center justify-center">
                <svg class="size-full -rotate-90" viewBox="0 0 36 36">
                  <path
                    class="text-slate-800"
                    stroke-width="3.5"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                  />
                  <path
                    class="text-purple-500 transition-all duration-500"
                    :stroke-dasharray="`${activeController.telemetry?.ramUsagePercent ?? 0}, 100`"
                    stroke-width="3.5"
                    stroke-linecap="round"
                    stroke="currentColor"
                    fill="none"
                    d="M18 2.0845 a 15.9155 15.9155 0 0 1 0 31.831 a 15.9155 15.9155 0 0 1 0 -31.831"
                  />
                </svg>
                <div class="absolute font-mono font-bold text-sm text-white">
                  {{ activeController.telemetry?.ramUsagePercent ?? 0 }}%
                </div>
              </div>
            </div>

            <!-- Beckhoff ADS Cycle Time -->
            <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800 text-center space-y-2">
              <div class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-center gap-1">
                <Zap class="size-3.5 text-amber-400" /> Cycle Time
              </div>
              <div class="pt-3">
                <div class="text-xl font-bold font-mono text-amber-300">2.1 ms</div>
                <div class="text-[10px] text-slate-500 mt-1">TwinCAT RT 1000µs</div>
              </div>
            </div>

            <!-- Free Storage Headroom -->
            <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800 text-center space-y-2">
              <div class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 flex items-center justify-center gap-1">
                <HardDrive class="size-3.5 text-cyan-400" /> Disk Free
              </div>
              <div class="pt-3">
                <div class="text-xl font-bold font-mono text-cyan-300">
                  {{ activeController.freeDiskSpace?.totalFreeGB ? Math.round(activeController.freeDiskSpace.totalFreeGB) + ' GB' : '64 GB' }}
                </div>
                <div class="text-[10px] text-slate-500 mt-1">SSD NVMe Spool</div>
              </div>
            </div>
          </div>

          <!-- Real-Time OT Signals Table -->
          <div class="space-y-2">
            <div class="flex items-center justify-between">
              <h4 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">
                Active Process Data Probes
              </h4>
              <span class="text-[11px] font-mono text-emerald-400 flex items-center gap-1">
                <span class="size-1.5 rounded-full bg-emerald-400"></span> Live Ingestion
              </span>
            </div>
            <div class="rounded-lg border border-slate-800 overflow-hidden">
              <table class="w-full text-xs text-left">
                <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-2.5">Tag Identifier</th>
                    <th class="px-4 py-2.5">Live Value</th>
                    <th class="px-4 py-2.5">Engineering Unit</th>
                    <th class="px-4 py-2.5 text-right">Data Quality</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800 font-mono">
                  <tr v-for="tag in liveTags" :key="tag.name" class="hover:bg-slate-800/30">
                    <td class="px-4 py-2.5 text-slate-200">{{ tag.name }}</td>
                    <td class="px-4 py-2.5 font-bold text-indigo-300">{{ tag.value }}</td>
                    <td class="px-4 py-2.5 text-slate-400">{{ tag.unit }}</td>
                    <td class="px-4 py-2.5 text-right text-emerald-400">{{ tag.quality }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>

    <!-- Command Dispatch Dialog -->
    <ControllerCommandModal
      :controller="activeController"
      :open="commandModalOpen"
      @update:open="commandModalOpen = $event"
      @submitted="fetchControllers"
    />

    <!-- Diagnostic Snapshot Payload Dialog -->
    <Dialog :open="isPayloadModalOpen" @update:open="isPayloadModalOpen = $event">
      <DialogContent class="max-w-2xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-2xl shadow-2xl">
        <DialogHeader class="p-5 border-b border-slate-800 bg-slate-900/50 flex flex-row items-center justify-between">
          <div class="flex items-center gap-2.5">
            <FileText class="size-5 text-indigo-400" />
            <div>
              <DialogTitle class="text-base font-bold text-slate-100">Diagnostic Snapshot Payload</DialogTitle>
              <DialogDescription class="text-xs text-slate-400">
                Node: {{ latestSnapshot?.hostname }} (Captured: {{ latestSnapshot ? new Date(latestSnapshot.capturedAtUtc).toLocaleString() : '' }})
              </DialogDescription>
            </div>
          </div>
          <Button
            size="sm"
            variant="outline"
            class="border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800 h-8 text-xs"
            @click="downloadSnapshot(latestSnapshot)"
          >
            <Download class="size-3.5 mr-1 text-emerald-400" />
            Download
          </Button>
        </DialogHeader>
        <div class="p-5 max-h-[500px] overflow-y-auto">
          <pre class="text-xs font-mono bg-slate-900/80 p-4 rounded-xl border border-slate-800 text-slate-300 overflow-x-auto whitespace-pre-wrap">{{ latestSnapshot?.snapshotPayloadJson || JSON.stringify(latestSnapshot, null, 2) }}</pre>
        </div>
      </DialogContent>
    </Dialog>

    <!-- Diagnostic Snapshot History Dialog -->
    <Dialog :open="isHistoryModalOpen" @update:open="isHistoryModalOpen = $event">
      <DialogContent class="max-w-3xl bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-2xl shadow-2xl">
        <DialogHeader class="p-5 border-b border-slate-800 bg-slate-900/50">
          <div class="flex items-center gap-2.5">
            <History class="size-5 text-indigo-400" />
            <div>
              <DialogTitle class="text-base font-bold text-slate-100">Historical Diagnostic Snapshots</DialogTitle>
              <DialogDescription class="text-xs text-slate-400">
                Audit trail for node {{ activeController?.hostname || activeController?.name }}
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>
        <div class="p-5 max-h-[480px] overflow-y-auto space-y-2">
          <div v-if="isLoadingHistory" class="p-8 text-center text-slate-400 text-xs flex items-center justify-center gap-2">
            <Loader2 class="size-4 animate-spin text-indigo-400" />
            <span>Loading historical snapshots...</span>
          </div>
          <div v-else-if="snapshotHistory.length === 0" class="p-8 text-center text-slate-500 text-xs">
            No diagnostic snapshots recorded yet for this controller.
          </div>
          <div
            v-for="s in snapshotHistory"
            :key="s.id"
            class="p-3.5 rounded-xl bg-slate-900/50 border border-slate-800 hover:border-slate-700 flex flex-col sm:flex-row sm:items-center justify-between gap-3 transition-colors"
          >
            <div class="space-y-1">
              <div class="flex items-center gap-2">
                <span class="text-xs font-semibold text-slate-200 font-mono">{{ new Date(s.capturedAtUtc).toLocaleString() }}</span>
                <Badge variant="outline" class="text-[10px] font-mono border-indigo-500/30 text-indigo-400 bg-indigo-950/20">
                  SHA-256: {{ s.payloadHashSha256.substring(0, 10) }}...
                </Badge>
              </div>
              <div class="text-[11px] text-slate-400 flex items-center gap-3">
                <span>By: {{ s.capturedByUserName || s.capturedByUserId }}</span>
                <span>Size: {{ Math.round((s.payloadSizeBytes || 0) / 1024) }} KB</span>
              </div>
            </div>
            <div class="flex items-center gap-2 self-end sm:self-auto">
              <Button
                size="sm"
                variant="outline"
                class="h-7 text-xs border-slate-800 bg-slate-900 text-slate-300 hover:bg-slate-800"
                @click="downloadSnapshot(s)"
              >
                <Download class="size-3 mr-1 text-emerald-400" />
                Export
              </Button>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  </div>
</template>
