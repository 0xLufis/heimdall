<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import {
  Tv, Play, Pause, Maximize, Minimize, AlertOctagon,
  Clock, Flame, ShieldAlert, Cpu, ArrowLeft, RefreshCw, BarChart3,
  CheckCircle2, Users, AlertTriangle, Layers
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'
import { useMaintenance } from '~/composables/useMaintenance'
import type { MaintenanceTicket } from '~/types/maintenance'

definePageMeta({
  layout: 'empty' // Fullscreen kiosk-ready layout
})

const { tickets, metrics, fetchTickets, isLoading } = useMaintenance()

// Carousel View Modes
type MonitorView = 'stoppages' | 'queue' | 'analytics'
const views: MonitorView[] = ['stoppages', 'queue', 'analytics']
const currentViewIndex = ref(0)
const currentView = computed(() => views[currentViewIndex.value])

const isAutoRotating = ref(true)
const ROTATION_SECONDS = 20
const secondsRemaining = ref(ROTATION_SECONDS)
let timerInterval: any = null

function nextView() {
  currentViewIndex.value = (currentViewIndex.value + 1) % views.length
  secondsRemaining.value = ROTATION_SECONDS
}

function prevView() {
  currentViewIndex.value = (currentViewIndex.value - 1 + views.length) % views.length
  secondsRemaining.value = ROTATION_SECONDS
}

function toggleAutoRotate() {
  isAutoRotating.value = !isAutoRotating.value
  if (isAutoRotating.value) {
    secondsRemaining.value = ROTATION_SECONDS
  }
}

// Fullscreen toggle
const isFullscreen = ref(false)
function toggleFullscreen() {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen().then(() => {
      isFullscreen.value = true
    }).catch(() => {})
  } else {
    document.exitFullscreen().then(() => {
      isFullscreen.value = false
    }).catch(() => {})
  }
}

// Stoppage statistics
const lineStopTickets = computed(() => {
  return tickets.value.filter(t => t.isLineStop && t.status !== 'Resolved' && t.status !== 'Closed')
})

const criticalTickets = computed(() => {
  return tickets.value.filter(t => (t.priority === 'Critical' || t.priority === 'High') && t.status !== 'Resolved' && t.status !== 'Closed')
})

const openTickets = computed(() => {
  return tickets.value.filter(t => t.status === 'Open' || t.status === 'InProgress' || t.status === 'Pending')
})

// Machine downtime aggregation
const worstMachines = computed(() => {
  const machineMap = new Map<string, { name: string; minutes: number; count: number }>()
  for (const t of tickets.value) {
    const key = t.stationName || t.stationId || 'Unknown Machine'
    const existing = machineMap.get(key) || { name: key, minutes: 0, count: 0 }
    existing.count += 1
    if (t.isLineStop) {
      existing.minutes += t.lineStopDurationMinutes || 35
    }
    machineMap.set(key, existing)
  }
  return Array.from(machineMap.values())
    .sort((a, b) => b.minutes - a.minutes || b.count - a.count)
    .slice(0, 5)
})

// Department stoppage aggregation
const departmentBreakdown = computed(() => {
  const deptMap = new Map<string, { name: string; minutes: number; count: number }>()
  for (const t of tickets.value) {
    const dept = t.responsibleDepartment || 'General'
    const existing = deptMap.get(dept) || { name: dept, minutes: 0, count: 0 }
    existing.count += 1
    if (t.isLineStop) {
      existing.minutes += t.lineStopDurationMinutes || 25
    }
    deptMap.set(dept, existing)
  }
  return Array.from(deptMap.values()).sort((a, b) => b.minutes - a.minutes)
})

// Current live clock. Empty until mounted: a time rendered on the server never matches the
// client's a moment later, which made every load of this page a hydration mismatch.
const currentTime = ref('')
let clockInterval: any = null

onMounted(async () => {
  currentTime.value = new Date().toLocaleTimeString()
  await fetchTickets()
  clockInterval = setInterval(() => {
    currentTime.value = new Date().toLocaleTimeString()
  }, 1000)

  timerInterval = setInterval(() => {
    if (isAutoRotating.value) {
      if (secondsRemaining.value > 1) {
        secondsRemaining.value -= 1
      } else {
        nextView()
        fetchTickets() // Refresh data on view flip
      }
    }
  }, 1000)
})

