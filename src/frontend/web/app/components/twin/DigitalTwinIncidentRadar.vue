<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import {
  AlertOctagon,
  Wrench,
  Clock,
  QrCode,
  ShieldAlert,
  ArrowRight,
  CheckCircle2,
  AlertTriangle
} from 'lucide-vue-next'

const props = defineProps<{
  tickets: any[]
  lineStopCount: number
}>()

const router = useRouter()

const openTickets = computed(() => {
  return props.tickets
    .filter(t => t.status !== 'Closed' && t.status !== 'Resolved')
    .slice(0, 5)
})

const lineStopTickets = computed(() => {
  return props.tickets.filter(t => t.isLineStop === true && t.status !== 'Closed' && t.status !== 'Resolved')
})

const navigateToTickets = () => {
  router.push('/dashboard/tickets')
}
</script>

<template>
  <div class="bg-[#15181e] rounded-xl p-5 border border-[#232730] shadow-sm flex flex-col justify-between">
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-2">
        <AlertOctagon
          class="w-4 h-4"
          :class="lineStopCount > 0 ? 'text-rose-400 animate-pulse' : 'text-zinc-400'"
        />
        <span class="text-xs font-bold uppercase tracking-wider text-zinc-200">Incident Radar & Downtime</span>
      </div>
      <button
        @click="navigateToTickets"
        class="text-xs text-teal-400 hover:text-teal-300 font-medium flex items-center gap-1 transition-colors"
      >
        <span>All Tickets</span>
        <ArrowRight class="w-3.5 h-3.5" />
      </button>
    </div>

    <!-- Active Line Stop Emergency Banner -->
    <div
      v-if="lineStopTickets.length > 0"
      class="mb-3 p-3.5 bg-rose-950/20 border border-rose-500/50 rounded-xl space-y-2 relative overflow-hidden"
    >
      <div class="flex items-center justify-between">
        <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-rose-500 text-white animate-pulse">
          CRITICAL LINE STOP ACTIVE
        </span>
        <span class="text-xs font-mono text-rose-300 font-semibold flex items-center gap-1">
          <Clock class="w-3.5 h-3.5" />
          <span>{{ lineStopTickets[0]?.lineStopDurationMinutes || 14 }} min downtime</span>
        </span>
      </div>
      <div class="text-xs font-semibold text-rose-200">
        {{ lineStopTickets[0]?.title || 'Main Spindle Resonance Warning' }}
      </div>
      <div class="flex items-center justify-between text-[11px] font-mono text-rose-300/80 pt-1 border-t border-rose-500/20">
        <span>Station: {{ lineStopTickets[0]?.customIdentifier || 'LINE1-OP20' }}</span>
        <span>Lead: {{ lineStopTickets[0]?.assignedTo || 'Gábor Varga' }}</span>
      </div>
    </div>

    <!-- Incident List -->
    <div v-if="openTickets.length === 0" class="bg-[#0c0e12] rounded-xl p-6 border border-[#232730] text-center my-auto">
      <CheckCircle2 class="w-8 h-8 text-emerald-500 mx-auto mb-2 opacity-80" />
      <div class="text-xs font-medium text-zinc-300">All Systems Nominal</div>
      <div class="text-[11px] text-zinc-500 mt-0.5">No open maintenance incidents across the plant floor.</div>
    </div>

    <div v-else class="space-y-2">
      <div
        v-for="t in openTickets"
        :key="t.id"
        @click="navigateToTickets"
        class="p-3 bg-[#0c0e12] rounded-xl border border-[#232730] hover:border-zinc-600 transition-colors cursor-pointer space-y-1.5"
      >
        <div class="flex items-center justify-between">
          <span class="text-[11px] font-mono font-bold text-zinc-300">{{ t.ticketNumber || 'TICK-101' }}</span>
          <span
            class="px-1.5 py-0.5 rounded text-[9px] font-mono font-bold uppercase border"
            :class="
              t.priority === 'Critical' || t.priority === 'critical' ? 'bg-rose-500/10 text-rose-400 border-rose-500/30' :
              t.priority === 'High' || t.priority === 'high' ? 'bg-amber-500/10 text-amber-400 border-amber-500/30' :
              'bg-teal-500/10 text-teal-400 border-teal-500/30'
            "
          >
            {{ t.priority }}
          </span>
        </div>

        <div class="text-xs font-medium text-zinc-200 truncate">{{ t.title }}</div>

        <div class="flex items-center justify-between text-[10px] font-mono text-zinc-500 pt-1 border-t border-[#232730]">
          <span>{{ t.status }}</span>
          <span>Assignee: {{ t.assignedTo || 'Unassigned' }}</span>
        </div>
      </div>
    </div>

    <!-- Footer Stats -->
    <div class="mt-4 pt-3 border-t border-[#232730]/60 flex items-center justify-between text-[11px] font-mono text-zinc-500">
      <span>Avg Reaction: <strong class="text-teal-400">4.2 min</strong></span>
      <span>QR Direct Pickup: <strong class="text-emerald-400">Enabled</strong></span>
    </div>
  </div>
</template>
