<script setup lang="ts">
/**
 * Inline QR card for the station kiosk (a panel in the page, not a modal overlay).
 * For a ticket the code lets a technician check in at the station, which the backend records as the
 * ticket's QR pickup (arrival time), or open the ticket on a phone. For the station it opens incident
 * reporting or the station page on a phone. The code is a /mobile/action link on this origin.
 */
import { computed, ref, watch } from 'vue'
import { QrCode } from 'lucide-vue-next'
import { useActionQr } from '~/composables/useActionQr'
import type { QrAction } from '~/utils/qrActionGenerator'
import type { TerminalStation, TerminalTicket } from '~/utils/terminalTicketView'

const props = defineProps<{
  station: TerminalStation
  ticket?: Pick<TerminalTicket, 'id' | 'ticketNumber'> | null
  /** Smaller code for the compact overlay window. */
  compact?: boolean
}>()

const emit = defineEmits<{ (e: 'close'): void }>()

interface QrChoice {
  action: QrAction
  label: string
  hint: string
}

const choices = computed<QrChoice[]>(() => props.ticket
  ? [
      { action: 'check-in', label: 'Check in', hint: 'Technician on site: scan to register your arrival for this ticket.' },
      { action: 'view-ticket', label: 'Open on phone', hint: 'Scan to open this ticket on your phone.' }
    ]
  : [
      { action: 'report-incident', label: 'Report on phone', hint: 'Scan to report an incident for this station from your phone.' },
      { action: 'view-station', label: 'Station on phone', hint: 'Scan to open this station on your phone.' }
    ])

const selected = ref<QrAction>(choices.value[0]!.action)
watch(() => props.ticket?.id, () => { selected.value = choices.value[0]!.action })

const qr = useActionQr({ qrWidth: props.compact ? 200 : 260 })
const { qrDataUrl, activeUri, renderError } = qr

// Only what the chosen action needs, so the code stays small enough to scan from a distance.
watch([() => props.station, () => props.ticket, selected], () => {
  const s = props.station
  qr.stationId.value = s.customIdentifier || s.name || s.id || ''
  qr.stationName.value = s.displayName || s.name || qr.stationId.value
  qr.ticketId.value = props.ticket?.id ?? ''
  const forReport = selected.value === 'report-incident'
  qr.machineType.value = forReport ? (s.machineType || '') : ''
  qr.groupId.value = forReport ? (s.groupId || '') : ''
  qr.prefillTitle.value = ''
  qr.prefillPriority.value = undefined
  qr.action.value = selected.value
}, { immediate: true, deep: true })

const title = computed(() => props.ticket
  ? `QR code · ${props.ticket.ticketNumber}`
  : `QR code · ${props.station.displayName || props.station.name || props.station.customIdentifier || 'Station'}`)
const hint = computed(() => choices.value.find(c => c.action === selected.value)?.hint ?? '')
const size = computed(() => (props.compact ? 200 : 260))
</script>

<template>
  <section
    data-testid="terminal-qr-card"
    class="w-full bg-slate-900 border-2 border-cyan-500/50 rounded-2xl p-4 shadow-2xl ring-1 ring-cyan-500/20"
    :aria-label="title"
  >
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <div class="text-xs font-bold uppercase tracking-wider text-cyan-400 flex items-center gap-2">
          <QrCode class="w-4 h-4 shrink-0" />
          <span class="truncate">{{ title }}</span>
        </div>
        <p class="text-sm text-slate-300 mt-1" data-testid="terminal-qr-hint">{{ hint }}</p>
      </div>
      <button
        type="button"
        data-testid="terminal-qr-close"
        class="shrink-0 h-10 px-3 rounded-lg border border-slate-700 text-slate-300 text-xs font-bold uppercase tracking-wide cursor-pointer hover:border-slate-500"
        @click="emit('close')"
      >
        Hide
      </button>
    </div>

    <div class="mt-3 grid grid-cols-2 gap-2" role="group" aria-label="What the QR code does">
      <button
        v-for="c in choices"
        :key="c.action"
        type="button"
        :data-testid="`terminal-qr-action-${c.action}`"
        :aria-pressed="selected === c.action"
        class="min-h-[48px] rounded-xl border-2 text-sm font-black uppercase tracking-wide cursor-pointer transition-colors"
        :class="selected === c.action
          ? 'border-cyan-400 bg-cyan-500/15 text-cyan-100'
          : 'border-slate-700 bg-slate-950 text-slate-400 hover:border-slate-500'"
        @click="selected = c.action"
      >
        {{ c.label }}
      </button>
    </div>

    <div class="mt-4 flex flex-col items-center gap-2">
      <div class="bg-white rounded-xl p-2">
        <img
          v-if="qrDataUrl"
          :src="qrDataUrl"
          :width="size"
          :height="size"
          :alt="`QR code. ${hint}`"
          data-testid="terminal-qr-image"
          class="block"
        >
      </div>
      <p v-if="renderError" class="text-xs text-rose-400">{{ renderError }}</p>
      <p class="text-[11px] font-mono text-slate-500 break-all text-center" data-testid="terminal-qr-url">{{ activeUri }}</p>
    </div>
  </section>
</template>
