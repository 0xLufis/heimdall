<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from 'vue'
import type { CircularBuffer } from '~/utils/CircularBuffer'

const props = withDefaults(
  defineProps<{
    buffer?: CircularBuffer
    data?: Float32Array | number[]
    color?: string
    height?: number
    fill?: boolean
    /** Draws a dashed reference line (e.g. a target or alarm limit) when it is inside the value range. */
    reference?: number | null
  }>(),
  {
    color: '#06b6d4',
    height: 48,
    fill: true,
    reference: null
  }
)

const canvasRef = ref<HTMLCanvasElement | null>(null)
let animId: number | null = null
// Last drawn state: frames are skipped while the data, size and styling are unchanged.
let drawnVersion = -1
let drawnBuffer: CircularBuffer | undefined
let drawnWidth = 0
let drawnHeight = 0
let dirty = true

const render = () => {
  const canvas = canvasRef.value
  if (!canvas) return

  const width = canvas.clientWidth
  const height = canvas.clientHeight
  const version = props.buffer ? props.buffer.version : -1
  if (
    !dirty &&
    props.buffer === drawnBuffer &&
    version === drawnVersion &&
    width === drawnWidth &&
    height === drawnHeight
  ) {
    return
  }

  const ctx = canvas.getContext('2d')
  if (!ctx) return
  dirty = false
  drawnBuffer = props.buffer
  drawnVersion = version
  drawnWidth = width
  drawnHeight = height

  // Support retina/HiDPI displays
  const dpr = window.devicePixelRatio || 1
  if (canvas.width !== width * dpr || canvas.height !== height * dpr) {
    canvas.width = width * dpr
    canvas.height = height * dpr
  }

  ctx.save()
  ctx.scale(dpr, dpr)
  ctx.clearRect(0, 0, width, height)

  const rawValues = props.buffer ? props.buffer.toArray() : (props.data || [])
  const count = rawValues.length

  if (count < 2) {
    // Not enough samples for a trend: neutral baseline (visible in light and dark themes)
    ctx.strokeStyle = 'rgba(127, 127, 127, 0.35)'
    ctx.lineWidth = 1
    ctx.setLineDash([4, 4])
    ctx.beginPath()
    ctx.moveTo(0, height / 2)
    ctx.lineTo(width, height / 2)
    ctx.stroke()
    ctx.restore()
    return
  }

  let min = Infinity
  let max = -Infinity
  for (let i = 0; i < count; i++) {
    const val = rawValues[i]
    if (val < min) min = val
    if (val > max) max = val
  }

  // Flat line: open the range so it is drawn mid-height instead of dividing by zero
  if (min === max) {
    const pad = Math.abs(min) > 1 ? Math.abs(min) * 0.01 : 1
    min -= pad
    max += pad
  }

  const paddingY = 4
  const usableHeight = height - paddingY * 2
  const stepX = width / (count - 1)
  const toY = (value: number) => height - paddingY - ((value - min) / (max - min)) * usableHeight

  ctx.beginPath()
  for (let i = 0; i < count; i++) {
    const x = i * stepX
    const y = toY(rawValues[i])
    if (i === 0) {
      ctx.moveTo(x, y)
    } else {
      ctx.lineTo(x, y)
    }
  }

  ctx.strokeStyle = props.color
  ctx.lineWidth = 1.75
  ctx.lineCap = 'round'
  ctx.lineJoin = 'round'
  ctx.stroke()

  if (props.fill) {
    ctx.lineTo((count - 1) * stepX, height)
    ctx.lineTo(0, height)
    ctx.closePath()

    const gradient = ctx.createLinearGradient(0, 0, 0, height)
    gradient.addColorStop(0, `${props.color}33`)
    gradient.addColorStop(1, `${props.color}00`)
    ctx.fillStyle = gradient
    ctx.fill()
  }

  if (typeof props.reference === 'number' && props.reference >= min && props.reference <= max) {
    const y = toY(props.reference)
    ctx.strokeStyle = 'rgba(127, 127, 127, 0.7)'
    ctx.lineWidth = 1
    ctx.setLineDash([3, 3])
    ctx.beginPath()
    ctx.moveTo(0, y)
    ctx.lineTo(width, y)
    ctx.stroke()
  }

  ctx.restore()
}

watch(() => [props.color, props.fill, props.reference, props.data], () => { dirty = true })

onMounted(() => {
  const loop = () => {
    render()
    animId = requestAnimationFrame(loop)
  }
  render()
  animId = requestAnimationFrame(loop)
})

onUnmounted(() => {
  if (animId) cancelAnimationFrame(animId)
})
</script>

<template>
  <div class="relative w-full" :style="{ height: `${height}px` }">
    <canvas ref="canvasRef" class="w-full h-full block" />
  </div>
</template>
