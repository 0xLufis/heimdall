<script setup lang="ts">
import { ref, onMounted, onUnmounted, nextTick } from 'vue'
import {
  Camera,
  X,
  RefreshCw,
  Zap,
  CheckCircle2,
  AlertCircle,
  Upload,
  Sparkles,
  Barcode,
  Layers
} from 'lucide-vue-next'
import { Button } from '~/components/ui/button'
import { Input } from '~/components/ui/input'
import { Badge } from '~/components/ui/badge'
import jsQR from 'jsqr'

const props = defineProps<{
  open?: boolean
  title?: string
}>()

const emit = defineEmits<{
  (e: 'scanned', qrCodeData: string): void
  (e: 'close'): void
}>()

const videoElement = ref<HTMLVideoElement | null>(null)
const canvasElement = ref<HTMLCanvasElement | null>(null)
const fileInput = ref<HTMLInputElement | null>(null)

const isScanning = ref(false)
const isCameraStreamActive = ref(false)
const isSimulatedFeed = ref(false)
const errorMessage = ref<string | null>(null)
const manualInput = ref('')
const facingMode = ref<'environment' | 'user'>('environment')
const lastScannedCode = ref<string | null>(null)

let mediaStream: MediaStream | null = null
let scanInterval: any = null
let simulationAnimFrame: number | null = null
// Incremented whenever the stream is torn down so a late getUserMedia resolution
// (e.g. permission prompt answered after unmount/close) is released instead of leaking.
let streamGeneration = 0
let isUnmounted = false

// Quick one-click test codes for factory floor simulation
const quickTestPresets = [
  'STATION-OP10-01',
  'L06-OP150',
  'L09-OP270',
  'ROBOT-CELL-01',
  'tkt-req-1'
]

async function startScanner() {
  errorMessage.value = null
  isScanning.value = true
  isSimulatedFeed.value = false

  // Ensure DOM is ready
  await nextTick()

  try {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      throw new Error('Camera API (getUserMedia) is not supported in this browser environment.')
    }

    // Stop existing stream if running
    stopMediaStream()
    const generation = streamGeneration

    // Resilient camera constraints: try ideal facingMode, fallback to generic video
    try {
      mediaStream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: facingMode.value },
          width: { ideal: 1280 },
          height: { ideal: 720 }
        },
        audio: false
      })
    } catch {
      // Fallback for laptops/desktops without dedicated environment camera
      mediaStream = await navigator.mediaDevices.getUserMedia({
        video: true,
        audio: false
      })
    }

    if (generation !== streamGeneration || isUnmounted || !isScanning.value) {
      // Scanner was stopped/unmounted while waiting for camera permission
      mediaStream?.getTracks().forEach(track => track.stop())
      mediaStream = null
      return
    }

    if (videoElement.value && mediaStream) {
      videoElement.value.srcObject = mediaStream
      videoElement.value.setAttribute('autoplay', '')
      videoElement.value.setAttribute('muted', '')
      videoElement.value.setAttribute('playsinline', '')

      videoElement.value.onloadedmetadata = () => {
        videoElement.value?.play().catch(e => console.warn('Video play warning:', e))
        isCameraStreamActive.value = true
      }

      await videoElement.value.play().catch(e => console.warn('Direct play warning:', e))
      isCameraStreamActive.value = true
    }

    // Start decoding loop (BarcodeDetector with jsQR fallback)
    startDecodingLoop()
  } catch (err: any) {
    console.warn('Real camera stream failed:', err)
    isCameraStreamActive.value = false
    
    if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
      errorMessage.value = 'Camera access was denied. Please grant permission in browser settings.'
    } else if (err.name === 'NotFoundError' || err.name === 'DevicesNotFoundError') {
      errorMessage.value = 'No physical camera detected on this system. You can switch to Simulated Vision Feed or upload an image.'
    } else {
      errorMessage.value = err.message || 'Unable to initialize optical camera feed.'
    }
  }
}

