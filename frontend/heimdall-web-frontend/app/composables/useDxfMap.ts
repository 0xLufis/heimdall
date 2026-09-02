import { ref } from 'vue'
import DxfParser from 'dxf-parser'

export interface ViewBox {
  x: number
  y: number
  width: number
  height: number
}

export const useDxfMap = (dxfUrl: string = '/sample/assembly_line.dxf') => {
  const isLoading = ref(true)
  const error = ref<string | null>(null)
  const parsedEntities = ref<any[]>([])
  const parsedBlocks = ref<Record<string, any>>({})
  const viewBox = ref<ViewBox>({ x: -40, y: -40, width: 1280, height: 880 })
  const activePinHandle = ref<string | null>(null)
  const highlightedHandles = ref<string[]>([])

  const loadAndParseDxf = async (url: string = dxfUrl) => {
    isLoading.value = true
    error.value = null
    try {
      const response = await fetch(url)
      if (!response.ok) throw new Error(`Failed to load CAD layout from ${url}`)
      const text = await response.text()
      const parser = new DxfParser()
      const parsed = parser.parseSync(text)

      if (parsed) {
        parsedEntities.value = parsed.entities || []
        parsedBlocks.value = parsed.blocks || {}
        computeInitialViewBox(parsed.entities || [])
      }
    } catch (e: any) {
      error.value = e.message || 'Failed to parse DXF layout'
    } finally {
      isLoading.value = false
    }
  }

  const computeInitialViewBox = (entities: any[]) => {
    let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity

    entities.forEach((entity: any) => {
      if (entity.type === 'LWPOLYLINE' || entity.type === 'POLYLINE') {
        (entity.vertices || []).forEach((v: any) => {
          minX = Math.min(minX, v.x)
          maxX = Math.max(maxX, v.x)
          minY = Math.min(minY, v.y)
          maxY = Math.max(maxY, v.y)
        })
      } else if (entity.type === 'INSERT' && entity.position) {
        minX = Math.min(minX, entity.position.x - 30)
        maxX = Math.max(maxX, entity.position.x + 30)
        minY = Math.min(minY, entity.position.y - 30)
        maxY = Math.max(maxY, entity.position.y + 30)
      } else if (entity.type === 'LINE' && entity.vertices) {
        minX = Math.min(minX, entity.vertices[0].x, entity.vertices[1].x)
        maxX = Math.max(maxX, entity.vertices[0].x, entity.vertices[1].x)
        minY = Math.min(minY, entity.vertices[0].y, entity.vertices[1].y)
        maxY = Math.max(maxY, entity.vertices[0].y, entity.vertices[1].y)
      } else if (entity.type === 'CIRCLE' && entity.center) {
        const r = entity.radius || 16
        minX = Math.min(minX, entity.center.x - r)
        maxX = Math.max(maxX, entity.center.x + r)
        minY = Math.min(minY, entity.center.y - r)
        maxY = Math.max(maxY, entity.center.y + r)
      } else if (entity.type === 'TEXT' && entity.startPoint) {
        minX = Math.min(minX, entity.startPoint.x - 50)
        maxX = Math.max(maxX, entity.startPoint.x + 50)
        minY = Math.min(minY, entity.startPoint.y - 20)
        maxY = Math.max(maxY, entity.startPoint.y + 20)
      }
    })

    if (minX !== Infinity && maxX !== -Infinity) {
      const pad = 40
      viewBox.value = {
        x: minX - pad,
        y: minY - pad,
        width: Math.max(100, maxX - minX + pad * 2),
        height: Math.max(100, maxY - minY + pad * 2)
      }
    } else {
      viewBox.value = { x: -40, y: -40, width: 1280, height: 880 }
    }
  }

  const zoom = (factor: number, centerX?: number, centerY?: number) => {
    const cx = centerX ?? viewBox.value.x + viewBox.value.width / 2
    const cy = centerY ?? viewBox.value.y + viewBox.value.height / 2

    const newWidth = viewBox.value.width * factor
    const newHeight = viewBox.value.height * factor

    viewBox.value = {
      x: cx - (cx - viewBox.value.x) * factor,
      y: cy - (cy - viewBox.value.y) * factor,
      width: newWidth,
      height: newHeight
    }
  }

  const pan = (dx: number, dy: number) => {
    viewBox.value.x += dx
    viewBox.value.y += dy
  }

  return {
    isLoading,
    error,
    parsedEntities,
    parsedBlocks,
    viewBox,
    activePinHandle,
    highlightedHandles,
    loadAndParseDxf,
    zoom,
    pan
  }
}
