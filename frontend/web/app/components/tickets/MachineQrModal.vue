<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  Copy, Printer, X, QrCode, CheckCheck, Download,
  ExternalLink, Layers, RefreshCw, Cpu, ShieldAlert
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '~/components/ui/dialog'
import { useActionQr } from '~/composables/useActionQr'
import SearchableTargetCombobox, { type TargetItem } from '~/components/common/SearchableTargetCombobox.vue'
import type { QrAction } from '~/utils/qrActionGenerator'

const props = defineProps<{
  open: boolean
  stationId?: string
  stationName?: string
  machineType?: string
  groupId?: string
  ticketId?: string
}>()

const emit = defineEmits<{
  (e: 'close'): void
}>()

const isOpen = computed({
  get: () => props.open,
  set: (v) => { if (!v) emit('close') }
})

// Initialize composable
const qr = useActionQr({
  initialParams: {
    action: 'report-incident',
    stationId: props.stationId,
    machineType: props.machineType,
    groupId: props.groupId,
    ticketId: props.ticketId
  },
  qrWidth: 260
})

// Sync props changes to composable
watch(() => [props.stationId, props.stationName, props.machineType, props.groupId, props.ticketId], () => {
  if (props.stationId) qr.stationId.value = props.stationId
  if (props.stationName) qr.stationName.value = props.stationName
  if (props.machineType) qr.machineType.value = props.machineType
  if (props.groupId) qr.groupId.value = props.groupId
  if (props.ticketId) qr.ticketId.value = props.ticketId
}, { immediate: true })

const actionOptions: { id: QrAction; label: string; desc: string }[] = [
  { id: 'report-incident', label: 'Report Incident', desc: 'Opens ticket creation modal prefilled with this station' },
  { id: 'inspect-machine', label: 'Inspect Machine', desc: 'Opens telemetry snapshot and status overview' },
  { id: 'view-station', label: 'View Station', desc: 'Navigates to full station detail dashboard' },
  { id: 'check-in', label: 'Technician Check-In', desc: 'Registers technician arrival on physical station' },
  { id: 'verify-pm', label: 'Verify PM Task', desc: 'Validates preventive maintenance checklist completion' }
]

// Query function for station search combobox
async function queryStations(q: string): Promise<TargetItem[]> {
  try {
    const res = await $fetch<any[]>('/api/proxy/v1/Machine').catch(() => [])
    if (res && res.length > 0) {
      return res.map(m => ({
        id: m.customIdentifier || m.name || m.id,
        label: m.displayName || m.name || m.customIdentifier,
        sublabel: `${m.organizationId || 'Plant Floor'} • ${m.machineType || 'Machining'}`,
        badge: m.machineType,
        raw: m
      }))
    }
  } catch {}
  return [
    { id: 'STATION-OP10-01', label: 'OP10 Machining Cell', sublabel: 'Battery Assembly Plant • Milling', badge: 'Milling' },
    { id: 'L06-OP150', label: 'Line 06 - Automated Battery Station 150', sublabel: 'Line 06 • Screwing Station', badge: 'Screwing Station' },
    { id: 'L09-OP270', label: 'Line 09 - AOI Optical Inspection 270', sublabel: 'Line 09 • Automatic Optical Inspection', badge: 'AOI' },
    { id: 'ROBOT-CELL-01', label: 'Robotic Welding Cell 01', sublabel: 'Battery Assembly Plant • Manipulator', badge: 'Manipulator' }
  ]
}

function handleStationSelect(item: TargetItem | { id: string; label: string; isCustom: true; raw?: any }) {
  if ('raw' in item && item.raw) {
    qr.setMachine(item.raw)
  } else {
    qr.stationId.value = item.id
    qr.stationName.value = item.label
  }
}
</script>

