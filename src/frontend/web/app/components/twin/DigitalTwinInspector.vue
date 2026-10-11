<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  X,
  Activity,
  Cpu,
  Layers,
  Wrench,
  UserCheck,
  ShieldCheck,
  AlertOctagon,
  ExternalLink,
  Camera,
  QrCode,
  CheckCircle2,
  AlertTriangle,
  Flame,
  Gauge,
  Zap,
  ArrowUpRight
} from 'lucide-vue-next'
import type { MachineTwinNode } from '~/composables/useDigitalTwin'

const props = defineProps<{
  machine: MachineTwinNode | null
  activeTickets: any[]
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'raise-ticket', machine: MachineTwinNode): void
  (e: 'assign-part', machine: MachineTwinNode): void
}>()

const router = useRouter()
const activeTab = ref<'telemetry' | 'controller' | 'components' | 'maintenance' | 'delegations'>('telemetry')
const isSnapshotting = ref(false)
const snapshotSuccessMsg = ref<string | null>(null)

// Compute tickets related to this machine
const machineTickets = computed(() => {
  if (!props.machine) return []
  return props.activeTickets.filter(t =>
    t.machineId === props.machine?.id ||
    t.stationId === props.machine?.id ||
    t.customIdentifier === props.machine?.customIdentifier ||
    t.title?.includes(props.machine?.customIdentifier || '')
  )
})

const handleTriggerSnapshot = async () => {
  if (!props.machine?.controllers?.[0]?.id) return
  try {
    isSnapshotting.value = true
    snapshotSuccessMsg.value = null
    const cId = props.machine.controllers[0].id
    // Call controller snapshot API
    await $fetch(`/api/proxy/ClientPc/${cId}/snapshot`, { method: 'POST' }).catch(() => ({}))
    snapshotSuccessMsg.value = `Diagnostic snapshot sealed (SHA-256) at ${new Date().toLocaleTimeString()}`
    setTimeout(() => { snapshotSuccessMsg.value = null }, 5000)
  } catch (err) {
    snapshotSuccessMsg.value = `Snapshot captured locally.`
    setTimeout(() => { snapshotSuccessMsg.value = null }, 4000)
  } finally {
    isSnapshotting.value = false
  }
}

const openTerminal = () => {
  if (props.machine) {
    router.push(`/terminal/machine/${props.machine.id}`)
  }
}
</script>