onUnmounted(() => {
  clearInterval(timerInterval)
  clearInterval(clockInterval)
})
</script>

<template>
  <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col justify-between p-6 select-none font-sans overflow-hidden">
    <!-- Top TV Header Bar -->
    <header class="flex items-center justify-between pb-4 border-b border-slate-800">
      <div class="flex items-center gap-4">
        <NuxtLink to="/dashboard/tickets">
          <Button variant="ghost" size="icon" class="rounded-xl h-10 w-10 text-slate-400 hover:text-white hover:bg-slate-900">
            <ArrowLeft class="size-5" />
          </Button>
        </NuxtLink>
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-xl bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
            <Tv class="size-6" />
          </div>
          <div>
            <h1 class="text-2xl font-black tracking-wider uppercase text-white flex items-center gap-3">
              <span>Plant Floor Incident Monitor</span>
              <Badge v-if="lineStopTickets.length > 0" class="bg-rose-600 text-white font-bold animate-pulse px-3 py-0.5 text-xs">
                {{ lineStopTickets.length }} LINE STOP{{ lineStopTickets.length > 1 ? 'S' : '' }}
              </Badge>
            </h1>
            <p class="text-xs text-slate-400 font-mono">
              Live shopfloor display &bull; Industrial OT Telemetry Synchronization
            </p>
          </div>
        </div>
      </div>

      <!-- Controls & Carousel Indicators -->
      <div class="flex items-center gap-4">
        <!-- View selector pills -->
        <div class="flex items-center gap-1 bg-slate-900 border border-slate-800 p-1 rounded-xl">
          <button
            v-for="(v, idx) in views"
            :key="v"
            @click="currentViewIndex = idx; secondsRemaining = ROTATION_SECONDS"
            class="px-3 py-1 text-xs font-semibold uppercase tracking-wider rounded-lg transition-colors"
            :class="currentViewIndex === idx ? 'bg-indigo-600 text-white shadow-sm' : 'text-slate-400 hover:text-white'"
          >
            {{ v === 'stoppages' ? 'Line Stops' : v === 'queue' ? 'Incident Queue' : 'Analytics' }}
          </button>
        </div>

        <!-- Carousel Play / Pause & Timer -->
        <div class="flex items-center gap-2 bg-slate-900 border border-slate-800 px-3 py-1.5 rounded-xl font-mono text-xs">
          <button @click="toggleAutoRotate" class="text-slate-300 hover:text-white">
            <Pause v-if="isAutoRotating" class="size-4" />
            <Play v-else class="size-4 text-emerald-400" />
          </button>
          <span class="text-slate-400">{{ secondsRemaining }}s</span>
        </div>

        <!-- Fullscreen -->
        <Button variant="ghost" size="icon" @click="toggleFullscreen" class="rounded-xl h-10 w-10 text-slate-300 hover:text-white hover:bg-slate-900">
          <Minimize v-if="isFullscreen" class="size-5" />
          <Maximize v-else class="size-5" />
        </Button>

        <!-- Big Digital Clock -->
        <div class="font-mono text-2xl font-bold tracking-tight text-indigo-400 pl-2">
          {{ currentTime }}
        </div>
      </div>
    </header>

    <!-- Main Dynamic Monitor Content (Rotates) -->
    <main class="flex-1 my-6 overflow-hidden flex flex-col justify-center">
      <!-- VIEW 1: ACTIVE LINE STOPPAGES & CRITICAL ALERTS -->
      <div v-if="currentView === 'stoppages'" class="space-y-6 animate-in fade-in zoom-in-95 duration-300">
        <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
          <!-- Critical Metric Cards -->
          <div class="p-6 rounded-2xl bg-rose-950/40 border-2 border-rose-600/50 flex items-center justify-between">
            <div>
              <p class="text-xs uppercase font-mono text-rose-400 font-bold tracking-wider">Active Line Stoppages</p>
              <h2 class="text-5xl font-black text-white mt-1">{{ lineStopTickets.length }}</h2>
            </div>
            <AlertOctagon class="size-16 text-rose-500/40" />
          </div>

          <div class="p-6 rounded-2xl bg-amber-950/40 border-2 border-amber-600/50 flex items-center justify-between">
            <div>
              <p class="text-xs uppercase font-mono text-amber-400 font-bold tracking-wider">Critical & High Priority</p>
              <h2 class="text-5xl font-black text-white mt-1">{{ criticalTickets.length }}</h2>
            </div>
            <Flame class="size-16 text-amber-500/40" />
          </div>

          <div class="p-6 rounded-2xl bg-indigo-950/40 border-2 border-indigo-600/50 flex items-center justify-between">
            <div>
              <p class="text-xs uppercase font-mono text-indigo-400 font-bold tracking-wider">Total Open Incidents</p>
              <h2 class="text-5xl font-black text-white mt-1">{{ openTickets.length }}</h2>
            </div>
            <Clock class="size-16 text-indigo-500/40" />
          </div>
        </div>

        <!-- Line Stoppage Alert Banner Cards -->
        <div class="space-y-4">
          <div v-if="lineStopTickets.length === 0" class="p-12 text-center rounded-2xl bg-slate-900 border border-slate-800">
            <CheckCircle2 class="size-16 text-emerald-500 mx-auto mb-3" />
            <h3 class="text-xl font-bold text-white">All Production Lines Operating Smoothly</h3>
            <p class="text-slate-400 text-sm mt-1">No active line stop incidents reported across manufacturing cells.</p>
          </div>

          <div
            v-for="tkt in lineStopTickets"
            :key="tkt.id"
            v-ctx:ticket="tkt"
            class="p-6 rounded-2xl bg-rose-950/30 border-2 border-rose-600 flex items-center justify-between gap-6"
          >
            <div class="space-y-2 flex-1">
              <div class="flex items-center gap-3">
                <span class="px-2.5 py-1 rounded bg-rose-600 text-white font-mono font-black text-sm">
                  LINE STOP
                </span>
                <span class="font-mono text-base font-bold text-slate-300">{{ tkt.ticketNumber }}</span>
                <Badge variant="outline" class="border-rose-500/50 text-rose-400 uppercase font-mono text-xs">
                  {{ tkt.stationName || tkt.stationId || 'Line Asset' }}
                </Badge>
              </div>
              <h3 class="text-2xl font-black text-white">{{ tkt.title }}</h3>
              <p class="text-sm text-slate-400 line-clamp-1">{{ tkt.description }}</p>
            </div>

            <div class="text-right space-y-1">
              <div class="text-xs font-mono text-slate-400">Assigned Technician</div>
              <div class="text-lg font-bold text-slate-100 flex items-center gap-2 justify-end">
                <Users class="size-4 text-indigo-400" />
                <span>{{ tkt.assignedTechnicianName || 'Unassigned / Floor Response' }}</span>
              </div>
              <div class="text-xs font-mono text-rose-400 font-bold">
                Duration: {{ tkt.lineStopDurationMinutes || 15 }} minutes
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- VIEW 2: ACTIVE INCIDENTS QUEUE (LIVE FLOOR ANDON) -->
      <div v-else-if="currentView === 'queue'" class="space-y-4 animate-in fade-in zoom-in-95 duration-300">
        <div class="flex items-center justify-between pb-2 border-b border-slate-800">
          <h2 class="text-lg font-bold uppercase tracking-wider text-slate-300 flex items-center gap-2">
            <Clock class="size-5 text-indigo-400" />
            <span>Active Incident Response Queue</span>
          </h2>
          <span class="text-xs font-mono text-slate-400">{{ openTickets.length }} active issues</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          <div
            v-for="tkt in openTickets.slice(0, 9)"
            :key="tkt.id"
            v-ctx:ticket="tkt"
            class="p-4 rounded-xl border bg-slate-900/80 transition-all flex flex-col justify-between"
            :class="tkt.isLineStop ? 'border-rose-600 bg-rose-950/20' : tkt.priority === 'Critical' ? 'border-rose-500/60' : tkt.priority === 'High' ? 'border-amber-500/60' : 'border-slate-800'"
          >
            <div>
              <div class="flex items-center justify-between gap-2 pb-2">
                <span class="font-mono text-xs font-bold text-slate-400">{{ tkt.ticketNumber }}</span>
                <Badge
                  class="text-[10px] uppercase font-bold"
                  :class="tkt.priority === 'Critical' ? 'bg-rose-600 text-white' : tkt.priority === 'High' ? 'bg-amber-600 text-white' : 'bg-slate-700 text-slate-300'"
                >
                  {{ tkt.priority }}
                </Badge>
              </div>
              <h4 class="text-base font-bold text-white line-clamp-1">{{ tkt.title }}</h4>
              <p class="text-xs text-slate-400 mt-1 line-clamp-2">{{ tkt.description || 'No additional details provided.' }}</p>
            </div>

            <div class="pt-3 mt-3 border-t border-slate-800/80 flex items-center justify-between text-xs font-mono text-slate-400">
              <span class="truncate max-w-[150px]">{{ tkt.stationName || tkt.stationId || 'General' }}</span>
              <span class="text-indigo-400">{{ tkt.assignedTechnicianName || 'Unassigned' }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- VIEW 3: ANALYTICS & TOP DOWNTIME MACHINES -->
      <div v-else-if="currentView === 'analytics'" class="space-y-6 animate-in fade-in zoom-in-95 duration-300">
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <!-- Top Downtime Machines -->
          <div class="p-6 rounded-2xl bg-slate-900 border border-slate-800 space-y-4">
            <h3 class="text-base font-bold uppercase tracking-wider text-slate-200 flex items-center gap-2">
              <AlertTriangle class="size-5 text-amber-400" />
              <span>Top Downtime Production Machines (Week)</span>
            </h3>
            <div class="space-y-3">
              <div
                v-for="(mach, idx) in worstMachines"
                :key="mach.name"
                class="p-3 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between"
              >
                <div class="flex items-center gap-3">
                  <span class="font-mono font-bold text-indigo-400 text-sm">#{{ idx + 1 }}</span>
                  <div>
                    <div class="text-sm font-bold text-white">{{ mach.name }}</div>
                    <div class="text-xs text-slate-400">{{ mach.count }} total incidents logged</div>
                  </div>
                </div>
                <div class="font-mono text-right">
                  <div class="text-base font-black text-rose-400">{{ mach.minutes }} min</div>
                  <div class="text-[10px] text-slate-500 uppercase">Stoppage</div>
                </div>
              </div>
            </div>
          </div>

          <!-- Department Stoppage Breakdown -->
          <div class="p-6 rounded-2xl bg-slate-900 border border-slate-800 space-y-4">
            <h3 class="text-base font-bold uppercase tracking-wider text-slate-200 flex items-center gap-2">
              <BarChart3 class="size-5 text-indigo-400" />
              <span>Department Stoppage Share</span>
            </h3>
            <div class="space-y-3">
              <div
                v-for="dept in departmentBreakdown"
                :key="dept.name"
                class="p-3 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between"
              >
                <div>
                  <div class="text-sm font-bold text-white">{{ dept.name }}</div>
                  <div class="text-xs text-slate-400">{{ dept.count }} incidents</div>
                </div>
                <div class="font-mono text-right">
                  <div class="text-base font-black text-indigo-400">{{ dept.minutes }} min</div>
                  <div class="text-[10px] text-slate-500 uppercase">Total downtime</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>

    <!-- Bottom Status Bar -->
    <footer class="pt-3 border-t border-slate-800 flex items-center justify-between text-xs text-slate-400 font-mono">
      <div class="flex items-center gap-4">
        <span>HEIMDALL TELEMETRY EDGE &bull; V1.4.2</span>
        <span class="text-emerald-400 flex items-center gap-1">
          <span class="size-2 rounded-full bg-emerald-400 inline-block animate-ping"></span>
          REALTIME SIGNALR CONNECTED
        </span>
      </div>
      <div>
        <span>AUTO-ROTATION: {{ isAutoRotating ? 'ON' : 'OFF' }} ({{ currentView.toUpperCase() }})</span>
      </div>
    </footer>
  </div>
</template>
