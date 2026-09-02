<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Camera, X, RefreshCw, Zap, Flashlight, CheckCircle2, AlertCircle } from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'

const props = defineProps<{
  open?: boolean
  title?: string
}>()

const emit = defineEmits<{
  (e: 'scanned', qrCodeData: string): void
  (e: 'close'): void
}>()

const videoElement = ref<HTMLVideoElement | null>(null)
const isScanning = ref(false)
const errorMessage = ref<string | null>(null)
const manualInput = ref('')
const facingMode = ref<'environment' | 'user'>('environment')
const lastScannedCode = ref<string | null>(null)

let mediaStream: MediaStream | null = null
let scanInterval: any = null

async function startScanner() {
  errorMessage.value = null
  isScanning.value = true

  try {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      errorMessage.value = 'Camera API is not supported in this browser environment.'
      isScanning.value = false
      return
    }

    // Stop existing stream if any
    stopScanner()

    mediaStream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: facingMode.value, width: { ideal: 1280 }, height: { ideal: 720 } },
      audio: false
    })

    if (videoElement.value) {
      videoElement.value.srcObject = mediaStream
      await videoElement.value.play()
    }

    isScanning.value = true

    // Check for native BarcodeDetector API (supported in Chromium / Android WebView)
    if ('BarcodeDetector' in window) {
      const BarcodeDetectorClass = (window as any).BarcodeDetector
      const detector = new BarcodeDetectorClass({ formats: ['qr_code', 'code_128', 'code_39', 'ean_13'] })

      scanInterval = setInterval(async () => {
        if (!videoElement.value || !isScanning.value) return
        try {
          const barcodes = await detector.detect(videoElement.value)
          if (barcodes && barcodes.length > 0) {
            const code = barcodes[0].rawValue
            onCodeDetected(code)
          }
        } catch (e) {
          // Ignore detection frame errors
        }
      }, 400)
    }
  } catch (err: any) {
    console.error('Camera QR scanner error:', err)
    if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
      errorMessage.value = 'Camera access was denied. Please grant permission in browser settings.'
    } else if (err.name === 'NotFoundError' || err.name === 'DevicesNotFoundError') {
      errorMessage.value = 'No camera device found on this device.'
    } else {
      errorMessage.value = `Camera error: ${err.message || 'Unable to initialize video stream'}`
    }
    isScanning.value = false
  }
}

function stopScanner() {
  if (scanInterval) {
    clearInterval(scanInterval)
    scanInterval = null
  }

  if (mediaStream) {
    mediaStream.getTracks().forEach(track => track.stop())
    mediaStream = null
  }

  if (videoElement.value) {
    videoElement.value.srcObject = null
  }

  isScanning.value = false
}

function toggleCamera() {
  facingMode.value = facingMode.value === 'environment' ? 'user' : 'environment'
  if (isScanning.value) {
    startScanner()
  }
}

function onCodeDetected(code: string) {
  if (!code) return
  lastScannedCode.value = code

  // Trigger tactile vibration feedback if supported
  if (typeof navigator !== 'undefined' && 'vibrate' in navigator) {
    try {
      navigator.vibrate([100, 50, 100])
    } catch (_) {}
  }

  emit('scanned', code)
  stopScanner()
}

function submitManualInput() {
  if (!manualInput.value.trim()) return
  onCodeDetected(manualInput.value.trim())
}

function handleClose() {
  stopScanner()
  emit('close')
}

onMounted(() => {
  if (props.open !== false) {
    startScanner()
  }
})

onUnmounted(() => {
  stopScanner()
})
</script>

