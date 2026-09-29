<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import {
  X,
  QrCode,
  Radio,
  Camera,
  Search,
  PackagePlus,
  Send,
  CheckCircle2,
  AlertTriangle,
  Barcode,
  Layers,
  MapPin,
  Cpu
} from 'lucide-vue-next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type { InventoryPart } from '~/types/inventory'
import { usePartsInventory } from '~/composables/usePartsInventory'

const props = defineProps<{
  open: boolean
}>()

const emit = defineEmits<{
  (e: 'update:open', val: boolean): void
  (e: 'usePart', part: InventoryPart): void
  (e: 'logPartWithCode', code: string): void
}>()

const { lookupCode } = usePartsInventory()

type ScannerAction = 'lookup' | 'log' | 'use'
const activeAction = ref<ScannerAction>('lookup')
const manualCode = ref('')
const isSearching = ref(false)
const scannedPart = ref<InventoryPart | null>(null)
const scanError = ref<string | null>(null)
const isCameraActive = ref(false)

const videoRef = ref<HTMLVideoElement | null>(null)
let mediaStream: MediaStream | null = null

const QUICK_TEST_CODES = [
  { label: 'Beckhoff IPC (IPC-1001)', code: 'IPC-1001' },
  { label: 'Cognex Camera (CAM-1001)', code: 'CAM-1001' },
  { label: 'Siemens Motor (MTR-1001)', code: 'MTR-1001' },
  { label: 'Pneumatic Valves (FST-VLV-M5)', code: 'FST-VLV-M5' },
  { label: 'RFID Tag Payload', code: 'E280116060000204A1B2C301' }
]

const startCamera = async () => {
  if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) return
  try {
    mediaStream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: 'environment' }
    })
    if (videoRef.value) {
      videoRef.value.srcObject = mediaStream
      isCameraActive.value = true
    }
  } catch (err) {
    // Camera unavailable or permission denied, fallback to manual input & quick scans
    isCameraActive.value = false
  }
}

const stopCamera = () => {
  if (mediaStream) {
    mediaStream.getTracks().forEach(track => track.stop())
    mediaStream = null
  }
  isCameraActive.value = false
}

watch(
  () => props.open,
  (val) => {
    if (val) {
      scannedPart.value = null
      scanError.value = null
      manualCode.value = ''
      startCamera()
    } else {
      stopCamera()
    }
  }
)

onUnmounted(() => {
  stopCamera()
})

const handleCodeScan = async (code: string) => {
  if (!code.trim()) return
  isSearching.value = true
  scanError.value = null

  try {
    const part = await lookupCode(code.trim())
    if (part) {
      scannedPart.value = part
      if (activeAction.value === 'use') {
        emit('usePart', part)
        emit('update:open', false)
      } else if (activeAction.value === 'log') {
        emit('logPartWithCode', code.trim())
        emit('update:open', false)
      }
    } else {
      scanError.value = `No inventory part found matching scanned code "${code}".`
      scannedPart.value = null
    }
  } catch (err: any) {
    scanError.value = err.message || 'Lookup failed.'
  } finally {
    isSearching.value = false
  }
}

const triggerUseScanned = () => {
  if (scannedPart.value) {
    emit('usePart', scannedPart.value)
    emit('update:open', false)
  }
}
</script>

