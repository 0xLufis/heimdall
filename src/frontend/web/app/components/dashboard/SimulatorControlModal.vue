<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue'
import {
  Activity,
  Flame,
  X,
  CheckCircle2,
  AlertTriangle,
  Sparkles,
  ToggleLeft,
  ToggleRight,
  Plus,
  Factory,
  Gauge,
  WifiOff
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'

interface SimStation {
  id: string
  name: string
  handle?: string | null
  line?: string | null
  machineType?: string | null
  controller: string
  state: 'RUNNING' | 'IDLE' | 'WARNING' | 'FAULTED' | 'MAINTENANCE'
  reason?: string
  scenario?: string | null
  driftProgress?: number | null
  values: Record<string, number | string | undefined>
}

interface SimStatus {
  sim_offline?: boolean
  error?: string
  active_fleet: number
  active_stations?: number
  total_dispatched: number
  total_errors: number
  fault_rate: number
  speed?: number
  dataset?: string
  transport?: { protocol: string; broker: string | null; connected: boolean; error?: string | null }
  lines?: string[]
  drift_profiles?: string[]
  scenarios?: string[]
  nodes: Array<{ hostname: string; profile: string; faulty?: boolean }>
  stations?: SimStation[]
}

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'ticketCreated'): void
}>()

const status = ref<SimStatus | null>(null)
const loading = ref(false)
const actionMessage = ref<string | null>(null)
const actionFailed = ref(false)
const selectedNode = ref<string>('')
const selectedStation = ref<string>('')
const selectedLine = ref<string>('')
const driftProfile = ref<string>('laser_drift')
const driftMinutes = ref<number>(10)
const ticketSettings = ref<{ autoAssignTickets: boolean; devTicketGenEnabled: boolean }>({
  autoAssignTickets: false,
  devTicketGenEnabled: true
})
const ticketCount = ref<number>(0)
let timer: any = null

const SPEEDS = [1, 5, 10, 30, 60]
const DRIFT_LABELS: Record<string, string> = {
  laser_drift: 'Laser power drift (glass heats up)',
  bearing_wear: 'Bearing wear (vibration)',
  thermal: 'Overheating',
  cycle_slowdown: 'Cycle time slowdown'
}

// Until the first status arrives the simulator state is unknown, which is treated like offline.
const simOffline = computed(() => !status.value || status.value.sim_offline === true)
const brokerDown = computed(() => !simOffline.value && status.value?.transport?.connected === false)
const stations = computed<SimStation[]>(() => status.value?.stations || [])
const lines = computed<string[]>(() => status.value?.lines || [])
const driftProfiles = computed<string[]>(() =>
  status.value?.drift_profiles?.length ? status.value.drift_profiles : Object.keys(DRIFT_LABELS)
)
const stationCounts = computed(() => {
  const counts: Record<string, number> = { RUNNING: 0, IDLE: 0, WARNING: 0, FAULTED: 0, MAINTENANCE: 0 }
  for (const st of stations.value) counts[st.state] = (counts[st.state] || 0) + 1
  return counts
})
const activeScenarioStations = computed(() =>
  stations.value.filter(st => st.scenario || st.state === 'FAULTED' || st.state === 'WARNING' || st.state === 'MAINTENANCE')
)

const stateClass = (state: string) => {
  switch (state) {
    case 'RUNNING': return 'border-emerald-500/30 text-emerald-700 dark:text-emerald-400 bg-emerald-500/10'
    case 'WARNING': return 'border-amber-500/30 text-amber-700 dark:text-amber-400 bg-amber-500/10'
    case 'FAULTED': return 'border-rose-500/30 text-rose-700 dark:text-rose-400 bg-rose-500/10'
    case 'MAINTENANCE': return 'border-sky-500/30 text-sky-700 dark:text-sky-400 bg-sky-500/10'
    default: return 'border-border text-muted-foreground bg-muted/40'
  }
}