function startDecodingLoop() {
  if (scanInterval) clearInterval(scanInterval)

  // Offscreen canvas for frame extraction
  const offscreenCanvas = document.createElement('canvas')
  const ctx = offscreenCanvas.getContext('2d', { willReadFrequently: true })

  scanInterval = setInterval(async () => {
    if (!isScanning.value) return

    // 1. If native BarcodeDetector is available, try it first
    if ('BarcodeDetector' in window && videoElement.value && videoElement.value.readyState >= 2) {
      try {
        const detector = new (window as any).BarcodeDetector({ formats: ['qr_code', 'code_128', 'code_39', 'ean_13'] })
        const barcodes = await detector.detect(videoElement.value)
        if (barcodes && barcodes.length > 0) {
          onCodeDetected(barcodes[0].rawValue)
          return
        }
      } catch {
        // Fallback to jsQR
      }
    }

    // 2. jsQR universal fallback (works on Safari, Firefox, Linux, all platforms)
    if (videoElement.value && videoElement.value.readyState >= 2) {
      const w = videoElement.value.videoWidth || 640
      const h = videoElement.value.videoHeight || 480
      if (w > 0 && h > 0 && ctx) {
        offscreenCanvas.width = w
        offscreenCanvas.height = h
        ctx.drawImage(videoElement.value, 0, 0, w, h)
        const imgData = ctx.getImageData(0, 0, w, h)
        if (imgData) {
          const result = jsQR(imgData.data, w, h, { inversionAttempts: 'attemptBoth' })
          if (result && result.data) {
            onCodeDetected(result.data)
          }
        }
      }
    }
  }, 250)
}

function startSimulatedVisionFeed() {
  errorMessage.value = null
  isSimulatedFeed.value = true
  isCameraStreamActive.value = false
  isScanning.value = true
  stopMediaStream()

  nextTick(() => {
    const canvas = canvasElement.value
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    let scanY = 0
    let step = 2

    function renderSimulation() {
      if (!isSimulatedFeed.value) return
      canvas!.width = 640
      canvas!.height = 360

      // Industrial dark sensor background
      ctx!.fillStyle = '#090d16'
      ctx!.fillRect(0, 0, 640, 360)

      // Grid lines
      ctx!.strokeStyle = 'rgba(79, 82, 178, 0.2)'
      ctx!.lineWidth = 1
      for (let x = 0; x < 640; x += 40) {
        ctx!.beginPath()
        ctx!.moveTo(x, 0)
        ctx!.lineTo(x, 360)
        ctx!.stroke()
      }
      for (let y = 0; y < 360; y += 40) {
        ctx!.beginPath()
        ctx!.moveTo(0, y)
        ctx!.lineTo(640, y)
        ctx!.stroke()
      }

      // Telemetry OSD text
      ctx!.fillStyle = '#00ff66'
      ctx!.font = '10px monospace'
      ctx!.fillText('COGNEX IN-SIGHT OPTICAL ENGINE • SIMULATED LIVE', 20, 25)
      ctx!.fillText(`CAM-OT-01 | EXP: 12ms | GAIN: 1.4x | FPS: 30`, 20, 40)
      ctx!.fillText(new Date().toISOString(), 20, 340)

      // Target Reticle
      ctx!.strokeStyle = '#5b5fc7'
      ctx!.lineWidth = 2
      ctx!.strokeRect(220, 80, 200, 200)

      // Laser scanline animation
      scanY += step
      if (scanY > 280 || scanY < 80) step = -step
      ctx!.strokeStyle = '#ff0055'
      ctx!.lineWidth = 2
      ctx!.beginPath()
      ctx!.moveTo(220, scanY)
      ctx!.lineTo(420, scanY)
      ctx!.stroke()

      simulationAnimFrame = requestAnimationFrame(renderSimulation)
    }

    renderSimulation()
  })
}

function stopMediaStream() {
  streamGeneration++
  if (scanInterval) {
    clearInterval(scanInterval)
    scanInterval = null
  }
  if (simulationAnimFrame) {
    cancelAnimationFrame(simulationAnimFrame)
    simulationAnimFrame = null
  }
  if (mediaStream) {
    mediaStream.getTracks().forEach(track => track.stop())
    mediaStream = null
  }
  if (videoElement.value) {
    videoElement.value.srcObject = null
  }
  isCameraStreamActive.value = false
}

function stopScanner() {
  stopMediaStream()
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

function handleImageUpload(event: Event) {
  const target = event.target as HTMLInputElement
  const file = target.files?.[0]
  if (!file) return

  const reader = new FileReader()
  reader.onload = (e) => {
    const img = new Image()
    img.onload = () => {
      const canvas = document.createElement('canvas')
      canvas.width = img.width
      canvas.height = img.height
      const ctx = canvas.getContext('2d')
      if (ctx) {
        ctx.drawImage(img, 0, 0)
        const imgData = ctx.getImageData(0, 0, canvas.width, canvas.height)
        const result = jsQR(imgData.data, canvas.width, canvas.height, { inversionAttempts: 'attemptBoth' })
        if (result && result.data) {
          onCodeDetected(result.data)
        } else {
          errorMessage.value = 'No readable QR code found in uploaded image. Please try another snapshot.'
        }
      }
    }
    img.src = e.target?.result as string
  }
  reader.readAsDataURL(file)
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
  isUnmounted = true
  stopScanner()
})
</script>