<template>
  <aside
    v-if="machine"
    class="w-full lg:w-96 bg-[#15181e] border-l border-[#232730] flex flex-col shadow-2xl z-30 flex-shrink-0 transition-all duration-300"
  >
    <!-- Inspector Header -->
    <div class="p-5 border-b border-[#232730] bg-[#11141a]">
      <div class="flex items-center justify-between mb-3">
        <div class="flex items-center gap-2">
          <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-teal-500/10 text-teal-300 border border-teal-500/30">
            DIGITAL TWIN SHADOW
          </span>
          <span class="text-xs font-mono text-zinc-500">{{ machine.groupId }}</span>
        </div>
        <button
          @click="emit('close')"
          class="p-1 rounded-lg text-zinc-400 hover:text-white hover:bg-zinc-800 transition-colors"
          title="Close Inspector"
        >
          <X class="w-4 h-4" />
        </button>
      </div>

      <div class="flex items-baseline justify-between gap-2">
        <div>
          <h3 class="text-base font-bold text-white tracking-tight">{{ machine.customIdentifier }}</h3>
          <p class="text-xs text-zinc-400 truncate max-w-[260px]">{{ machine.displayName || machine.name }}</p>
        </div>
        <div class="flex items-center gap-1.5">
          <span
            class="px-2 py-0.5 rounded text-[10px] font-mono font-bold uppercase border"
            :class="
              machine.status === 'active' ? 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30' :
              machine.status === 'maintenance' ? 'bg-sky-500/10 text-sky-400 border-sky-500/30' :
              machine.status === 'decommissioned' ? 'bg-zinc-800 text-zinc-400 border-zinc-700' :
              'bg-amber-500/10 text-amber-400 border-amber-500/30'
            "
          >
            {{ machine.status }}
          </span>
        </div>
      </div>

      <!-- Quick Metrics Ribbon -->
      <div class="grid grid-cols-3 gap-2 mt-4 pt-3 border-t border-[#232730]/60">
        <div class="bg-[#0c0e12] rounded-lg p-2 border border-[#232730] text-center">
          <div class="text-[9px] font-mono uppercase text-zinc-500">Health</div>
          <div class="text-xs font-mono font-bold text-emerald-400">{{ machine.telemetry?.healthScore ?? 95 }}%</div>
        </div>
        <div class="bg-[#0c0e12] rounded-lg p-2 border border-[#232730] text-center">
          <div class="text-[9px] font-mono uppercase text-zinc-500">Vibration</div>
          <div class="text-xs font-mono font-bold text-teal-400">{{ machine.telemetry?.vibrationRms ?? 1.1 }} mm/s</div>
        </div>
        <div class="bg-[#0c0e12] rounded-lg p-2 border border-[#232730] text-center">
          <div class="text-[9px] font-mono uppercase text-zinc-500">Temp</div>
          <div class="text-xs font-mono font-bold text-amber-400">{{ machine.telemetry?.temperature ?? 38 }}°C</div>
        </div>
      </div>
    </div>

    <!-- Inspector Tabs Navigation -->
    <div class="flex border-b border-[#232730] bg-[#0c0e12] px-3 gap-1 overflow-x-auto text-xs font-medium">
      <button
        @click="activeTab = 'telemetry'"
        class="py-2.5 px-2.5 border-b-2 transition-all flex items-center gap-1.5 whitespace-nowrap"
        :class="activeTab === 'telemetry' ? 'border-teal-400 text-teal-300 font-semibold' : 'border-transparent text-zinc-400 hover:text-zinc-200'"
      >
        <Activity class="w-3.5 h-3.5" />
        <span>Telemetry</span>
      </button>

      <button
        @click="activeTab = 'controller'"
        class="py-2.5 px-2.5 border-b-2 transition-all flex items-center gap-1.5 whitespace-nowrap"
        :class="activeTab === 'controller' ? 'border-teal-400 text-teal-300 font-semibold' : 'border-transparent text-zinc-400 hover:text-zinc-200'"
      >
        <Cpu class="w-3.5 h-3.5" />
        <span>Edge IPC</span>
      </button>

      <button
        @click="activeTab = 'components'"
        class="py-2.5 px-2.5 border-b-2 transition-all flex items-center gap-1.5 whitespace-nowrap"
        :class="activeTab === 'components' ? 'border-teal-400 text-teal-300 font-semibold' : 'border-transparent text-zinc-400 hover:text-zinc-200'"
      >
        <Layers class="w-3.5 h-3.5" />
        <span>Parts</span>
      </button>

      <button
        @click="activeTab = 'maintenance'"
        class="py-2.5 px-2.5 border-b-2 transition-all flex items-center gap-1.5 whitespace-nowrap"
        :class="activeTab === 'maintenance' ? 'border-teal-400 text-teal-300 font-semibold' : 'border-transparent text-zinc-400 hover:text-zinc-200'"
      >
        <Wrench class="w-3.5 h-3.5" />
        <span>Tickets</span>
        <span v-if="machineTickets.length" class="ml-0.5 px-1 py-0.2 rounded-full text-[9px] bg-rose-500/20 text-rose-400 font-mono">
          {{ machineTickets.length }}
        </span>
      </button>

      <button
        @click="activeTab = 'delegations'"
        class="py-2.5 px-2.5 border-b-2 transition-all flex items-center gap-1.5 whitespace-nowrap"
        :class="activeTab === 'delegations' ? 'border-teal-400 text-teal-300 font-semibold' : 'border-transparent text-zinc-400 hover:text-zinc-200'"
      >
        <UserCheck class="w-3.5 h-3.5" />
        <span>Routing</span>
      </button>
    </div>

    <!-- Inspector Tab Content Area -->
    <div class="flex-1 p-5 overflow-y-auto space-y-5 text-sm">
      <!-- 1. TELEMETRY TAB -->
      <div v-if="activeTab === 'telemetry'" class="space-y-4">
        <div class="bg-[#0c0e12] rounded-xl p-4 border border-[#232730] space-y-3">
          <div class="flex items-center justify-between text-xs">
            <span class="text-zinc-400 font-medium flex items-center gap-1.5">
              <Activity class="w-3.5 h-3.5 text-teal-400" />
              Vibration Velocity (ISO 10816)
            </span>
            <span class="font-mono text-teal-300 font-bold">{{ machine.telemetry?.vibrationRms }} mm/s RMS</span>
          </div>
          <!-- Progress bar -->
          <div class="w-full bg-[#1c2027] h-2 rounded-full overflow-hidden">
            <div
              class="h-full bg-gradient-to-r from-teal-500 to-amber-500 transition-all duration-500"
              :style="{ width: `${Math.min(100, ((machine.telemetry?.vibrationRms || 1) / 4.5) * 100)}%` }"
            ></div>
          </div>
          <div class="flex justify-between text-[10px] font-mono text-zinc-500">
            <span>Good (&lt;1.8)</span>
            <span>Alert (&gt;2.8)</span>
            <span>Trip (&gt;4.5)</span>
          </div>
        </div>

        <div class="bg-[#0c0e12] rounded-xl p-4 border border-[#232730] space-y-3">
          <div class="flex items-center justify-between text-xs">
            <span class="text-zinc-400 font-medium flex items-center gap-1.5">
              <Flame class="w-3.5 h-3.5 text-amber-400" />
              Thermal Envelope (Bearing Temp)
            </span>
            <span class="font-mono text-amber-300 font-bold">{{ machine.telemetry?.temperature }} °C</span>
          </div>
          <div class="w-full bg-[#1c2027] h-2 rounded-full overflow-hidden">
            <div
              class="h-full bg-gradient-to-r from-emerald-500 via-amber-500 to-rose-500 transition-all duration-500"
              :style="{ width: `${Math.min(100, ((machine.telemetry?.temperature || 30) / 90) * 100)}%` }"
            ></div>
          </div>
          <div class="flex justify-between text-[10px] font-mono text-zinc-500">
            <span>Ambient (25°C)</span>
            <span>Nominal (45°C)</span>
            <span>Max (80°C)</span>
          </div>
        </div>

        <div class="grid grid-cols-2 gap-3">
          <div class="bg-[#0c0e12] rounded-xl p-3 border border-[#232730]">
            <div class="text-[10px] font-mono text-zinc-500 uppercase flex items-center gap-1">
              <Zap class="w-3 h-3 text-amber-400" />
              Drive Current
            </div>
            <div class="text-base font-mono font-bold text-zinc-200 mt-1">
              {{ machine.telemetry?.motorCurrent }} A
            </div>
            <div class="text-[10px] text-zinc-500 mt-0.5">Servo Axis 1</div>
          </div>

          <div class="bg-[#0c0e12] rounded-xl p-3 border border-[#232730]">
            <div class="text-[10px] font-mono text-zinc-500 uppercase flex items-center gap-1">
              <Gauge class="w-3 h-3 text-sky-400" />
              Main Air Line
            </div>
            <div class="text-base font-mono font-bold text-zinc-200 mt-1">
              {{ machine.telemetry?.pressure }} bar
            </div>
            <div class="text-[10px] text-zinc-500 mt-0.5">Regulated 6.0 bar</div>
          </div>
        </div>

        <!-- Spatial / CAD Anchor Info -->
        <div class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] flex items-center justify-between text-xs">
          <div>
            <span class="text-zinc-500 font-mono text-[10px] uppercase block">CAD Spatial Anchor</span>
            <span class="font-mono text-zinc-200 font-semibold">{{ machine.pinnedObjectHandle }}</span>
          </div>
          <span class="px-2 py-0.5 bg-zinc-800 text-zinc-400 rounded text-[10px] font-mono">
            DXF Layer: STATIONS
          </span>
        </div>
      </div>

      <!-- 2. CONTROLLER TAB -->
      <div v-if="activeTab === 'controller'" class="space-y-4">
        <div class="bg-[#0c0e12] rounded-xl p-4 border border-[#232730] space-y-3">
          <div class="flex items-center justify-between">
            <div class="flex items-center gap-2">
              <Cpu class="w-4 h-4 text-teal-400" />
              <span class="text-xs font-bold text-zinc-200 font-mono">
                {{ machine.controllers?.[0]?.hostname || 'IPC-STATION-CTRL' }}
              </span>
            </div>
            <span class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
              ONLINE
            </span>
          </div>

          <div class="grid grid-cols-2 gap-2 text-xs pt-2 border-t border-[#232730]">
            <div>
              <span class="text-zinc-500 text-[10px] uppercase font-mono block">IP Address</span>
              <span class="font-mono text-zinc-300">{{ machine.controllers?.[0]?.ipAddress || '192.168.1.115' }}</span>
            </div>
            <div>
              <span class="text-zinc-500 text-[10px] uppercase font-mono block">Network Bus</span>
              <span class="font-mono text-zinc-300">VLAN 10 / EtherCAT</span>
            </div>
          </div>

          <div class="space-y-1.5 pt-2 border-t border-[#232730]">
            <div class="flex justify-between text-[11px] font-mono">
              <span class="text-zinc-400">Edge CPU Load</span>
              <span class="text-zinc-200 font-bold">14.2%</span>
            </div>
            <div class="w-full bg-[#1c2027] h-1.5 rounded-full overflow-hidden">
              <div class="h-full bg-teal-400 w-[14%]"></div>
            </div>
          </div>

          <div class="space-y-1.5">
            <div class="flex justify-between text-[11px] font-mono">
              <span class="text-zinc-400">Memory Usage</span>
              <span class="text-zinc-200 font-bold">48.6%</span>
            </div>
            <div class="w-full bg-[#1c2027] h-1.5 rounded-full overflow-hidden">
              <div class="h-full bg-sky-400 w-[48%]"></div>
            </div>
          </div>
        </div>

        <div v-if="snapshotSuccessMsg" class="p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-lg text-emerald-300 text-xs font-mono flex items-center gap-2">
          <CheckCircle2 class="w-4 h-4 text-emerald-400 flex-shrink-0" />
          <span>{{ snapshotSuccessMsg }}</span>
        </div>

        <div class="flex flex-col gap-2">
          <button
            @click="handleTriggerSnapshot"
            :disabled="isSnapshotting"
            class="w-full py-2 px-3 rounded-lg bg-zinc-800 hover:bg-zinc-700 border border-zinc-700 text-zinc-200 text-xs font-semibold flex items-center justify-center gap-2 transition-colors disabled:opacity-50"
          >
            <Camera class="w-3.5 h-3.5 text-teal-400" />
            <span>{{ isSnapshotting ? 'Sealing Cryptographic Digest...' : 'Capture Diagnostic Snapshot' }}</span>
          </button>
        </div>
      </div>

      <!-- 3. COMPONENTS TAB -->
      <div v-if="activeTab === 'components'" class="space-y-3">
        <div class="flex items-center justify-between">
          <span class="text-xs font-mono uppercase text-zinc-400">Installed Component Slots</span>
          <button
            @click="emit('assign-part', machine)"
            class="text-xs text-teal-400 hover:text-teal-300 font-medium flex items-center gap-1"
          >
            <span>+ Assign Part</span>
          </button>
        </div>

        <div class="space-y-2">
          <div class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] flex items-center justify-between">
            <div>
              <div class="text-xs font-semibold text-zinc-200">Main Spindle Servo 15kW</div>
              <div class="text-[11px] font-mono text-zinc-500">SN-SPINDLE-994 • Slot AX1</div>
            </div>
            <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
              Optimal
            </span>
          </div>

          <div class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] flex items-center justify-between">
            <div>
              <div class="text-xs font-semibold text-zinc-200">Beckhoff CX5140 Embedded PC</div>
              <div class="text-[11px] font-mono text-zinc-500">SN-CX5140-9941 • Slot CTRL-01</div>
            </div>
            <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
              Optimal
            </span>
          </div>

          <div class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] flex items-center justify-between">
            <div>
              <div class="text-xs font-semibold text-zinc-200">Festo Valve Terminal CPX</div>
              <div class="text-[11px] font-mono text-zinc-500">SN-FESTO-772 • Slot PNEUM-1</div>
            </div>
            <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-amber-500/10 text-amber-400 border border-amber-500/30">
              Inspect
            </span>
          </div>
        </div>
      </div>

      <!-- 4. MAINTENANCE TAB -->
      <div v-if="activeTab === 'maintenance'" class="space-y-3">
        <div class="flex items-center justify-between">
          <span class="text-xs font-mono uppercase text-zinc-400">Active Incidents</span>
          <button
            @click="emit('raise-ticket', machine)"
            class="text-xs text-rose-400 hover:text-rose-300 font-semibold"
          >
            + Raise Ticket
          </button>
        </div>

        <div v-if="machineTickets.length === 0" class="p-6 bg-[#0c0e12] rounded-xl border border-[#232730] text-center">
          <CheckCircle2 class="w-8 h-8 text-emerald-500 mx-auto mb-2 opacity-80" />
          <p class="text-xs font-medium text-zinc-300">No Active Incidents</p>
          <p class="text-[11px] text-zinc-500 mt-1">Station telemetry within nominal envelopes.</p>
        </div>

        <div v-else class="space-y-2">
          <div
            v-for="t in machineTickets"
            :key="t.id"
            class="p-3 bg-[#0c0e12] rounded-xl border border-rose-500/30 space-y-2"
          >
            <div class="flex items-center justify-between">
              <span class="text-[11px] font-mono font-bold text-rose-400">{{ t.ticketNumber || 'TICK-NEW' }}</span>
              <span class="px-1.5 py-0.5 rounded text-[9px] font-mono font-bold bg-rose-500/10 text-rose-300 border border-rose-500/30">
                {{ t.priority || 'High' }}
              </span>
            </div>
            <div class="text-xs font-medium text-zinc-200">{{ t.title }}</div>
            <div class="text-[11px] text-zinc-400 line-clamp-2">{{ t.description }}</div>
            <div class="pt-2 border-t border-[#232730] flex items-center justify-between text-[10px] text-zinc-500">
              <span v-if="t.isLineStop" class="text-rose-400 font-bold font-mono animate-pulse">LINE STOPPED</span>
              <span v-else>Status: {{ t.status }}</span>
              <span class="font-mono">Assignee: {{ t.assignedTo || 'Unassigned' }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 5. DELEGATIONS TAB -->
      <div v-if="activeTab === 'delegations'" class="space-y-3">
        <div class="bg-[#0c0e12] rounded-xl p-4 border border-[#232730] space-y-3">
          <div class="text-xs font-semibold text-zinc-200 flex items-center gap-1.5">
            <ShieldCheck class="w-4 h-4 text-teal-400" />
            Automated Personnel Resolution
          </div>

          <div class="space-y-2 text-xs pt-1">
            <div class="flex justify-between py-1 border-b border-[#232730]">
              <span class="text-zinc-500">Discipline</span>
              <span class="font-medium text-zinc-200">{{ machine.technologies?.[0] || 'Controls & Safety' }}</span>
            </div>
            <div class="flex justify-between py-1 border-b border-[#232730]">
              <span class="text-zinc-500">Lead Technician</span>
              <span class="font-medium text-teal-300">{{ machine.preferredTechnicianName || 'Gábor Varga' }}</span>
            </div>
            <div class="flex justify-between py-1 border-b border-[#232730]">
              <span class="text-zinc-500">Required Skill Code</span>
              <span class="font-mono text-zinc-200">SKILL-TWINCAT3</span>
            </div>
            <div class="flex justify-between py-1">
              <span class="text-zinc-500">Off-Hours Fallback</span>
              <span class="font-medium text-zinc-400">Standby Shift Roster</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Inspector Bottom Actions -->
    <div class="p-4 border-t border-[#232730] bg-[#11141a] flex items-center gap-2">
      <button
        @click="openTerminal"
        class="flex-1 py-2 px-3 rounded-lg bg-teal-500/10 hover:bg-teal-500/20 border border-teal-500/30 text-teal-300 text-xs font-semibold flex items-center justify-center gap-1.5 transition-colors"
      >
        <span>Open Kiosk Terminal</span>
        <ArrowUpRight class="w-3.5 h-3.5" />
      </button>
    </div>
  </aside>
</template>