const fmt = (value: unknown, digits = 1) =>
  typeof value === 'number' && Number.isFinite(value) ? value.toFixed(digits) : '–'

const errorText = (err: any, fallback: string) =>
  err?.data?.statusMessage || err?.data?.message || err?.statusMessage || err?.message || fallback

async function fetchStatus() {
  try {
    status.value = await $fetch<SimStatus>('/api/simulator/status')
  } catch (err: any) {
    // 403 (simulation disabled) or BFF failure: show it as offline instead of stale numbers.
    status.value = {
      sim_offline: true,
      error: errorText(err, 'Simulator status unavailable'),
      active_fleet: 0,
      total_dispatched: 0,
      total_errors: 0,
      fault_rate: 0,
      nodes: []
    }
  }

  try {
    const settingsRes = await $fetch<any>('/api/tickets/settings')
    if (settingsRes && settingsRes.settings) {
      ticketSettings.value = settingsRes.settings
    }
  } catch {}

  try {
    const tktsRes = await $fetch<any>('/api/tickets')
    if (tktsRes && Array.isArray(tktsRes.tickets)) {
      ticketCount.value = tktsRes.tickets.length
    }
  } catch {}
}

/** Runs one simulator command, reports the outcome and refreshes the status. */
async function simAction(action: string, body: Record<string, unknown>, fallbackMessage: string) {
  loading.value = true
  actionMessage.value = null
  actionFailed.value = false
  try {
    const res = await $fetch<any>(`/api/simulator/${action}`, { method: 'POST', body })
    actionMessage.value = res?.message || fallbackMessage
    await fetchStatus()
  } catch (err: any) {
    actionFailed.value = true
    actionMessage.value = errorText(err, 'Simulator command failed')
  } finally {
    loading.value = false
  }
}

const injectFault = () =>
  simAction('fault', { hostname: selectedNode.value || undefined }, 'Fault injected successfully!')

const clearFaults = () => simAction('clear-faults', {}, 'All faults cleared across fleet!')

const stationFault = () =>
  selectedStation.value && simAction('station/fault', { station: selectedStation.value }, 'Station fault injected')

const stationMaintenance = () =>
  selectedStation.value && simAction('station/maintenance', { station: selectedStation.value }, 'Station set to maintenance')

const stationRecover = (station = selectedStation.value) =>
  station && simAction('recover', { station }, 'Station recovered')

const startDrift = () =>
  selectedStation.value && simAction('station/drift', {
    station: selectedStation.value,
    profile: driftProfile.value,
    duration_s: Math.max(1, Number(driftMinutes.value) || 10) * 60
  }, 'Drift started')

const stopLine = () => selectedLine.value && simAction('line/stop', { line: selectedLine.value }, 'Line stopped')

const recoverLine = () => selectedLine.value && simAction('recover', { line: selectedLine.value }, 'Line recovered')

const setSpeed = (speed: number) => simAction('speed', { speed }, `Simulation speed ${speed}x`)

const runScenario = (name: string) => simAction('scenario', { name }, 'Scenario applied')

async function toggleAutoAssign() {
  const newVal = !ticketSettings.value.autoAssignTickets
  try {
    await $fetch('/api/tickets/settings', {
      method: 'POST',
      body: { autoAssignTickets: newVal }
    })
    ticketSettings.value.autoAssignTickets = newVal
    actionFailed.value = false
    actionMessage.value = `Auto-Assign Tickets set to ${newVal ? 'ON' : 'OFF'}`
  } catch (err: any) {
    actionFailed.value = true
    actionMessage.value = errorText(err, 'Could not change ticket assignment setting')
  }
}

async function toggleDevGen() {
  const newVal = !ticketSettings.value.devTicketGenEnabled
  try {
    await $fetch('/api/tickets/settings', {
      method: 'POST',
      body: { devTicketGenEnabled: newVal }
    })
    ticketSettings.value.devTicketGenEnabled = newVal
    actionFailed.value = false
    actionMessage.value = `Dev 40s Ticket Generator ${newVal ? 'Enabled' : 'Paused'}`
  } catch (err: any) {
    actionFailed.value = true
    actionMessage.value = errorText(err, 'Could not change ticket generator setting')
  }
}

