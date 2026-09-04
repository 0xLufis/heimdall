<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { 
  Activity, 
  Flame, 
  X, 
  RefreshCw, 
  ShieldAlert, 
  Cpu, 
  CheckCircle2, 
  Sparkles,
  ToggleLeft,
  ToggleRight,
  Plus
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'ticketCreated'): void
}>()

const status = ref<any>(null)
const loading = ref(false)
const actionMessage = ref<string | null>(null)
const selectedNode = ref<string>('')
const ticketSettings = ref<{ autoAssignTickets: boolean; devTicketGenEnabled: boolean }>({
  autoAssignTickets: false,
  devTicketGenEnabled: true
})
const ticketCount = ref<number>(0)
let timer: any = null

async function fetchStatus() {
  try {
    const data = await $fetch<any>('/api/simulator/status')
    status.value = data
  } catch {}

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

async function injectFault() {
  loading.value = true
  actionMessage.value = null
  try {
    const res = await $fetch<any>('/api/simulator/fault', {
      method: 'POST',
      body: { hostname: selectedNode.value || undefined }
    })
    actionMessage.value = res.message || 'Fault injected successfully!'
    await fetchStatus()
  } catch (err: any) {
    actionMessage.value = 'Failed to inject fault: ' + err.message
  } finally {
    loading.value = false
  }
}

async function clearFaults() {
  loading.value = true
  actionMessage.value = null
  try {
    await $fetch<any>('/api/simulator/clear-faults', { method: 'POST' })
    actionMessage.value = 'All faults cleared across fleet!'
    await fetchStatus()
  } catch (err: any) {
    actionMessage.value = 'Failed to clear faults.'
  } finally {
    loading.value = false
  }
}

async function toggleAutoAssign() {
  const newVal = !ticketSettings.value.autoAssignTickets
  try {
    await $fetch('/api/tickets/settings', {
      method: 'POST',
      body: { autoAssignTickets: newVal }
    })
    ticketSettings.value.autoAssignTickets = newVal
    actionMessage.value = `Auto-Assign Tickets set to ${newVal ? 'ON' : 'OFF'}`
  } catch {}
}

async function toggleDevGen() {
  const newVal = !ticketSettings.value.devTicketGenEnabled
  try {
    await $fetch('/api/tickets/settings', {
      method: 'POST',
      body: { devTicketGenEnabled: newVal }
    })
    ticketSettings.value.devTicketGenEnabled = newVal
    actionMessage.value = `Dev 40s Ticket Generator ${newVal ? 'Enabled' : 'Paused'}`
  } catch {}
}

async function generateInstantDevTicket() {
  loading.value = true
  actionMessage.value = null
  try {
    const res = await $fetch<any>('/api/tickets', {
      method: 'POST',
      body: {
        title: 'Manual Simulator Anomaly Triggered',
        description: 'Simulated high-temperature excursion triggered via interactive fleet control panel.',
        priority: 'High',
        stationId: selectedNode.value || 'ROBOT-CELL-01',
        stationName: `Station ${selectedNode.value || 'ROBOT-CELL-01'}`
      }
    })
    actionMessage.value = `Dev ticket generated: ${res.ticket?.ticketNumber || 'OK'}`
    await fetchStatus()
    emit('ticketCreated')
  } catch (err: any) {
    actionMessage.value = 'Error creating dev ticket.'
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
  <div v-if="open" role="dialog" aria-modal="true" class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-md flex items-center justify-center p-4">
    <div class="bg-slate-900 border border-slate-800 rounded-3xl shadow-2xl w-full max-w-xl overflow-hidden animate-in fade-in zoom-in-95 duration-200">
      
      <!-- Header -->
      <div class="p-5 border-b border-slate-800 flex items-center justify-between bg-slate-950/40">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-2xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
            <Activity class="h-5 w-5" />
          </div>
          <div>
            <h3 class="text-base font-black uppercase tracking-tight text-slate-100 flex items-center gap-2">
              Interactive Fleet Simulator & Dev Generator
            </h3>
            <p class="text-[10px] font-bold text-slate-500 uppercase tracking-widest mt-0.5">
              Live Edge Anomaly Injection, gRPC Telemetry & Dev Ticketing
            </p>
          </div>
        </div>

        <Button variant="ghost" size="icon" @click="close" class="text-slate-400 hover:text-white rounded-xl">
          <X class="h-5 w-5" />
        </Button>
      </div>

      <!-- Action Message Alert -->
      <div v-if="actionMessage" class="mx-6 mt-4 p-3 bg-indigo-950/40 border border-indigo-500/30 rounded-xl flex items-center gap-2.5 text-xs text-indigo-300">
        <CheckCircle2 class="h-4 w-4 shrink-0 text-indigo-400" />
        <span>{{ actionMessage }}</span>
      </div>

      <!-- Body -->
      <div class="p-6 space-y-6 max-h-[70vh] overflow-y-auto">
        
        <!-- Live Fleet Telemetry Stats -->
        <div>
          <div class="text-[10px] font-black uppercase tracking-widest text-slate-400 mb-2.5 flex items-center justify-between">
            <span>Fleet Simulator Status</span>
            <span class="text-emerald-400 flex items-center gap-1.5">
              <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
              Streaming Active
            </span>
          </div>

          <div class="grid grid-cols-3 gap-3 text-center">
            <div class="p-3 bg-slate-950 rounded-2xl border border-slate-800">
              <span class="text-[10px] uppercase font-bold text-slate-500 block">Simulated Nodes</span>
              <span class="text-lg font-black text-slate-100 font-mono mt-0.5 block">
                {{ status?.active_fleet || 50 }}
              </span>
            </div>
            <div class="p-3 bg-slate-950 rounded-2xl border border-slate-800">
              <span class="text-[10px] uppercase font-bold text-slate-500 block">Dispatched</span>
              <span class="text-lg font-black text-indigo-400 font-mono mt-0.5 block">
                {{ status?.total_dispatched || '400+' }}
              </span>
            </div>
            <div class="p-3 bg-slate-950 rounded-2xl border border-slate-800">
              <span class="text-[10px] uppercase font-bold text-slate-500 block">Errors / Faults</span>
              <span class="text-lg font-black text-amber-400 font-mono mt-0.5 block">
                {{ status?.total_errors || 0 }}
              </span>
            </div>
          </div>
        </div>

        <!-- Anomaly Injection Sandbox -->
        <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-xs font-black uppercase tracking-wider text-slate-200 flex items-center gap-1.5">
              <Flame class="h-4 w-4 text-rose-400" />
              Dynamic Anomaly Injection
            </span>
            <Badge variant="outline" class="text-[9px] font-mono border-rose-500/30 text-rose-400">
              Spike CPU & CRC Errors
            </Badge>
          </div>

          <div class="flex gap-2">
            <select
              v-model="selectedNode"
              class="flex-1 bg-slate-900 border border-slate-800 text-xs rounded-xl px-3 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"
            >
              <option value="">Random Machine / Node</option>
              <option v-for="node in (status?.nodes || [])" :key="node.hostname" :value="node.hostname">
                {{ node.hostname }} ({{ node.profile }})
              </option>
            </select>

            <Button
              size="sm"
              @click="injectFault"
              :disabled="loading"
              class="bg-rose-600 hover:bg-rose-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-4"
            >
              Inject Fault
            </Button>
          </div>

          <div class="flex justify-end">
            <Button
              variant="ghost"
              size="sm"
              @click="clearFaults"
              :disabled="loading"
              class="text-[10px] text-slate-400 hover:text-slate-200"
            >
              Clear Injected Faults
            </Button>
          </div>
        </div>

        <!-- Dev Ticketing Configuration -->
        <div class="p-4 bg-slate-950 rounded-2xl border border-slate-800 space-y-4">
          <div class="flex items-center justify-between">
            <span class="text-xs font-black uppercase tracking-wider text-slate-200 flex items-center gap-1.5">
              <Sparkles class="h-4 w-4 text-indigo-400" />
              Dev-Only Ticket Generator Settings
            </span>
            <span class="text-xs font-mono font-bold text-slate-400">
              Cap: {{ ticketCount }} / 50
            </span>
          </div>

          <!-- Setting 1: Auto-Assign Tickets Setting -->
          <div class="flex items-center justify-between py-2 border-b border-slate-900">
            <div>
              <div class="text-xs font-bold text-slate-200">Automatic Ticket Assignment</div>
              <div class="text-[10px] text-slate-500">
                Default is OFF (New tickets remain unassigned until claimed)
              </div>
            </div>

            <button
              type="button"
              @click="toggleAutoAssign"
              class="p-1 rounded-lg text-slate-300 hover:text-white transition-colors"
            >
              <ToggleRight v-if="ticketSettings.autoAssignTickets" class="h-6 w-6 text-indigo-400" />
              <ToggleLeft v-else class="h-6 w-6 text-slate-600" />
            </button>
          </div>

          <!-- Setting 2: Dev 40s Ticket Generator -->
          <div class="flex items-center justify-between py-2 border-b border-slate-900">
            <div>
              <div class="text-xs font-bold text-slate-200">Autonomous Incident Generator (~40s)</div>
              <div class="text-[10px] text-slate-500">
                Pushes realistic floor incidents automatically (stops at 50 max)
              </div>
            </div>

            <button
              type="button"
              @click="toggleDevGen"
              class="p-1 rounded-lg text-slate-300 hover:text-white transition-colors"
            >
              <ToggleRight v-if="ticketSettings.devTicketGenEnabled" class="h-6 w-6 text-indigo-400" />
              <ToggleLeft v-else class="h-6 w-6 text-slate-600" />
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
      <div class="p-4 border-t border-slate-800 bg-slate-950/40 flex justify-end">
        <Button size="sm" variant="ghost" @click="close" class="text-xs font-black uppercase text-slate-400 rounded-xl">
          Close
        </Button>
      </div>

    </div>
  </div>
</template>
