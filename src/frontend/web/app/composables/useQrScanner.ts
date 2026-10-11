import { ref } from 'vue'

export function useQrScanner() {
  const isScanning = ref(false)
  const isTorchOn = ref(false)
  const hasTorchSupport = ref(false)
  const hasNativeBarcodeDetector = ref(false)
  const cameraError = ref<string | null>(null)
  let mediaStream: MediaStream | null = null
  let videoTrack: MediaStreamTrack | null = null

  if (typeof window !== 'undefined') {
    hasNativeBarcodeDetector.value = 'BarcodeDetector' in window
  }

  /**
   * Generates a high-contrast industrial audio confirmation beep upon successful scan.
   */
  function triggerAudioFeedback() {
    if (typeof window === 'undefined') return
    try {
      const AudioCtx = window.AudioContext || (window as any).webkitAudioContext
      if (!AudioCtx) return
      const ctx = new AudioCtx()
      const osc = ctx.createOscillator()
      const gain = ctx.createGain()

      osc.type = 'sine'
      osc.frequency.setValueAtTime(880, ctx.currentTime) // High pitch A5 confirmation beep
      gain.gain.setValueAtTime(0.15, ctx.currentTime)
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.08)

      osc.connect(gain)
      gain.connect(ctx.destination)

      osc.start()
      osc.stop(ctx.currentTime + 0.08)
    } catch {
      // AudioContext unavailable or restricted
    }
  }

  /**
   * Triggers haptic tactile vibration in noisy plant environments.
   */
  function triggerHapticFeedback() {
    if (typeof window !== 'undefined' && 'vibrate' in navigator) {
      try {
        navigator.vibrate([40, 30, 40])
      } catch {
        // Vibrate not allowed
      }
    }
  }

  async function startScanning(videoElement: HTMLVideoElement | null): Promise<boolean> {
    cameraError.value = null
    if (typeof navigator === 'undefined' || !navigator.mediaDevices?.getUserMedia) {
      cameraError.value = 'Camera API not supported on this browser.'
      return false
    }

    try {
      mediaStream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: 'environment' },
          width: { ideal: 1280 },
          height: { ideal: 720 }
        },
        audio: false
      })

      if (videoElement) {
        videoElement.srcObject = mediaStream
        await videoElement.play().catch(() => {})
      }

      videoTrack = mediaStream.getVideoTracks()[0] || null
      if (videoTrack) {
        const capabilities: any = videoTrack.getCapabilities ? videoTrack.getCapabilities() : {}
        hasTorchSupport.value = Boolean(capabilities.torch)
      }

      isScanning.value = true
      return true
    } catch (err: any) {
      cameraError.value = err?.message || 'Failed to acquire camera stream.'
      isScanning.value = false
      return false
    }
  }

  async function toggleTorch(): Promise<boolean> {
    if (!videoTrack || !hasTorchSupport.value) return false
    try {
      const nextState = !isTorchOn.value
      await (videoTrack as any).applyConstraints({
        advanced: [{ torch: nextState }]
      })
      isTorchOn.value = nextState
      return isTorchOn.value
    } catch {
      return false
    }
  }

  function stopScanning() {
    if (mediaStream) {
      mediaStream.getTracks().forEach(track => track.stop())
      mediaStream = null
    }
    videoTrack = null
    isScanning.value = false
    isTorchOn.value = false
  }

  function onSuccessfulScan(scannedContent: string) {
    triggerAudioFeedback()
    triggerHapticFeedback()
    return scannedContent
  }

  return {
    isScanning,
    isTorchOn,
    hasTorchSupport,
    hasNativeBarcodeDetector,
    cameraError,
    startScanning,
    stopScanning,
    toggleTorch,
    triggerAudioFeedback,
    triggerHapticFeedback,
    onSuccessfulScan
  }
}