async function generateInstantDevTicket() {
  loading.value = true
  actionMessage.value = null
  actionFailed.value = false
  const target = selectedStation.value || selectedNode.value || stations.value[0]?.id || 'ROBOT-CELL-01'
  try {
    const res = await $fetch<any>('/api/tickets', {
      method: 'POST',
      body: {
        title: 'Manual Simulator Anomaly Triggered',
        description: 'Simulated high-temperature excursion triggered via interactive fleet control panel.',
        priority: 'High',
        stationId: target,
        stationName: `Station ${target}`
      }
    })
    actionMessage.value = `Dev ticket generated: ${res.ticket?.ticketNumber || 'OK'}`
    await fetchStatus()
    emit('ticketCreated')
  } catch (err: any) {
    actionFailed.value = true
    actionMessage.value = errorText(err, 'Error creating dev ticket.')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchStatus()
  timer = setInterval(fetchStatus, 5000)
})

onUnmounted(() => {
  if (timer) clearInterval(timer)
})

function close() {
  emit('update:open', false)
}
</script>

<template>
  <div v-if="open" role="dialog" class="fixed inset-y-0 right-0 z-40 w-full max-w-xl bg-card border-l border-border shadow-2xl flex flex-col animate-in slide-in-from-right duration-200">
    <!-- Header -->
    <div class="p-5 border-b border-border flex items-center justify-between bg-muted/40 shrink-0">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-2xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20">
            <Activity class="h-5 w-5" />
          </div>
          <div>
            <h3 class="text-base font-black uppercase tracking-tight text-foreground flex items-center gap-2">
              Interactive Fleet Simulator & Dev Generator
            </h3>
            <p class="text-[10px] font-bold text-muted-foreground uppercase tracking-widest mt-0.5">
              Live Edge Anomaly Injection, MQTT Station Telemetry & Dev Ticketing
            </p>
          </div>
        </div>

        <Button variant="ghost" size="icon" @click="close" class="text-muted-foreground hover:text-foreground rounded-xl">
          <X class="h-5 w-5" />
        </Button>
      </div>

      <!-- Action Message Alert -->
      <div
        v-if="actionMessage"
        role="status"
        class="mx-6 mt-4 p-3 border rounded-xl flex items-center gap-2.5 text-xs"
        :class="actionFailed
          ? 'bg-rose-500/10 border-rose-500/30 text-rose-700 dark:text-rose-300'
          : 'bg-indigo-500/10 border-indigo-500/30 text-indigo-700 dark:text-indigo-300'"
      >
        <AlertTriangle v-if="actionFailed" class="h-4 w-4 shrink-0" />
        <CheckCircle2 v-else class="h-4 w-4 shrink-0 text-indigo-600 dark:text-indigo-400" />
        <span>{{ actionMessage }}</span>
      </div>

      <!-- Body -->
      <div class="p-6 space-y-6 flex-1 overflow-y-auto">

        <!-- Simulator offline / broker down -->
        <div
          v-if="simOffline"
          data-testid="sim-offline"
          class="p-4 bg-amber-500/10 border border-amber-500/30 rounded-2xl flex items-start gap-3 text-xs text-amber-800 dark:text-amber-300"
        >
          <WifiOff class="h-4 w-4 shrink-0 mt-0.5" />
          <div class="space-y-1">
            <div class="font-black uppercase tracking-wider">Fleet simulator offline</div>
            <p>
              No simulator is answering, so no simulated telemetry is being sent and the scenario controls are disabled.
              Start it with <code class="font-mono">just start simulator</code> (enterprise dataset) or
              <code class="font-mono">just sim-demo</code> (demo plant).
            </p>
            <p v-if="status?.error" class="font-mono text-[10px] opacity-80 break-all">{{ status.error }}</p>
          </div>
        </div>
        <div
          v-else-if="brokerDown"
          data-testid="sim-broker-down"
          class="p-4 bg-amber-500/10 border border-amber-500/30 rounded-2xl flex items-start gap-3 text-xs text-amber-800 dark:text-amber-300"
        >
          <AlertTriangle class="h-4 w-4 shrink-0 mt-0.5" />
          <div class="space-y-1">
            <div class="font-black uppercase tracking-wider">Simulator cannot reach the MQTT broker</div>
            <p>
              The simulator runs, but its telemetry is not delivered ({{ status?.transport?.broker }}).
              Start the backend; the simulator reconnects by itself.
            </p>
            <p v-if="status?.transport?.error" class="font-mono text-[10px] opacity-80 break-all">{{ status.transport.error }}</p>
          </div>
        </div>

        <!-- Live Fleet Telemetry Stats -->
        <div>
          <div class="text-[10px] font-black uppercase tracking-widest text-muted-foreground mb-2.5 flex items-center justify-between">
            <span>
              Fleet Simulator Status
              <span v-if="status?.dataset" class="font-mono normal-case tracking-normal">· {{ status.dataset }} dataset</span>
            </span>
            <span v-if="simOffline" class="text-muted-foreground flex items-center gap-1.5">
              <span class="h-2 w-2 rounded-full bg-muted-foreground/50"></span>
              Offline
            </span>
            <span v-else-if="brokerDown" class="text-amber-600 dark:text-amber-400 flex items-center gap-1.5">
              <span class="h-2 w-2 rounded-full bg-amber-500"></span>
              Broker unreachable
            </span>
            <span v-else class="text-emerald-600 dark:text-emerald-400 flex items-center gap-1.5">
              <span class="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span>
              Streaming Active
            </span>
          </div>

          <div class="grid grid-cols-4 gap-3 text-center">
            <div class="p-3 bg-muted/30 rounded-2xl border border-border">
              <span class="text-[10px] uppercase font-bold text-muted-foreground block">IPC Nodes</span>
              <span class="text-lg font-black text-foreground font-mono mt-0.5 block">
                {{ simOffline ? '–' : status?.active_fleet }}
              </span>
            </div>
            <div class="p-3 bg-muted/30 rounded-2xl border border-border">
              <span class="text-[10px] uppercase font-bold text-muted-foreground block">Stations</span>
              <span class="text-lg font-black text-foreground font-mono mt-0.5 block">
                {{ simOffline ? '–' : stations.length }}
              </span>
            </div>
            <div class="p-3 bg-muted/30 rounded-2xl border border-border">
              <span class="text-[10px] uppercase font-bold text-muted-foreground block">Dispatched</span>
              <span class="text-lg font-black text-indigo-600 dark:text-indigo-400 font-mono mt-0.5 block">
                {{ simOffline ? '–' : status?.total_dispatched }}
              </span>
            </div>
            <div class="p-3 bg-muted/30 rounded-2xl border border-border">
              <span class="text-[10px] uppercase font-bold text-muted-foreground block">Send Errors</span>
              <span class="text-lg font-black text-amber-600 dark:text-amber-400 font-mono mt-0.5 block">
                {{ simOffline ? '–' : status?.total_errors }}
              </span>
            </div>
          </div>

          <div v-if="!simOffline && stations.length" class="mt-3 flex flex-wrap gap-1.5">
            <Badge
              v-for="(count, state) in stationCounts"
              :key="state"
              variant="outline"
              class="text-[9px] font-mono"
              :class="count ? stateClass(String(state)) : 'border-border text-muted-foreground/60'"
            >
              {{ state }} {{ count }}
            </Badge>
          </div>
        </div>

        <!-- Station Process Scenarios -->
        <div class="p-4 bg-muted/30 rounded-2xl border border-border space-y-4">
          <div class="flex items-center justify-between">
            <span class="text-xs font-black uppercase tracking-wider text-foreground flex items-center gap-1.5">
              <Factory class="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
              Station Process Scenarios
            </span>
            <Badge variant="outline" class="text-[9px] font-mono border-indigo-500/30 text-indigo-700 dark:text-indigo-400">
              PLC telemetry
            </Badge>
          </div>

          <p v-if="!simOffline && !stations.length" class="text-[11px] text-muted-foreground">
            The simulator runs without station links (heartbeats only). Start it with a seeded dataset to simulate station telemetry.
          </p>

          <!-- Station target + actions -->
          <div class="space-y-2">
            <select
              v-model="selectedStation"
              aria-label="Station"
              :disabled="simOffline || !stations.length"
              class="w-full bg-background border border-border text-xs rounded-xl px-3 py-2 text-foreground focus:outline-none focus:border-indigo-500 disabled:opacity-50"
            >
              <option value="">Select station…</option>
              <option v-for="st in stations" :key="st.id" :value="st.id">
                {{ st.id }} – {{ st.machineType || 'Station' }} ({{ st.state }})
              </option>
            </select>

            <div class="flex flex-wrap gap-2">
              <Button
                size="sm"
                data-testid="sim-station-fault"
                @click="stationFault"
                :disabled="loading || simOffline || !selectedStation"
                class="bg-rose-600 hover:bg-rose-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-3"
              >
                Fault
              </Button>
              <Button
                size="sm"
                variant="outline"
                data-testid="sim-station-maintenance"
                @click="stationMaintenance"
                :disabled="loading || simOffline || !selectedStation"
                class="rounded-xl text-xs font-black uppercase tracking-wider px-3"
              >
                Maintenance
              </Button>
              <Button
                size="sm"
                variant="outline"
                data-testid="sim-station-recover"
                @click="stationRecover()"
                :disabled="loading || simOffline || !selectedStation"
                class="rounded-xl text-xs font-black uppercase tracking-wider px-3"
              >
                Recover
              </Button>
            </div>

            <div class="flex flex-wrap items-center gap-2">
              <select
                v-model="driftProfile"
                aria-label="Drift profile"
                :disabled="simOffline"
                class="flex-1 min-w-40 bg-background border border-border text-xs rounded-xl px-3 py-2 text-foreground focus:outline-none focus:border-indigo-500 disabled:opacity-50"
              >
                <option v-for="p in driftProfiles" :key="p" :value="p">{{ DRIFT_LABELS[p] || p }}</option>
              </select>
              <label class="flex items-center gap-1.5 text-[10px] text-muted-foreground">
                over
                <input
                  v-model.number="driftMinutes"
                  type="number"
                  min="1"
                  max="240"
                  aria-label="Drift duration in minutes"
                  :disabled="simOffline"
                  class="w-14 bg-background border border-border text-xs rounded-lg px-2 py-1.5 text-foreground font-mono focus:outline-none focus:border-indigo-500 disabled:opacity-50"
                />
                min
              </label>
              <Button
                size="sm"
                data-testid="sim-station-drift"
                @click="startDrift"
                :disabled="loading || simOffline || !selectedStation"
                class="bg-amber-600 hover:bg-amber-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-3"
              >
                Start Drift
              </Button>
            </div>
          </div>

          <!-- Line stop -->
          <div class="flex flex-wrap items-center gap-2 pt-3 border-t border-border">
            <select
              v-model="selectedLine"
              aria-label="Line"
              :disabled="simOffline || !lines.length"
              class="flex-1 min-w-32 bg-background border border-border text-xs rounded-xl px-3 py-2 text-foreground focus:outline-none focus:border-indigo-500 disabled:opacity-50"
            >
              <option value="">Select line…</option>
              <option v-for="line in lines" :key="line" :value="line">{{ line }}</option>
            </select>
            <Button
              size="sm"
              variant="outline"
              data-testid="sim-line-stop"
              @click="stopLine"
              :disabled="loading || simOffline || !selectedLine"
              class="rounded-xl text-xs font-black uppercase tracking-wider px-3"
            >
              Stop Line
            </Button>
            <Button
              size="sm"
              variant="outline"
              data-testid="sim-line-recover"
              @click="recoverLine"
              :disabled="loading || simOffline || !selectedLine"
              class="rounded-xl text-xs font-black uppercase tracking-wider px-3"
            >
              Restart Line
            </Button>
          </div>

          <!-- Presets + speed -->
          <div class="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-border">
            <div class="flex flex-wrap gap-2">
              <Button
                size="sm"
                data-testid="sim-scenario-demo"
                @click="runScenario('demo_story')"
                :disabled="loading || simOffline || !stations.length"
                class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-3"
                title="Laser power drift, spindle bearing wear and a slow dispenser"
              >
                Run Demo Story
              </Button>
              <Button
                size="sm"
                variant="ghost"
                data-testid="sim-recover-all"
                @click="runScenario('clear')"
                :disabled="loading || simOffline"
                class="text-[10px] text-muted-foreground hover:text-foreground"
              >
                Recover All Stations
              </Button>
            </div>

            <div class="flex items-center gap-1" role="group" aria-label="Simulation speed">
              <Gauge class="h-3.5 w-3.5 text-muted-foreground mr-1" />
              <button
                v-for="s in SPEEDS"
                :key="s"
                type="button"
                @click="setSpeed(s)"
                :disabled="loading || simOffline"
                :aria-pressed="status?.speed === s"
                class="px-2 py-1 rounded-lg border text-[10px] font-mono font-bold transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                :class="status?.speed === s
                  ? 'bg-indigo-600 border-indigo-600 text-white'
                  : 'border-border text-muted-foreground hover:text-foreground hover:bg-muted'"
              >
                {{ s }}x
              </button>
            </div>
          </div>

          <!-- Stations that are not simply running -->
          <div v-if="activeScenarioStations.length" class="pt-3 border-t border-border space-y-1.5">
            <div class="text-[10px] font-black uppercase tracking-widest text-muted-foreground">Active scenarios & alarms</div>
            <div
              v-for="st in activeScenarioStations"
              :key="st.id"
              class="flex items-center justify-between gap-2 p-2 bg-background rounded-xl border border-border text-[11px]"
            >
              <button
                type="button"
                @click="selectedStation = st.id"
                class="min-w-0 text-left cursor-pointer"
                :title="`Select ${st.id}`"
              >
                <div class="flex items-center gap-1.5">
                  <span class="font-mono font-bold text-foreground">{{ st.id }}</span>
                  <Badge variant="outline" class="text-[9px] font-mono px-1.5 py-0" :class="stateClass(st.state)">{{ st.state }}</Badge>
                </div>
                <div class="text-[10px] text-muted-foreground font-mono truncate">
                  {{ st.scenario || st.reason }}
                  <template v-if="st.driftProgress != null"> · {{ Math.round(st.driftProgress * 100) }}%</template>
                  · {{ fmt(st.values.power_kw, 2) }} kW · {{ fmt(st.values.temperature_c) }} °C · {{ fmt(st.values.vibration_mms, 2) }} mm/s
                </div>
              </button>
              <Button
                size="sm"
                variant="ghost"
                @click="stationRecover(st.id)"
                :disabled="loading"
                class="h-7 px-2 text-[10px] text-muted-foreground hover:text-foreground shrink-0"
              >
                Recover
              </Button>
            </div>
          </div>
        </div>

        <!-- Anomaly Injection Sandbox -->
        <div class="p-4 bg-muted/30 rounded-2xl border border-border space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-xs font-black uppercase tracking-wider text-foreground flex items-center gap-1.5">
              <Flame class="h-4 w-4 text-rose-600 dark:text-rose-400" />
              IPC Anomaly Injection
            </span>
            <Badge variant="outline" class="text-[9px] font-mono border-rose-500/30 text-rose-700 dark:text-rose-400">
              Spike CPU & CRC Errors
            </Badge>
          </div>

          <div class="flex gap-2">
            <select
              v-model="selectedNode"
              aria-label="IPC node"
              :disabled="simOffline"
              class="flex-1 bg-background border border-border text-xs rounded-xl px-3 py-2 text-foreground focus:outline-none focus:border-indigo-500 disabled:opacity-50"
            >
              <option value="">Random Machine / Node</option>
              <option v-for="node in (status?.nodes || [])" :key="node.hostname" :value="node.hostname">
                {{ node.hostname }} ({{ node.profile }})
              </option>
            </select>

            <Button
              size="sm"
              data-testid="sim-inject-fault"
              @click="injectFault"
              :disabled="loading || simOffline"
              class="bg-rose-600 hover:bg-rose-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-4"
            >
              Inject Fault
            </Button>
          </div>

          <div class="flex justify-end">
            <Button
              variant="ghost"
              size="sm"
              data-testid="sim-clear-faults"
              @click="clearFaults"
              :disabled="loading || simOffline"
              class="text-[10px] text-muted-foreground hover:text-foreground"
            >
              Clear Injected Faults
            </Button>
          </div>
        </div>

        <!-- Dev Ticketing Configuration -->
        <div class="p-4 bg-muted/30 rounded-2xl border border-border space-y-4">
          <div class="flex items-center justify-between">
            <span class="text-xs font-black uppercase tracking-wider text-foreground flex items-center gap-1.5">
              <Sparkles class="h-4 w-4 text-indigo-600 dark:text-indigo-400" />
              Dev-Only Ticket Generator Settings
            </span>
            <span class="text-xs font-mono font-bold text-muted-foreground">
              Cap: {{ ticketCount }} / 50
            </span>
          </div>

          <!-- Setting 1: Auto-Assign Tickets Setting -->
          <div class="flex items-center justify-between py-2 border-b border-border">
            <div>
              <div class="text-xs font-bold text-foreground">Automatic Ticket Assignment</div>
              <div class="text-[10px] text-muted-foreground">
                Default is OFF (New tickets remain unassigned until claimed)
              </div>
            </div>

            <button
              type="button"
              @click="toggleAutoAssign"
              class="p-1 rounded-lg text-muted-foreground hover:text-foreground transition-colors"
            >
              <ToggleRight v-if="ticketSettings.autoAssignTickets" class="h-6 w-6 text-indigo-600 dark:text-indigo-400" />
              <ToggleLeft v-else class="h-6 w-6 text-muted-foreground" />
            </button>
          </div>

          <!-- Setting 2: Dev 40s Ticket Generator -->
          <div class="flex items-center justify-between py-2 border-b border-border">
            <div>
              <div class="text-xs font-bold text-foreground">Autonomous Incident Generator (~40s)</div>
              <div class="text-[10px] text-muted-foreground">
                Pushes realistic floor incidents automatically (stops at 50 max)
              </div>
            </div>

            <button
              type="button"
              @click="toggleDevGen"
              class="p-1 rounded-lg text-muted-foreground hover:text-foreground transition-colors"
            >
              <ToggleRight v-if="ticketSettings.devTicketGenEnabled" class="h-6 w-6 text-indigo-600 dark:text-indigo-400" />
              <ToggleLeft v-else class="h-6 w-6 text-muted-foreground" />
            </button>
          </div>

          <!-- Instant Trigger Button -->
          <div class="pt-1 flex justify-end">
            <Button
              size="sm"
              @click="generateInstantDevTicket"
              :disabled="loading || ticketCount >= 50"
              class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase tracking-wider"
            >
              <Plus class="h-3.5 w-3.5 mr-1.5" />
              Generate Dev Ticket Now
            </Button>
          </div>
        </div>

      </div>

      <!-- Footer -->
      <div class="p-4 border-t border-border bg-muted/40 flex justify-end">
        <Button size="sm" variant="ghost" @click="close" class="text-xs font-black uppercase text-muted-foreground hover:text-foreground rounded-xl">
          Close
        </Button>
      </div>

  </div>
</template>