<template>
  <Dialog v-model:open="isOpen">
    <DialogContent class="max-w-lg bg-slate-950 border-slate-800 text-slate-100 p-0 overflow-hidden rounded-3xl shadow-2xl">
      <!-- Header -->
      <DialogHeader class="p-5 border-b border-slate-800/80 bg-slate-900/60 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="p-2.5 rounded-2xl bg-indigo-600/10 text-indigo-400 border border-indigo-500/20">
            <QrCode class="w-5 h-5" />
          </div>
          <div>
            <DialogTitle class="text-sm font-black uppercase tracking-wider text-slate-200">
              Composable Action QR
            </DialogTitle>
            <p class="text-[10px] text-slate-500 uppercase tracking-widest mt-0.5">
              Reactive Industrial Deep-Link & Dispatch Voucher
            </p>
          </div>
        </div>
      </DialogHeader>

      <div class="p-6 space-y-4 max-h-[80vh] overflow-y-auto">
        <!-- Target Equipment Free-text Combobox -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5 flex items-center gap-1.5">
            <Cpu class="w-3.5 h-3.5 text-indigo-400" />
            <span>Target Station / Machine (Free-Text or Query)</span>
          </label>
          <SearchableTargetCombobox
            v-model="qr.stationId.value"
            placeholder="Type custom equipment ID or select station..."
            category-label="Floor Stations & Machines"
            icon-type="machine"
            :query-fn="queryStations"
            @select="handleStationSelect"
          />
          <div v-if="qr.stationName.value && qr.stationName.value !== qr.stationId.value" class="mt-1 text-[11px] text-slate-400 flex items-center gap-1.5">
            <span class="text-indigo-400 font-bold">Display:</span>
            <span class="text-slate-200 font-semibold">{{ qr.stationName.value }}</span>
          </div>
        </div>

        <!-- Action Selector -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Action Intent
          </label>
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
            <button
              v-for="opt in actionOptions"
              :key="opt.id"
              type="button"
              @click="qr.action.value = opt.id"
              class="p-2.5 rounded-xl border text-left transition-all flex flex-col justify-between"
              :class="[
                qr.action.value === opt.id
                  ? 'border-indigo-500 bg-indigo-950/40 text-indigo-200 shadow-md ring-1 ring-indigo-500/50'
                  : 'border-slate-800 bg-slate-900/60 text-slate-400 hover:text-slate-200 hover:bg-slate-900'
              ]"
            >
              <span class="text-xs font-bold">{{ opt.label }}</span>
              <span class="text-[9px] text-slate-500 truncate mt-1">{{ opt.desc }}</span>
            </button>
          </div>
        </div>

        <!-- Protocol & Scheme Toggle -->
        <div>
          <label class="block text-[10px] font-black uppercase tracking-widest text-slate-400 mb-1.5">
            Protocol & Destination Scheme
          </label>
          <div class="flex gap-2">
            <button
              type="button"
              @click="qr.protocol.value = 'web'"
              class="flex-1 py-1.5 px-3 rounded-xl border text-xs font-bold transition-all flex items-center justify-center gap-2"
              :class="[
                qr.protocol.value === 'web'
                  ? 'border-cyan-500/40 bg-cyan-950/20 text-cyan-300 ring-1 ring-cyan-500/50'
                  : 'border-slate-800 bg-slate-900/60 text-slate-400 hover:text-slate-200'
              ]"
            >
              <span>Web PWA (HTTPS)</span>
              <Badge variant="outline" class="text-[9px] border-cyan-500/30 text-cyan-400 py-0">Universal</Badge>
            </button>

            <button
              type="button"
              @click="qr.protocol.value = 'heimdall'"
              class="flex-1 py-1.5 px-3 rounded-xl border text-xs font-bold transition-all flex items-center justify-center gap-2"
              :class="[
                qr.protocol.value === 'heimdall'
                  ? 'border-indigo-500/40 bg-indigo-950/20 text-indigo-300 ring-1 ring-indigo-500/50'
                  : 'border-slate-800 bg-slate-900/60 text-slate-400 hover:text-slate-200'
              ]"
            >
              <span>Heimdall Native App</span>
              <Badge variant="outline" class="text-[9px] border-indigo-500/30 text-indigo-400 py-0">heimdall://</Badge>
            </button>
          </div>
        </div>

        <!-- QR Code Display Box -->
        <div class="flex flex-col items-center justify-center p-6 bg-slate-900/80 rounded-2xl border border-slate-800/80 relative">
          <div v-if="qr.qrDataUrl.value" class="flex flex-col items-center gap-3">
            <img :src="qr.qrDataUrl.value" alt="Action QR Code" class="w-[200px] h-[200px] rounded-xl shadow-lg border border-slate-700/50" />
            <div class="flex items-center gap-2">
              <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider border-indigo-500/30 text-indigo-300 bg-indigo-500/10">
                {{ qr.action.value }}
              </Badge>
              <Badge v-if="qr.machineType.value" variant="outline" class="text-[9px] font-black uppercase tracking-wider border-slate-700 text-slate-400 bg-slate-950">
                {{ qr.machineType.value }}
              </Badge>
            </div>
          </div>
          <div v-else-if="qr.isRendering.value" class="flex flex-col items-center gap-3 py-12">
            <RefreshCw class="w-8 h-8 text-indigo-400 animate-spin" />
            <span class="text-xs text-slate-500">Generating Action QR...</span>
          </div>
          <div v-else class="flex flex-col items-center gap-2 py-8 text-center text-slate-500">
            <ShieldAlert class="w-8 h-8 text-amber-500/60" />
            <span class="text-xs">{{ qr.renderError.value || 'Failed to render QR' }}</span>
          </div>
        </div>

        <!-- Action URI Readonly Field -->
        <div class="space-y-1.5">
          <div class="flex items-center justify-between">
            <label class="text-[10px] font-black uppercase tracking-wider text-slate-500">Resolved Action URI</label>
            <span class="text-[9px] font-mono text-indigo-400/80">{{ qr.protocol.value === 'heimdall' ? 'App Scheme' : 'Web URL' }}</span>
          </div>
          <div class="flex gap-2">
            <Input
              :value="qr.activeUri.value"
              readonly
              class="flex-1 font-mono text-[11px] bg-slate-900 border-slate-800 text-slate-300 focus:border-indigo-500"
            />
            <Button
              variant="outline"
              size="sm"
              class="border-slate-700 bg-slate-900 hover:bg-indigo-600 hover:border-indigo-600 hover:text-white text-slate-300 transition-colors shrink-0 gap-1.5"
              @click="qr.copyUri"
            >
              <CheckCheck v-if="qr.isCopied.value" class="w-3.5 h-3.5 text-emerald-400" />
              <Copy v-else class="w-3.5 h-3.5" />
              <span class="text-xs">{{ qr.isCopied.value ? 'Copied!' : 'Copy' }}</span>
            </Button>
          </div>
        </div>

        <!-- Action Buttons -->
        <div class="flex items-center justify-between pt-2 border-t border-slate-800/80">
          <div class="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 gap-1.5 text-xs font-bold"
              @click="qr.printQr(`Heimdall Action QR - ${qr.stationName.value || qr.stationId.value}`)"
            >
              <Printer class="w-3.5 h-3.5 text-indigo-400" />
              <span>Print Voucher</span>
            </Button>

            <Button
              variant="outline"
              size="sm"
              class="border-slate-800 bg-slate-900 hover:bg-slate-800 text-slate-300 gap-1.5 text-xs font-bold"
              @click="qr.downloadPng()"
            >
              <Download class="w-3.5 h-3.5 text-cyan-400" />
              <span>Save PNG</span>
            </Button>
          </div>

          <Button
            variant="ghost"
            size="sm"
            class="text-slate-400 hover:text-slate-200 text-xs font-bold"
            @click="emit('close')"
          >
            Close
          </Button>
        </div>
      </div>
    </DialogContent>
  </Dialog>
</template>
