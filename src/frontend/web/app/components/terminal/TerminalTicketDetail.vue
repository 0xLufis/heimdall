<script setup lang="ts">
/**
 * Read-only view of one of the station's tickets on the kiosk. Rendered inline in place of the ticket
 * list (the app has no modal overlays); the kiosk cannot change a ticket, only show it and its QR code.
 */
import { computed, ref, watch } from 'vue'
import { ArrowLeft, Lock, QrCode, Zap } from 'lucide-vue-next'
import TerminalQrCard from '~/components/terminal/TerminalQrCard.vue'
import {
  formatTerminalTime,
  terminalPriorityClass,
  type TerminalStation,
  type TerminalTicket
} from '~/utils/terminalTicketView'

const props = defineProps<{
  ticket: TerminalTicket
  station: TerminalStation
  /** Tighter spacing and a smaller QR code for the compact overlay window. */
  compact?: boolean
}>()

const emit = defineEmits<{ (e: 'close'): void }>()

const showQr = ref(false)
watch(() => props.ticket.id, () => { showQr.value = false })

const facts = computed(() => {
  const t = props.ticket
  const rows: { label: string; value: string }[] = [
    { label: 'Reported', value: formatTerminalTime(t.createdAt) || '—' },
    { label: 'Updated', value: formatTerminalTime(t.updatedAt) || '—' },
    { label: 'Assigned to', value: t.assignedTechnicianName || 'Not assigned yet' },
    { label: 'Technician on site', value: t.qrScannedAt ? formatTerminalTime(t.qrScannedAt) : 'Not checked in' }
  ]
  if (t.errorCode) rows.push({ label: 'Alarm', value: t.errorCode })
  if (t.category) rows.push({ label: 'Category', value: t.category })
  if (t.reportedByUserName) rows.push({ label: 'Reported by', value: t.reportedByUserName })
  if (t.pendingReason) rows.push({ label: 'Waiting for', value: t.pendingReason })
  return rows
})
</script>

<template>
  <section
    data-testid="terminal-ticket-detail"
    class="w-full"
    :aria-label="`Ticket ${ticket.ticketNumber} (read-only)`"
  >
    <div class="flex items-center justify-between gap-2">
      <button
        type="button"
        data-testid="terminal-ticket-back"
        class="h-10 px-3 rounded-lg border border-slate-700 text-slate-200 text-xs font-bold uppercase tracking-wide flex items-center gap-1.5 cursor-pointer hover:border-slate-500"
        @click="emit('close')"
      >
        <ArrowLeft class="w-4 h-4" />
        Back
      </button>
      <span class="text-[11px] font-mono text-slate-500 flex items-center gap-1">
        <Lock class="w-3 h-3" />
        Read-only
      </span>
    </div>

    <div class="mt-3">
      <div class="flex flex-wrap items-center gap-2">
        <span class="text-xs font-mono font-bold text-cyan-400">{{ ticket.ticketNumber }}</span>
        <span class="text-[10px] px-1.5 py-0.5 rounded border font-bold uppercase" :class="terminalPriorityClass(ticket.priority)">
          {{ ticket.priority }}
        </span>
        <span class="text-[11px] font-mono font-semibold px-2 py-0.5 rounded bg-slate-800 text-slate-200" data-testid="terminal-ticket-status">
          {{ ticket.status }}
        </span>
        <span
          v-if="ticket.isLineStop"
          class="text-[10px] px-1.5 py-0.5 rounded border border-red-500/50 bg-red-500/10 text-red-300 font-bold uppercase flex items-center gap-1"
        >
          <Zap class="w-3 h-3" />
          Line stop
        </span>
      </div>
      <h3 class="font-black text-white mt-1 leading-snug" :class="compact ? 'text-sm' : 'text-base'">{{ ticket.title }}</h3>
    </div>

    <dl class="mt-3 grid grid-cols-2 gap-x-3 gap-y-2">
      <div v-for="f in facts" :key="f.label" class="min-w-0">
        <dt class="text-[10px] font-bold uppercase tracking-wide text-slate-500">{{ f.label }}</dt>
        <dd class="text-xs text-slate-200 font-mono truncate">{{ f.value }}</dd>
      </div>
    </dl>

    <p
      v-if="ticket.description"
      data-testid="terminal-ticket-description"
      class="mt-3 text-sm text-slate-300 whitespace-pre-line"
    >{{ ticket.description }}</p>

    <div v-if="ticket.comments?.length" class="mt-3">
      <div class="text-[10px] font-bold uppercase tracking-wide text-slate-500 mb-1">Latest notes</div>
      <ul class="space-y-1.5">
        <li
          v-for="(c, i) in ticket.comments"
          :key="`${c.createdAt}-${i}`"
          class="bg-slate-950/60 border border-slate-800 rounded-lg px-2.5 py-1.5"
        >
          <div class="text-[10px] font-mono text-slate-500">{{ c.authorName }} · {{ formatTerminalTime(c.createdAt) }}</div>
          <div class="text-xs text-slate-300 whitespace-pre-line">{{ c.content }}</div>
        </li>
      </ul>
    </div>

    <button
      type="button"
      data-testid="terminal-ticket-qr"
      :aria-pressed="showQr"
      class="mt-4 w-full min-h-[52px] rounded-xl border-2 border-cyan-500/60 bg-cyan-500/10 text-cyan-100 font-black uppercase tracking-wider flex items-center justify-center gap-2 cursor-pointer hover:bg-cyan-500/20"
      @click="showQr = !showQr"
    >
      <QrCode class="w-5 h-5" />
      {{ showQr ? 'Hide QR code' : 'Show QR code' }}
    </button>
    <TerminalQrCard
      v-if="showQr"
      class="mt-3"
      :station="station"
      :ticket="ticket"
      :compact="compact"
      @close="showQr = false"
    />
  </section>
</template>
