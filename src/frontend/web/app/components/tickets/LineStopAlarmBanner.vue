<script setup lang="ts">
import { computed } from 'vue'
import { ShieldAlert, AlertTriangle, ArrowRight } from 'lucide-vue-next'
import { Badge } from '~/components/ui/badge'
import { Button } from '~/components/ui/button'
import type { MaintenanceTicket } from '~/types/maintenance'

const props = defineProps<{
  tickets: MaintenanceTicket[]
}>()

const emit = defineEmits<{
  'selectTicket': [ticket: MaintenanceTicket]
}>()

const activeCriticalTickets = computed(() => {
  return props.tickets.filter(t =>
    (t.priority === 'Critical' || t.isEscalated) &&
    t.status !== 'Resolved' &&
    t.status !== 'Closed'
  )
})
</script>

<template>
  <div
    v-if="activeCriticalTickets.length > 0"
    class="border border-rose-500/40 bg-rose-950/30 text-rose-200 rounded-xl p-3.5 shadow-lg shadow-rose-950/20 animate-pulse flex flex-col sm:flex-row sm:items-center justify-between gap-3"
  >
    <div class="flex items-center gap-3">
      <div class="p-2 rounded-lg bg-rose-500/20 text-rose-400">
        <ShieldAlert class="w-5 h-5" />
      </div>
      <div>
        <div class="flex items-center gap-2">
          <span class="text-xs font-black tracking-wider uppercase text-rose-400">
            Active Line-Stop / Critical Stoppage Alarm
          </span>
          <Badge variant="outline" class="text-[9px] border-rose-500/50 text-rose-300 bg-rose-500/20 font-mono">
            {{ activeCriticalTickets.length }} Active
          </Badge>
        </div>
        <p class="text-xs text-rose-300/90 font-mono mt-0.5 truncate max-w-xl">
          Station: {{ activeCriticalTickets[0].stationName || activeCriticalTickets[0].stationId }} — {{ activeCriticalTickets[0].title }}
        </p>
      </div>
    </div>

    <Button
      variant="outline"
      size="sm"
      @click="emit('selectTicket', activeCriticalTickets[0])"
      class="border-rose-500/50 text-rose-200 hover:bg-rose-900/40 text-xs gap-1.5 h-8 shrink-0 cursor-pointer"
    >
      <span>Triage Incident</span>
      <ArrowRight class="w-3.5 h-3.5" />
    </Button>
  </div>
</template>