<template>
  <div class="relative rounded-2xl border border-border bg-card p-6 shadow-2xl overflow-hidden max-w-xl mx-auto">
    <!-- Header -->
    <div class="flex items-center justify-between pb-4 border-b border-border">
      <div class="flex items-center gap-3">
        <div class="p-2 rounded-xl bg-indigo-500/10 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20">
          <Camera class="h-5 w-5" />
        </div>
        <div>
          <h4 class="text-sm font-black uppercase tracking-tight text-foreground">
            {{ title || 'Equipment QR Code Scanner' }}
          </h4>
          <p class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider mt-0.5">
            ISO/IEC 18004 Live Optical Feed & Barcode Reader
          </p>
        </div>
      </div>

      <div class="flex items-center gap-2">
        <Button
          variant="ghost"
          size="icon"
          @click="toggleCamera"
          class="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg cursor-pointer"
          title="Switch Camera (Front/Rear)"
        >
          <RefreshCw class="h-4 w-4" />
        </Button>
        <Button
          variant="ghost"
          size="icon"
          @click="handleClose"
          class="h-8 w-8 text-muted-foreground hover:text-foreground hover:bg-muted rounded-lg cursor-pointer"
          title="Close Scanner"
        >
          <X class="h-4 w-4" />
        </Button>
      </div>
    </div>

    <!-- Video Viewfinder Area (Always mounted to avoid Vue ref race conditions) -->
    <div class="relative mt-4 bg-black rounded-xl overflow-hidden aspect-video border border-border flex items-center justify-center shadow-inner">
      <!-- Real Camera Video Element -->
      <video
        ref="videoElement"
        autoplay
        playsinline
        muted
        class="w-full h-full object-cover"
        :class="{ hidden: isSimulatedFeed }"
      ></video>

      <!-- Simulated Optical Vision Canvas -->
      <canvas
        v-if="isSimulatedFeed"
        ref="canvasElement"
        class="w-full h-full object-cover"
      ></canvas>

      <!-- Viewfinder Reticle Overlay -->
      <div v-if="isScanning && !errorMessage" class="absolute inset-0 pointer-events-none flex items-center justify-center">
        <!-- Reticle corners -->
        <div class="relative w-48 h-48 border-2 border-indigo-500/40 rounded-2xl flex items-center justify-center">
          <div class="absolute -top-1 -left-1 w-6 h-6 border-t-4 border-l-4 border-indigo-500 rounded-tl"></div>
          <div class="absolute -top-1 -right-1 w-6 h-6 border-t-4 border-r-4 border-indigo-500 rounded-tr"></div>
          <div class="absolute -bottom-1 -left-1 w-6 h-6 border-b-4 border-l-4 border-indigo-500 rounded-bl"></div>
          <div class="absolute -bottom-1 -right-1 w-6 h-6 border-b-4 border-r-4 border-indigo-500 rounded-br"></div>
          
          <!-- Animated laser scanline -->
          <div class="w-full h-0.5 bg-gradient-to-r from-transparent via-emerald-500 to-transparent shadow-[0_0_15px_#57715b] animate-pulse"></div>
        </div>

        <!-- Telemetry Pill -->
        <div class="absolute bottom-2.5 px-3 py-1 rounded-full bg-black/70 backdrop-blur-xs text-[10px] font-mono text-white/90 border border-white/10 flex items-center gap-1.5">
          <span class="size-2 rounded-full" :class="isCameraStreamActive ? 'bg-emerald-400 animate-ping' : 'bg-indigo-400 animate-pulse'" />
          <span>{{ isCameraStreamActive ? 'Live Camera Feed' : isSimulatedFeed ? 'Simulated Vision Feed' : 'Optical Engine Ready' }}</span>
        </div>
      </div>

      <!-- Scanned Confirmation Overlay -->
      <div v-if="lastScannedCode" class="absolute inset-0 bg-background/95 backdrop-blur-sm flex flex-col items-center justify-center p-6 text-center z-10 animate-in fade-in">
        <CheckCircle2 class="h-12 w-12 text-emerald-600 dark:text-emerald-400 mb-2 animate-bounce" />
        <span class="text-xs font-black text-foreground uppercase tracking-widest">QR Code Detected</span>
        <code class="mt-2 px-3 py-1.5 rounded-lg bg-muted border border-border text-indigo-600 dark:text-indigo-300 font-mono text-sm max-w-full truncate">
          {{ lastScannedCode }}
        </code>
      </div>

      <!-- Error State with Fallback Options -->
      <div v-if="errorMessage && !isSimulatedFeed" class="absolute inset-0 bg-background/95 p-6 flex flex-col items-center justify-center text-center z-10 space-y-3">
        <AlertCircle class="h-10 w-10 text-amber-500" />
        <div class="space-y-1">
          <span class="text-xs font-bold text-amber-600 dark:text-amber-400 uppercase tracking-wider block">Camera Unavailable</span>
          <p class="text-[11px] text-muted-foreground max-w-xs leading-relaxed">
            {{ errorMessage }}
          </p>
        </div>
        
        <div class="flex items-center gap-2 pt-1 flex-wrap justify-center">
          <Button size="sm" @click="startScanner" class="bg-primary hover:bg-primary/90 text-primary-foreground text-xs font-bold cursor-pointer">
            <RefreshCw class="size-3 mr-1" />
            Retry Camera
          </Button>

          <Button size="sm" variant="outline" @click="startSimulatedVisionFeed" class="text-xs font-bold cursor-pointer">
            <Sparkles class="size-3 mr-1 text-indigo-500" />
            Simulate Vision Feed
          </Button>

          <Button size="sm" variant="outline" @click="fileInput?.click()" class="text-xs font-bold cursor-pointer">
            <Upload class="size-3 mr-1 text-cyan-500" />
            Upload Image
          </Button>
        </div>
      </div>
    </div>

    <!-- Controls & Manual Write-In -->
    <div class="mt-4 space-y-3">
      <div class="flex items-center justify-between">
        <div class="flex items-center gap-2">
          <Badge
            variant="outline"
            :class="isScanning ? 'border-emerald-500/30 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10' : 'border-border text-muted-foreground bg-muted'"
            class="text-[9px] font-black uppercase tracking-widest"
          >
            <span class="w-1.5 h-1.5 rounded-full mr-1.5" :class="isScanning ? 'bg-emerald-500 dark:bg-emerald-400 animate-ping' : 'bg-muted-foreground'"></span>
            {{ isScanning ? 'Scanner Active' : 'Scanner Idle' }}
          </Badge>
          <span class="text-[10px] text-muted-foreground font-mono">
            {{ facingMode === 'environment' ? 'Rear Camera' : 'Front Camera' }}
          </span>
        </div>

        <div class="flex items-center gap-1.5">
          <Button
            size="sm"
            variant="outline"
            @click="fileInput?.click()"
            class="text-[10px] font-bold h-7 px-2 border-border cursor-pointer"
            title="Scan from image file"
          >
            <Upload class="size-3 mr-1" />
            <span>Upload QR</span>
          </Button>

          <Button
            v-if="!isCameraStreamActive && !isSimulatedFeed"
            size="sm"
            variant="outline"
            @click="startSimulatedVisionFeed"
            class="text-[10px] font-bold h-7 px-2 border-indigo-500/30 text-indigo-600 dark:text-indigo-400 cursor-pointer"
          >
            <Sparkles class="size-3 mr-1" />
            <span>Simulate Feed</span>
          </Button>
        </div>
      </div>

      <!-- Quick Test Barcode Buttons -->
      <div class="space-y-1 pt-1">
        <span class="text-[10px] font-bold text-muted-foreground uppercase tracking-wider block">
          Quick Test Target Barcodes
        </span>
        <div class="flex flex-wrap gap-1.5">
          <button
            v-for="code in quickTestPresets"
            :key="code"
            type="button"
            @click="onCodeDetected(code)"
            class="px-2 py-0.5 rounded-md bg-muted/60 hover:bg-muted text-[10px] font-mono text-foreground border border-border transition-colors cursor-pointer"
          >
            {{ code }}
          </button>
        </div>
      </div>

      <!-- Manual Input Fallback -->
      <div class="pt-2 border-t border-border">
        <label class="block text-[10px] font-black uppercase tracking-widest text-muted-foreground mb-1.5">
          Manual Equipment ID Entry
        </label>
        <div class="flex gap-2">
          <Input
            v-model="manualInput"
            placeholder="e.g. STATION-OP10-01 or ctrl-101"
            class="bg-background border-input text-foreground rounded-xl text-xs font-mono"
            @keyup.enter="submitManualInput"
          />
          <Button
            @click="submitManualInput"
            :disabled="!manualInput.trim()"
            class="bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-xs font-black uppercase tracking-wider px-4 cursor-pointer"
          >
            Use ID
          </Button>
        </div>
      </div>
    </div>

    <!-- Hidden File Input for Image Upload Scanning -->
    <input
      ref="fileInput"
      type="file"
      accept="image/*"
      class="hidden"
      @change="handleImageUpload"
    />
  </div>
</template>