<template>
  <div class="relative rounded-2xl border border-slate-800 bg-slate-950 p-6 shadow-2xl overflow-hidden max-w-xl mx-auto">
    <!-- Header -->
    <div class="flex items-center justify-between pb-4 border-b border-slate-800">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20">
          <Camera class="h-5 w-5" />
        </div>
        <div>
          <h4 class="text-sm font-black uppercase tracking-tight text-slate-100">
            {{ title || 'Equipment QR Code Scanner' }}
          </h4>
          <p class="text-[10px] font-bold text-slate-500 uppercase tracking-wider mt-0.5">
            Scan barcode or QR code on Industrial Machine / Controller
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          @click="toggleCamera"
          class="h-8 w-8 text-slate-400 hover:text-white hover:bg-slate-900 rounded-lg"
          title="Switch Camera"
        >
          <RefreshCw class="h-4 w-4" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          @click="handleClose"
          class="h-8 w-8 text-slate-400 hover:text-white hover:bg-slate-900 rounded-lg"
          title="Close Scanner"
        >
          <X class="h-4 w-4" />
        </Button>
      </div>
    </div>

    <!-- Video Viewfinder Area -->
    <div class="relative mt-4 bg-slate-900 rounded-xl overflow-hidden aspect-video border border-slate-800 flex items-center justify-center">
      <video
        ref="videoElement"
        class="w-full h-full object-cover"
        playsinline
        muted
      ></video>

      <!-- Viewfinder Reticle Overlay -->
      <div v-if="isScanning" class="absolute inset-0 pointer-events-none flex items-center justify-center">
        <!-- Reticle corners -->
        <div class="relative w-48 h-48 border-2 border-indigo-500/40 rounded-2xl flex items-center justify-center">
          <div class="absolute -top-1 -left-1 w-6 h-6 border-t-4 border-l-4 border-indigo-500 rounded-tl"></div>
          <div class="absolute -top-1 -right-1 w-6 h-6 border-t-4 border-r-4 border-indigo-500 rounded-tr"></div>
          <div class="absolute -bottom-1 -left-1 w-6 h-6 border-b-4 border-l-4 border-indigo-500 rounded-bl"></div>
          <div class="absolute -bottom-1 -right-1 w-6 h-6 border-b-4 border-r-4 border-indigo-500 rounded-br"></div>
          
          <!-- Animated laser scanline -->
          <div class="w-full h-0.5 bg-gradient-to-r from-transparent via-indigo-500 to-transparent shadow-[0_0_15px_#6366f1] animate-pulse"></div>
        </div>
      </div>

      <!-- Scanned Confirmation Overlay -->
      <div v-if="lastScannedCode" class="absolute inset-0 bg-slate-950/90 backdrop-blur-sm flex flex-col items-center justify-center p-6 text-center z-10 animate-in fade-in">
        <CheckCircle2 class="h-12 w-12 text-emerald-400 mb-2 animate-bounce" />
        <span class="text-xs font-black text-slate-300 uppercase tracking-widest">QR Code Detected</span>
        <code class="mt-2 px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 text-indigo-300 font-mono text-sm max-w-full truncate">
          {{ lastScannedCode }}
        </code>
      </div>

      <!-- Error State -->
      <div v-if="errorMessage" class="absolute inset-0 bg-slate-950/95 p-6 flex flex-col items-center justify-center text-center z-10">
        <AlertCircle class="h-10 w-10 text-amber-500 mb-2" />
        <span class="text-xs font-bold text-amber-400 uppercase tracking-wider mb-2">Camera Unavailable</span>
        <p class="text-[11px] text-slate-400 max-w-xs leading-relaxed mb-4">
          {{ errorMessage }}
        </p>
        <Button size="sm" @click="startScanner" class="bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-black uppercase">
          Retry Camera
        </Button>
      </div>
    </div>

    <!-- Controls & Manual Write-In -->
    <div class="mt-4 space-y-3">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Badge
            variant="outline"
            :class="isScanning ? 'border-emerald-500/30 text-emerald-400 bg-emerald-500/10' : 'border-slate-800 text-slate-500 bg-slate-900'"
            class="text-[9px] font-black uppercase tracking-widest"
          >
            <span class="w-1.5 h-1.5 rounded-full mr-1.5" :class="isScanning ? 'bg-emerald-400 animate-ping' : 'bg-slate-600'"></span>
            {{ isScanning ? 'Scanner Active' : 'Scanner Idle' }}
          </Badge>
          <span class="text-[10px] text-slate-500 font-mono">
            {{ facingMode === 'environment' ? 'Rear Camera' : 'Front Camera' }}
          </span>
        </div>

        <Button
          v-if="!isScanning && !errorMessage"
          size="sm"
          @click="startScanner"
          class="bg-indigo-600 hover:bg-indigo-700 text-white text-[10px] font-black uppercase tracking-widest rounded-xl h-8"
        >
          Start Camera
        </Button>
      </div>

      <!-- Manual Input Fallback -->
      <div class="pt-3 border-t border-slate-900">
        <label class="block text-[10px] font-black uppercase tracking-widest text-slate-500 mb-1.5">
          Manual Equipment ID Entry
        </label>
        <div class="flex gap-2">
          <Input
            v-model="manualInput"
            placeholder="e.g. STATION-OP10-01 or ctrl-101"
            class="bg-slate-900 border-slate-800 rounded-xl text-xs"
            @keyup.enter="submitManualInput"
          />
          <Button
            @click="submitManualInput"
            :disabled="!manualInput.trim()"
            class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-4"
          >
            Use ID
          </Button>
        </div>
      </div>
    </div>
  </div>
</template>
