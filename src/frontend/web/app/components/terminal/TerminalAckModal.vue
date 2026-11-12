<script setup lang="ts">
import { CheckCircle2, X } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Badge } from '~/components/ui/badge'

defineProps<{
  open: boolean
  ackData: any
  countdown: number
}>()

const emit = defineEmits<{
  'close': []
}>()
</script>

<template>
  <div
    v-if="open"
    class="w-full bg-slate-900 border-2 border-emerald-500/60 rounded-2xl p-4 sm:p-5 text-left shadow-2xl animate-in slide-in-from-top-4 duration-200 mb-4 ring-1 ring-emerald-500/30"
  >
    <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
      <div class="flex items-center gap-4">
        <div class="w-12 h-12 rounded-xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center border border-emerald-500/40 shrink-0">
          <CheckCircle2 class="w-7 h-7" />
        </div>
        <div>
          <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
            <Badge variant="outline" class="text-[10px] px-2 py-0.5 border-emerald-500/40 text-emerald-400 bg-emerald-500/10 font-mono">
              Ticket Dispatched Successfully
            </Badge>
            <span class="text-xs font-mono text-slate-400">Station: {{ ackData?.stationName }}</span>
          </div>
          <h2 class="text-xl font-black text-white mt-0.5">
            {{ ackData?.ticketNumber || 'TKT-DISPATCHED' }}
            <span class="text-sm font-normal text-slate-300 ml-2">({{ ackData?.message || 'Floor maintenance notified' }})</span>
          </h2>
          <div class="flex items-center gap-3 text-xs font-mono text-slate-400 mt-1">
            <span>Alarm: <strong class="text-cyan-400">{{ ackData?.errorCode }}</strong></span>
            <span>Priority: <strong class="text-rose-400 font-bold">{{ ackData?.priority }}</strong></span>
          </div>
        </div>
      </div>

      <div class="flex items-center gap-3 shrink-0">
        <Button
          type="button"
          size="sm"
          @click="emit('close')"
          class="h-10 px-5 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs cursor-pointer shadow-md shadow-emerald-950/40"
        >
          Dismiss ({{ countdown }}s)
        </Button>
      </div>
    </div>
  </div>
</template>
