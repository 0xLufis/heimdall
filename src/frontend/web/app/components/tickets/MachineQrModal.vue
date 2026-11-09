<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  Copy, Printer, X, QrCode, CheckCheck, Download,
  ExternalLink, Layers, RefreshCw, Cpu, ShieldAlert,
  Globe, Smartphone, Wifi, Sparkles
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
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

// Host mode: 'origin' (browser default) | 'lan' | 'custom'
const hostMode = ref<'origin' | 'custom'>('origin')
const customHost = ref('')

watch(hostMode, (mode) => {
  if (mode === 'origin') {
    qr.hostOverride.value = ''
  } else if (customHost.value) {
    qr.hostOverride.value = customHost.value
  }
})

watch(customHost, (val) => {
  if (hostMode.value === 'custom') {
    qr.hostOverride.value = val.trim()
  }
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
  <!-- Non-blocking slide-out right drawer (Zero Overlay Architecture) -->
  <div
    v-if="open"
    class="fixed inset-y-0 right-0 z-40 w-full max-w-lg bg-card border-l border-border shadow-2xl flex flex-col animate-in slide-in-from-right duration-200"
    role="dialog"
    aria-label="Composable Action QR Code Generator"
  >
    <!-- Header -->
    <div class="p-5 border-b border-border bg-muted/40 flex items-center justify-between shrink-0">
      <div class="flex items-center gap-3">
        <div class="p-2.5 rounded-2xl bg-indigo-600/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20">
          <QrCode class="w-5 h-5" />
        </div>
        <div>
          <h3 class="text-sm font-black uppercase tracking-wider text-foreground">
            Composable Action QR
          </h3>
          <p class="text-[10px] text-muted-foreground uppercase tracking-widest mt-0.5">
            ISO/IEC 18004 Industrial Mobile Dispatch Voucher
          </p>
        </div>
      </div>
      <Button
        variant="ghost"
        size="icon"
        class="h-8 w-8 text-muted-foreground hover:text-foreground rounded-lg cursor-pointer"
        @click="emit('close')"
        title="Close QR Drawer"
      >
        <X class="w-4 h-4" />
      </Button>
    </div>

    <!-- Scrollable Body -->
    <div class="p-5 space-y-4 flex-1 overflow-y-auto custom-scrollbar text-xs">
      <!-- Target Equipment Free-text Combobox -->
      <div>
        <label class="block text-[10px] font-black uppercase tracking-widest text-muted-foreground mb-1.5 flex items-center gap-1.5">
          <Cpu class="w-3.5 h-3.5 text-indigo-600 dark:text-indigo-400" />
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
        <div v-if="qr.stationName.value && qr.stationName.value !== qr.stationId.value" class="mt-1 text-[11px] text-muted-foreground flex items-center gap-1.5">
          <span class="text-indigo-600 dark:text-indigo-400 font-bold">Display:</span>
          <span class="text-foreground font-semibold">{{ qr.stationName.value }}</span>
        </div>
      </div>

      <!-- Action Selector -->
      <div>
        <label class="block text-[10px] font-black uppercase tracking-widest text-muted-foreground mb-1.5">
          Action Intent
        </label>
        <div class="grid grid-cols-2 sm:grid-cols-3 gap-1.5">
          <button
            v-for="opt in actionOptions"
            :key="opt.id"
            type="button"
            @click="qr.action.value = opt.id"
            class="p-2.5 rounded-xl border text-left transition-all flex flex-col justify-between cursor-pointer"
            :class="[
              qr.action.value === opt.id
                ? 'border-indigo-500 bg-indigo-500/10 text-indigo-700 dark:text-indigo-300 shadow-xs ring-1 ring-indigo-500/50'
                : 'border-border bg-muted/30 text-muted-foreground hover:text-foreground hover:bg-muted'
            ]"
          >
            <span class="text-xs font-bold">{{ opt.label }}</span>
            <span class="text-[9px] text-muted-foreground truncate mt-1">{{ opt.desc }}</span>
          </button>
        </div>
      </div>

      <!-- Protocol & Scheme Toggle -->
      <div>
        <label class="block text-[10px] font-black uppercase tracking-widest text-muted-foreground mb-1.5">
          Protocol & Destination Scheme
        </label>
        <div class="flex gap-2">
          <button
            type="button"
            @click="qr.protocol.value = 'web'"
            class="flex-1 py-1.5 px-3 rounded-xl border text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer"
            :class="[
              qr.protocol.value === 'web'
                ? 'border-cyan-500/40 bg-cyan-500/10 text-cyan-700 dark:text-cyan-300 ring-1 ring-cyan-500/50'
                : 'border-border bg-muted/30 text-muted-foreground hover:text-foreground'
            ]"
          >
            <Globe class="size-3.5" />
            <span>Web Universal</span>
            <Badge variant="outline" class="text-[9px] border-cyan-500/30 text-cyan-700 dark:text-cyan-400 py-0">Phone Camera</Badge>
          </button>

          <button
            type="button"
            @click="qr.protocol.value = 'heimdall'"
            class="flex-1 py-1.5 px-3 rounded-xl border text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer"
            :class="[
              qr.protocol.value === 'heimdall'
                ? 'border-indigo-500/40 bg-indigo-500/10 text-indigo-700 dark:text-indigo-300 ring-1 ring-indigo-500/50'
                : 'border-border bg-muted/30 text-muted-foreground hover:text-foreground'
            ]"
          >
            <Smartphone class="size-3.5" />
            <span>Heimdall App</span>
            <Badge variant="outline" class="text-[9px] border-indigo-500/30 text-indigo-700 dark:text-indigo-400 py-0">heimdall://</Badge>
          </button>
        </div>
      </div>

      <!-- Phone Camera Access Host Override -->
      <div v-if="qr.protocol.value === 'web'" class="space-y-1.5 p-3 rounded-xl bg-muted/20 border border-border">
        <div class="flex items-center justify-between text-[10px] font-bold uppercase tracking-wider text-muted-foreground">
          <span class="flex items-center gap-1.5">
            <Wifi class="size-3 text-indigo-500" />
            <span>Mobile Device Network Access</span>
          </span>
          <span class="font-normal lowercase">for phone camera scanning</span>
        </div>
        <div class="flex gap-2">
          <button
            type="button"
            @click="hostMode = 'origin'"
            class="flex-1 py-1 px-2 rounded-lg border text-[11px] font-medium transition-all cursor-pointer"
            :class="hostMode === 'origin' ? 'border-primary bg-primary/10 text-primary font-bold' : 'border-border text-muted-foreground'"
          >
            Current Host
          </button>
          <button
            type="button"
            @click="hostMode = 'custom'"
            class="flex-1 py-1 px-2 rounded-lg border text-[11px] font-medium transition-all cursor-pointer"
            :class="hostMode === 'custom' ? 'border-primary bg-primary/10 text-primary font-bold' : 'border-border text-muted-foreground'"
          >
            Custom LAN IP / Domain
          </button>
        </div>
        <div v-if="hostMode === 'custom'" class="pt-1">
          <Input
            v-model="customHost"
            placeholder="e.g. http://192.168.1.100:3000 or https://heimdall.plant"
            class="h-7 text-xs font-mono bg-background"
          />
          <p class="text-[9px] text-muted-foreground mt-0.5">
            Allows phone cameras on factory Wi-Fi to load Heimdall instead of localhost.
          </p>
        </div>
      </div>

      <!-- Standard High-Contrast QR Code Display Box -->
      <div class="flex flex-col items-center justify-center p-6 bg-muted/30 rounded-2xl border border-border relative">
        <div v-if="qr.qrDataUrl.value" class="flex flex-col items-center gap-3">
          <!-- White card wrapper ensures standard ISO quiet zone with maximum optical contrast -->
          <div class="p-3 bg-white rounded-2xl shadow-xl border border-slate-200">
            <img
              :src="qr.qrDataUrl.value"
              alt="Action QR Code"
              class="w-[200px] h-[200px] block"
            />
          </div>
          <div class="flex items-center gap-2 flex-wrap justify-center">
            <Badge variant="outline" class="text-[9px] font-black uppercase tracking-wider border-indigo-500/30 text-indigo-700 dark:text-indigo-300 bg-indigo-500/10">
              {{ qr.action.value }}
            </Badge>
            <Badge v-if="qr.machineType.value" variant="outline" class="text-[9px] font-black uppercase tracking-wider border-border text-muted-foreground bg-muted">
              {{ qr.machineType.value }}
            </Badge>
            <span class="text-[10px] text-emerald-600 dark:text-emerald-400 font-semibold flex items-center gap-1">
              <Sparkles class="size-3" />
              <span>Camera-Ready ISO/IEC 18004</span>
            </span>
          </div>
        </div>
        <div v-else-if="qr.isRendering.value" class="flex flex-col items-center gap-3 py-12">
          <RefreshCw class="w-8 h-8 text-indigo-600 dark:text-indigo-400 animate-spin" />
          <span class="text-xs text-muted-foreground">Generating Action QR...</span>
        </div>
        <div v-else class="flex flex-col items-center gap-2 py-8 text-center text-muted-foreground">
          <ShieldAlert class="w-8 h-8 text-amber-500/80" />
          <span class="text-xs">{{ qr.renderError.value || 'Failed to render QR' }}</span>
        </div>
      </div>

      <!-- Action URI Readonly Field -->
      <div class="space-y-1.5">
        <div class="flex items-center justify-between">
          <label class="text-[10px] font-black uppercase tracking-wider text-muted-foreground">Resolved Action URI</label>
          <span class="text-[9px] font-mono text-indigo-600 dark:text-indigo-400">{{ qr.protocol.value === 'heimdall' ? 'App Scheme' : 'Web URL' }}</span>
        </div>
        <div class="flex gap-2">
          <Input
            :value="qr.activeUri.value"
            readonly
            class="flex-1 font-mono text-[11px] bg-background border-border text-foreground focus:border-indigo-500"
          />
          <Button
            variant="outline"
            size="sm"
            class="border-border bg-background hover:bg-indigo-600 hover:border-indigo-600 hover:text-white text-foreground transition-colors shrink-0 gap-1.5 cursor-pointer"
            @click="qr.copyUri"
          >
            <CheckCheck v-if="qr.isCopied.value" class="w-3.5 h-3.5 text-emerald-500" />
            <Copy v-else class="w-3.5 h-3.5" />
            <span class="text-xs">{{ qr.isCopied.value ? 'Copied!' : 'Copy' }}</span>
          </Button>
        </div>
      </div>
    </div>

    <!-- Action Footer (Non-blocking) -->
    <div class="p-4 border-t border-border bg-muted/20 flex items-center justify-between shrink-0">
      <div class="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          class="border-border bg-background hover:bg-muted text-foreground gap-1.5 text-xs font-bold cursor-pointer"
          @click="qr.printQr(`Heimdall Action QR - ${qr.stationName.value || qr.stationId.value}`)"
        >
          <Printer class="w-3.5 h-3.5 text-indigo-600 dark:text-indigo-400" />
          <span>Print Voucher</span>
        </Button>

        <Button
          variant="outline"
          size="sm"
          class="border-border bg-background hover:bg-muted text-foreground gap-1.5 text-xs font-bold cursor-pointer"
          @click="qr.downloadPng()"
        >
          <Download class="w-3.5 h-3.5 text-cyan-600 dark:text-cyan-400" />
          <span>Save PNG</span>
        </Button>
      </div>

      <Button
        variant="ghost"
        size="sm"
        class="text-muted-foreground hover:text-foreground text-xs font-bold cursor-pointer"
        @click="emit('close')"
      >
        Close
      </Button>
    </div>
  </div>
</template>
