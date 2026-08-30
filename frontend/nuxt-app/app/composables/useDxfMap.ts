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
  const viewBox = ref<ViewBox>({ x: 0, y: -50, width: 200, height: 100 })
  const activePinHandle = ref<string | null>(null)
  const highlightedHandles = ref<string[]>([])

  const loadAndParseDxf = async (url: string = dxfUrl) => {
    isLoading.value = true
    error.value = null
    try {
      const response = await fetch(url)
      if (!response.ok) throw new Error(`Failed to load DXF from ${url}`)
      const text = await response.text()
      const parser = new DxfParser()
      const parsed = parser.parseSync(text)

      if (parsed && parsed.entities) {
        parsedEntities.value = parsed.entities
        computeInitialViewBox(parsed.entities)
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
      if (entity.type === 'INSERT' && entity.position) {
        minX = Math.min(minX, entity.position.x - 20)
        maxX = Math.max(maxX, entity.position.x + 20)
        minY = Math.min(minY, entity.position.y - 20)
        maxY = Math.max(maxY, entity.position.y + 20)
      } else if (entity.type === 'LINE' && entity.vertices) {
        minX = Math.min(minX, entity.vertices[0].x, entity.vertices[1].x)
        maxX = Math.max(maxX, entity.vertices[0].x, entity.vertices[1].x)
        minY = Math.min(minY, entity.vertices[0].y, entity.vertices[1].y)
        maxY = Math.max(maxY, entity.vertices[0].y, entity.vertices[1].y)
      } else if (entity.type === 'CIRCLE' && entity.center) {
        const r = entity.radius || 10
        minX = Math.min(minX, entity.center.x - r)
        maxX = Math.max(maxX, entity.center.x + r)
        minY = Math.min(minY, entity.center.y - r)
        maxY = Math.max(maxY, entity.center.y + r)
      }
    })

    if (minX !== Infinity && maxX !== -Infinity) {
      const pad = 20
      viewBox.value = {
        x: minX - pad,
        y: minY - pad,
        width: maxX - minX + pad * 2,
        height: maxY - minY + pad * 2
      }
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
    viewBox,
    activePinHandle,
    highlightedHandles,
    loadAndParseDxf,
    zoom,
    pan
  }
}