<template>
  <div v-if="open" class="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-background/80 backdrop-blur-xs animate-in fade-in duration-200">
    <div class="relative w-full max-w-lg bg-card border border-border rounded-xl shadow-2xl overflow-hidden flex flex-col max-h-[95vh]">
      <!-- Header -->
      <div class="flex items-center justify-between px-5 py-3.5 border-b border-border bg-muted/30">
        <div class="flex items-center gap-2.5">
          <div class="p-2 rounded-lg bg-primary/10 border border-primary/20 text-primary">
            <QrCode class="size-5" />
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h2 class="text-sm font-bold text-foreground">
                Code Scan Interface
              </h2>
              <span class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-primary/10 text-primary border border-primary/20">
                QR · Barcode · RFID
              </span>
            </div>
            <p class="text-[11px] text-muted-foreground">
              Mobile optical camera reader & hardware RFID input
            </p>
          </div>
        </div>
        <button
          type="button"
          @click="emit('update:open', false)"
          class="p-1 rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition-colors cursor-pointer"
        >
          <X class="size-5" />
        </button>
      </div>

      <!-- Action Workflow Selector -->
      <div class="grid grid-cols-3 gap-1.5 p-3 border-b border-border bg-muted/15 text-xs">
        <button
          type="button"
          @click="activeAction = 'lookup'"
          class="flex items-center justify-center gap-1.5 py-2 px-2.5 rounded-lg border font-semibold transition-all cursor-pointer"
          :class="activeAction === 'lookup' ? 'bg-primary text-primary-foreground border-primary shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
        >
          <Search class="size-3.5" />
          <span>Look-up</span>
        </button>

        <button
          type="button"
          @click="activeAction = 'use'"
          class="flex items-center justify-center gap-1.5 py-2 px-2.5 rounded-lg border font-semibold transition-all cursor-pointer"
          :class="activeAction === 'use' ? 'bg-amber-600 text-white border-amber-600 shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
        >
          <Send class="size-3.5" />
          <span>Use Up</span>
        </button>

        <button
          type="button"
          @click="activeAction = 'log'"
          class="flex items-center justify-center gap-1.5 py-2 px-2.5 rounded-lg border font-semibold transition-all cursor-pointer"
          :class="activeAction === 'log' ? 'bg-teal-600 text-white border-teal-600 shadow-xs' : 'bg-card border-border text-muted-foreground hover:text-foreground'"
        >
          <PackagePlus class="size-3.5" />
          <span>Log Part</span>
        </button>
      </div>

      <!-- Scanner Area -->
      <div class="p-4 space-y-3.5 overflow-y-auto flex-1">
        <!-- Live Camera Viewport or Viewport Simulation -->
        <div class="relative w-full aspect-video sm:h-52 bg-black/90 rounded-xl overflow-hidden border border-border flex items-center justify-center">
          <video
            v-if="isCameraActive"
            ref="videoRef"
            autoplay
            playsinline
            muted
            class="w-full h-full object-cover"
          />

          <!-- Viewfinder Overlay Reticle -->
          <div class="absolute inset-0 flex items-center justify-center pointer-events-none">
            <div class="size-40 border-2 border-primary/80 rounded-xl relative shadow-lg">
              <div class="absolute top-0 left-0 w-4 h-4 border-t-2 border-l-2 border-primary -translate-x-0.5 -translate-y-0.5" />
              <div class="absolute top-0 right-0 w-4 h-4 border-t-2 border-r-2 border-primary translate-x-0.5 -translate-y-0.5" />
              <div class="absolute bottom-0 left-0 w-4 h-4 border-b-2 border-l-2 border-primary -translate-x-0.5 translate-y-0.5" />
              <div class="absolute bottom-0 right-0 w-4 h-4 border-b-2 border-r-2 border-primary translate-x-0.5 translate-y-0.5" />
              <div class="absolute top-1/2 inset-x-2 h-0.5 bg-primary/40 animate-pulse" />
            </div>
          </div>

          <!-- Camera Status Indicator -->
          <div class="absolute bottom-2.5 inset-x-0 flex items-center justify-center pointer-events-none">
            <span class="px-2.5 py-1 rounded-full text-[10px] font-mono bg-black/70 text-white/90 backdrop-blur-xs flex items-center gap-1.5">
              <span class="size-1.5 rounded-full" :class="isCameraActive ? 'bg-emerald-400 animate-ping' : 'bg-amber-400'" />
              <span>{{ isCameraActive ? 'Camera Active · Scan Target' : 'Hardware / Virtual Scanner Active' }}</span>
            </span>
          </div>
        </div>

        <!-- Manual Input / Bluetooth Barcode Gun / RFID Reader -->
        <div class="space-y-1.5">
          <label class="text-[11px] font-semibold text-muted-foreground flex items-center gap-1.5">
            <Barcode class="size-3.5" />
            <span>Manual Code / Barcode Gun / RFID Payload</span>
          </label>
          <div class="flex gap-2">
            <Input
              v-model="manualCode"
              placeholder="e.g. IPC-1001, CAM-1001, or scan barcode..."
              class="h-9 text-xs font-mono"
              @keydown.enter="handleCodeScan(manualCode)"
            />
            <Button
              size="sm"
              :disabled="!manualCode.trim() || isSearching"
              @click="handleCodeScan(manualCode)"
              class="h-9 px-3 text-xs shrink-0 cursor-pointer"
            >
              Lookup
            </Button>
          </div>
        </div>

        <!-- Quick Test Buttons -->
        <div class="space-y-1.5">
          <span class="text-[10px] uppercase font-bold text-muted-foreground tracking-wider">
            Quick Scan Simulation Presets:
          </span>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="item in QUICK_TEST_CODES"
              :key="item.code"
              type="button"
              @click="handleCodeScan(item.code)"
              class="px-2 py-1 rounded-md text-[11px] font-mono bg-muted/60 hover:bg-muted text-muted-foreground hover:text-foreground border border-border transition-colors cursor-pointer"
            >
              {{ item.label }}
            </button>
          </div>
        </div>

        <!-- Error Message -->
        <div v-if="scanError" class="p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-xs flex items-center gap-2">
          <AlertTriangle class="size-4 shrink-0" />
          <span>{{ scanError }}</span>
        </div>

        <!-- Scanned Part Card Details -->
        <div v-if="scannedPart" class="p-3.5 rounded-xl bg-card border border-border shadow-xs space-y-2 animate-in fade-in duration-200">
          <div class="flex items-start justify-between gap-2">
            <div>
              <div class="font-mono text-xs font-bold text-primary">{{ scannedPart.customIdentifier }}</div>
              <div class="text-sm font-semibold text-foreground">{{ scannedPart.name }}</div>
              <div class="text-[11px] text-muted-foreground">{{ scannedPart.manufacturer.name }} · {{ scannedPart.category }}</div>
            </div>
            <div class="flex flex-col items-end gap-1">
              <span
                class="px-2 py-0.5 rounded text-[10px] font-semibold uppercase"
                :class="scannedPart.trackingType === 'serialized' ? 'bg-teal-500/15 text-teal-600' : 'bg-purple-500/15 text-purple-600'"
              >
                {{ scannedPart.trackingType }}
              </span>
              <span class="text-xs font-mono font-bold text-foreground">
                Qty: {{ scannedPart.quantity }}
              </span>
            </div>
          </div>

          <div class="grid grid-cols-2 gap-2 text-xs pt-2 border-t border-border/60">
            <div class="flex items-center gap-1 text-muted-foreground text-[11px]">
              <MapPin class="size-3 text-primary shrink-0" />
              <span class="truncate">{{ scannedPart.location }}</span>
            </div>
            <div class="text-right font-mono font-semibold text-emerald-600 dark:text-emerald-400">
              €{{ scannedPart.priceEur }}
            </div>
          </div>

          <!-- Quick Action Buttons -->
          <div class="flex items-center justify-end gap-2 pt-2 border-t border-border/60">
            <Button
              v-if="scannedPart.quantity > 0"
              size="sm"
              @click="triggerUseScanned"
              class="h-8 text-xs bg-amber-600 hover:bg-amber-700 text-white gap-1.5 cursor-pointer"
            >
              <Send class="size-3" />
              <span>Use Part (Cost Center)</span>
            </Button>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="flex items-center justify-end px-5 py-3 border-t border-border bg-muted/20">
        <Button
          variant="outline"
          size="sm"
          @click="emit('update:open', false)"
          class="h-8 text-xs cursor-pointer"
        >
          Close Scanner
        </Button>
      </div>
    </div>
  </div>
</template>
