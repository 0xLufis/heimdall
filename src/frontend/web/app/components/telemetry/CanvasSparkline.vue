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
    showMinMax?: boolean
    unit?: string
  }>(),
  {
    color: '#06b6d4', // Cyan
    height: 48,
    fill: true,
    showMinMax: false,
    unit: ''
  }
)

const canvasRef = ref<HTMLCanvasElement | null>(null)
let animId: number | null = null

const render = () => {
  const canvas = canvasRef.value
  if (!canvas) return
  const ctx = canvas.getContext('2d')
  if (!ctx) return

  const width = canvas.clientWidth
  const height = canvas.clientHeight

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
    // Draw subtle standby baseline
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)'
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

  // Prevent divide by zero if flatline
  if (min === max) {
    min -= 1
    max += 1
  }

  const paddingY = 4
  const usableHeight = height - paddingY * 2
  const stepX = width / (count - 1)

  ctx.beginPath()
  for (let i = 0; i < count; i++) {
    const x = i * stepX
    const normalized = (rawValues[i] - min) / (max - min)
    const y = height - paddingY - normalized * usableHeight
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

  ctx.restore()
}

onMounted(() => {
  render()
  const loop = () => {
    render()
    animId = requestAnimationFrame(loop)
  }
  animId = requestAnimationFrame(loop)
})

onUnmounted(() => {
  if (animId) cancelAnimationFrame(animId)
})
</script>

<template>
  <div class="relative w-full" :style="{ height: `${height}px` }">
    <canvas ref canvasRef class="w-full h-full block" />
  </div>
</template>
