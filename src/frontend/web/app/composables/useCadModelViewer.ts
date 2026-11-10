import { ref, shallowRef, onUnmounted, getCurrentInstance } from 'vue'

export interface CadViewerOptions {
  modelUrl?: string
  backgroundColor?: number | string
  enableGrid?: boolean
  enableShadows?: boolean
}

export interface MeshNodeInfo {
  name: string
  slotIdentifier?: string
  status?: string
}

export interface Projected3dNode {
  name: string
  x: number
  y: number
  isVisible: boolean
  worldPos: [number, number, number]
}

/** Health state of a CAD node, derived by the caller from real component data. */
export type CadNodeStatus = 'healthy' | 'warning' | 'fault'

export const DEFAULT_CAD_MODEL_URL = '/sample/station_digital_twin.glb'

const STATUS_EMISSIVE: Record<CadNodeStatus, { color: number; intensity: number }> = {
  healthy: { color: 0x10b981, intensity: 0.35 },
  warning: { color: 0xf59e0b, intensity: 0.5 },
  fault: { color: 0xef4444, intensity: 0.6 }
}

/**
 * Resolves a model URL and accepts it only if it is same-origin (relative paths included).
 * The model URL comes from backend data (`model3dUrl`), so an arbitrary host must not be
 * fetched with the user's credentials or used to beacon out. Returns null when rejected.
 */
export function resolveSameOriginModelUrl(url: string | null | undefined, origin?: string): string | null {
  if (!url || typeof url !== 'string') return null
  const base = origin ?? (typeof window !== 'undefined' ? window.location.origin : undefined)
  if (!base) return null
  try {
    const resolved = new URL(url, base)
    if (resolved.origin !== new URL(base).origin) return null
    if (resolved.protocol !== 'http:' && resolved.protocol !== 'https:') return null
    return resolved.href
  } catch {
    return null
  }
}

/**
 * Disposes a material (or material array). Textures are only disposed when the material owns
 * them: highlight clones share their textures with the loaded original.
 */
const disposeMaterial = (material: any, withTextures = false) => {
  if (!material) return
  const list = Array.isArray(material) ? material : [material]
  for (const m of list) {
    if (withTextures) {
      for (const key of Object.keys(m)) {
        const value = m[key]
        if (value && typeof value === 'object' && value.isTexture) value.dispose()
      }
    }
    m.dispose?.()
  }
}

const cloneMaterial = (material: any) =>
  Array.isArray(material) ? material.map((m: any) => m.clone()) : material?.clone()

const disposeObjectTree = (root: any) => {
  root?.traverse?.((child: any) => {
    if (child.isMesh || child.isLine || child.isPoints) {
      child.geometry?.dispose?.()
      disposeMaterial(child.material, true)
    }
  })
}

export const useCadModelViewer = () => {
  const isLoading = ref(false)
  const loadProgress = ref(0)
  const error = ref<string | null>(null)
  const isWireframe = ref(false)
  const isStatusOverlayEnabled = ref(false)
  const hoveredNodeName = ref<string | null>(null)
  const selectedNodeName = ref<string | null>(null)
  const availableNodeNames = ref<string[]>([])
  const projectedNodes = ref<Record<string, Projected3dNode>>({})
  const nodeStatuses = shallowRef<Record<string, CadNodeStatus>>({})

  // Three.js runtime references (shallow to avoid Vue reactivity proxy overhead on WebGL objects)
  const sceneRef = shallowRef<any>(null)
  const cameraRef = shallowRef<any>(null)
  const rendererRef = shallowRef<any>(null)
  const controlsRef = shallowRef<any>(null)
  const modelRootRef = shallowRef<any>(null)
  /** Upper-cased node name → mesh (one entry per mesh name). */
  const meshMap = new Map<string, any>()
  /** mesh.id → the mesh's own loaded material (never rendered while an override is active). */
  const originalMaterials = new Map<number, any>()
  /** mesh.id → the highlight material clone currently applied (owned by the viewer, disposed on swap). */
  const overrideMaterials = new Map<number, any>()

  let THREE_NS: any = null
  let animationFrameId: number | null = null
  let resizeObserver: ResizeObserver | null = null
  let containerEl: HTMLElement | null = null
  let canvasEl: HTMLCanvasElement | null = null
  let isDisposed = false
  let loadGeneration = 0

  // Camera animation state
  let isAnimatingCamera = false
  let targetCamPos: any = null
  let targetLookAt: any = null
  let currentLookAt: any = null

  const uniqueMeshes = () => Array.from(new Set(meshMap.values()))

  const initViewer = async (container: HTMLElement, canvas: HTMLCanvasElement, options: CadViewerOptions = {}) => {
    if (typeof window === 'undefined') return

    isDisposed = false
    containerEl = container
    canvasEl = canvas
    isLoading.value = true
    error.value = null

    try {
      // Dynamic imports for Three.js to guarantee SSR safety
      const THREE = await import('three')
      const { OrbitControls } = await import('three/examples/jsm/controls/OrbitControls.js')
      const { GLTFLoader } = await import('three/examples/jsm/loaders/GLTFLoader.js')
      // Unmounted while the chunks were loading: never create a renderer / RAF loop.
      if (isDisposed) return
      THREE_NS = THREE

      const width = container.clientWidth || 800
      const height = container.clientHeight || 500

      // 1. Scene setup
      const scene = new THREE.Scene()
      scene.background = options.backgroundColor !== undefined
        ? new THREE.Color(options.backgroundColor as any)
        : new THREE.Color(0x0b0f19)
      sceneRef.value = scene

      // 2. Camera setup
      const camera = new THREE.PerspectiveCamera(42, width / height, 0.05, 100)
      camera.position.set(1.9, 1.7, 2.1)
      cameraRef.value = camera

      // 3. Renderer setup
      const renderer = new THREE.WebGLRenderer({
        canvas,
        antialias: true,
        alpha: true,
        powerPreference: 'high-performance'
      })
      renderer.setSize(width, height)
      renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2))
      renderer.toneMapping = THREE.ACESFilmicToneMapping
      renderer.toneMappingExposure = 1.1
      rendererRef.value = renderer

      // 4. Controls setup
      const controls = new OrbitControls(camera, canvas)
      controls.enableDamping = true
      controls.dampingFactor = 0.06
      controls.minDistance = 0.4
      controls.maxDistance = 8.0
      controls.maxPolarAngle = Math.PI / 2 + 0.05 // Don't drop below floor
      controls.target.set(0, 0.95, 0)
      controls.update()
      controlsRef.value = controls
      currentLookAt = new THREE.Vector3(0, 0.95, 0)

      // 5. Lighting
      scene.add(new THREE.AmbientLight(0xffffff, 1.4))

      const keyLight = new THREE.DirectionalLight(0xffffff, 2.2)
      keyLight.position.set(4, 9, 5)
      scene.add(keyLight)

      const fillLight = new THREE.DirectionalLight(0x94a3b8, 1.2)
      fillLight.position.set(-4, 6, -4)
      scene.add(fillLight)

      const bottomLight = new THREE.DirectionalLight(0x38bdf8, 0.4)
      bottomLight.position.set(0, -3, 0)
      scene.add(bottomLight)

      // 6. Ground grid
      if (options.enableGrid !== false) {
        const grid = new THREE.GridHelper(3.0, 30, 0x3b82f6, 0x1e293b)
        grid.position.y = 0
        scene.add(grid)
      }

      // 7. Load 3D Model
      await loadModel(options.modelUrl || DEFAULT_CAD_MODEL_URL, THREE, GLTFLoader)
      if (isDisposed) return

      // 8. Render loop (scratch objects reused across frames)
      const box = new THREE.Box3()
      const center = new THREE.Vector3()
      const animate = () => {
        if (isDisposed) return
        animationFrameId = requestAnimationFrame(animate)

        // Smooth camera tweening when focusing on a component
        if (isAnimatingCamera && targetCamPos && targetLookAt) {
          camera.position.lerp(targetCamPos, 0.08)
          currentLookAt.lerp(targetLookAt, 0.08)
          controls.target.copy(currentLookAt)
          controls.update()

          if (camera.position.distanceTo(targetCamPos) < 0.01 && currentLookAt.distanceTo(targetLookAt) < 0.01) {
            isAnimatingCamera = false
          }
        } else {
          controls.update()
        }

        // Update 3D screen projection coordinates for HUD overlays. Only publish to Vue when a
        // value actually changed, otherwise every frame would trigger a component re-render.
        if (containerEl && meshMap.size > 0) {
          const w = containerEl.clientWidth || 800
          const h = containerEl.clientHeight || 500
          const prev = projectedNodes.value
          const projMap: Record<string, Projected3dNode> = {}
          let changed = false

          for (const nodeName of availableNodeNames.value) {
            if (projMap[nodeName]) continue
            const mesh = meshMap.get(nodeName.toUpperCase())
            if (!mesh) continue

            box.setFromObject(mesh)
            box.getCenter(center)
            center.y = box.max.y + 0.08 // Float above top of mesh bounding box

            const worldPos: [number, number, number] = [Number(center.x.toFixed(2)), Number(center.y.toFixed(2)), Number(center.z.toFixed(2))]
            center.project(camera)
            const isVisible =
              center.z > -1.0 && center.z < 1.0 &&
              center.x >= -1.05 && center.x <= 1.05 &&
              center.y >= -1.05 && center.y <= 1.05

            const node: Projected3dNode = {
              name: nodeName,
              x: Math.round((center.x * 0.5 + 0.5) * w),
              y: Math.round((-center.y * 0.5 + 0.5) * h),
              isVisible,
              worldPos
            }
            const old = prev[nodeName]
            if (!old || old.x !== node.x || old.y !== node.y || old.isVisible !== node.isVisible) changed = true
            projMap[nodeName] = node
          }
          if (changed || Object.keys(prev).length !== Object.keys(projMap).length) {
            projectedNodes.value = projMap
          }
        }

        renderer.render(scene, camera)
      }
      animate()

      // 9. Resize observer
      resizeObserver = new ResizeObserver(() => {
        if (!containerEl || !cameraRef.value || !rendererRef.value) return
        const newW = containerEl.clientWidth || 800
        const newH = containerEl.clientHeight || 500
        cameraRef.value.aspect = newW / newH
        cameraRef.value.updateProjectionMatrix()
        rendererRef.value.setSize(newW, newH)
      })
      resizeObserver.observe(container)

    } catch (err: any) {
      if (isDisposed) return
      console.error('[useCadModelViewer] Initialization failed:', err)
      error.value = err?.message || 'Failed to initialize WebGL 3D viewer'
    } finally {
      if (!isDisposed) isLoading.value = false
    }
  }

  const loadModel = async (url: string, THREE: any, GLTFLoader: any) => {
    const safeUrl = resolveSameOriginModelUrl(url)
    if (!safeUrl) {
      error.value = 'Refusing to load 3D model from an untrusted (cross-origin) location'
      isLoading.value = false
      throw new Error(error.value)
    }

    const generation = ++loadGeneration
    isLoading.value = true
    error.value = null

    // Sub-resources referenced by the glTF (buffers, textures) must stay same-origin too.
    const manager = new THREE.LoadingManager()
    manager.setURLModifier((resourceUrl: string) => {
      if (resourceUrl.startsWith('data:') || resourceUrl.startsWith('blob:')) return resourceUrl
      return resolveSameOriginModelUrl(resourceUrl, new URL(safeUrl).origin) ?? 'data:,'
    })
    const loader = new GLTFLoader(manager)

    return new Promise<void>((resolve, reject) => {
      loader.load(
        safeUrl,
        (gltf: any) => {
          const model = gltf.scene
          // Disposed, or superseded by a newer load: discard this result entirely.
          if (isDisposed || generation !== loadGeneration || !sceneRef.value) {
            disposeObjectTree(model)
            resolve()
            return
          }

          if (modelRootRef.value) {
            sceneRef.value.remove(modelRootRef.value)
            restoreOriginalMaterials()
            disposeObjectTree(modelRootRef.value)
          }
          meshMap.clear()
          originalMaterials.clear()

          modelRootRef.value = model
          sceneRef.value.add(model)

          const names: string[] = []

          // Index mesh nodes by case-normalized name and remember their own material
          model.traverse((child: any) => {
            if (child.isMesh) {
              const name = child.name || child.parent?.name || 'MeshNode'
              if (!meshMap.has(name.toUpperCase())) names.push(name)
              meshMap.set(name.toUpperCase(), child)
              if (child.material) originalMaterials.set(child.id, child.material)
            }
          })

          availableNodeNames.value = names
          projectedNodes.value = {}
          isLoading.value = false
          applyMaterialHighlights()
          resolve()
        },
        (xhr: ProgressEvent) => {
          if (xhr.lengthComputable && generation === loadGeneration) {
            loadProgress.value = Math.round((xhr.loaded / xhr.total) * 100)
          }
        },
        (err: any) => {
          if (isDisposed || generation !== loadGeneration) {
            resolve()
            return
          }
          console.error('[useCadModelViewer] Model load error:', err)
          error.value = 'Failed to load 3D CAD asset'
          isLoading.value = false
          reject(err)
        }
      )
    })
  }

  const findMeshByNodePath = (nodePathOrSlot: string) => {
    if (!nodePathOrSlot) return null
    return meshMap.get(nodePathOrSlot.toUpperCase()) || null
  }

  const focusComponent = (nodePathOrSlot: string) => {
    const mesh = findMeshByNodePath(nodePathOrSlot)
    if (!mesh || !cameraRef.value || !controlsRef.value || !THREE_NS) return

    const THREE = THREE_NS
    const box = new THREE.Box3().setFromObject(mesh)
    const center = new THREE.Vector3()
    box.getCenter(center)

    const sphere = new THREE.Sphere()
    box.getBoundingSphere(sphere)
    const radius = Math.max(sphere.radius, 0.15)

    // Calculate optimal camera offset vector
    const offset = new THREE.Vector3(radius * 2.2, radius * 1.5, radius * 2.2)
    targetCamPos = center.clone().add(offset)
    targetLookAt = center.clone()
    isAnimatingCamera = true

    // Set selected highlight
    selectComponent(nodePathOrSlot)
  }

  const selectComponent = (nodePathOrSlot: string | null) => {
    selectedNodeName.value = nodePathOrSlot
    applyMaterialHighlights()
  }

  const hoverComponent = (nodePathOrSlot: string | null) => {
    hoveredNodeName.value = nodePathOrSlot
    applyMaterialHighlights()
  }

  /** Puts every mesh back on its own material and disposes all viewer-owned highlight clones. */
  const restoreOriginalMaterials = () => {
    for (const mesh of uniqueMeshes()) {
      const orig = originalMaterials.get(mesh.id)
      if (orig) mesh.material = orig
    }
    overrideMaterials.forEach(m => disposeMaterial(m))
    overrideMaterials.clear()
  }

  const setEmissive = (material: any, color: number, intensity: number) => {
    const list = Array.isArray(material) ? material : [material]
    for (const m of list) {
      if (m?.emissive) {
        m.emissive.setHex(color)
        m.emissiveIntensity = intensity
      }
    }
  }

  const setWireframe = (material: any, on: boolean) => {
    const list = Array.isArray(material) ? material : [material]
    for (const m of list) if (m) m.wireframe = on
  }

  /**
   * Re-applies selection / hover / status highlighting. Each mesh holds at most one viewer-owned
   * clone at a time; the previous clone is disposed before it is replaced, so repeated hovering
   * does not leak GPU programs or textures.
   */
  const applyMaterialHighlights = () => {
    if (!modelRootRef.value) return
    const selected = selectedNodeName.value?.toUpperCase() ?? null
    const hovered = hoveredNodeName.value?.toUpperCase() ?? null
    const statuses = nodeStatuses.value

    for (const mesh of uniqueMeshes()) {
      const orig = originalMaterials.get(mesh.id)
      if (!orig) continue
      const name = (mesh.name || '').toUpperCase()
      const status = isStatusOverlayEnabled.value ? statuses[name] : undefined

      let emissive: { color: number; intensity: number } | null = null
      if (selected && name === selected) emissive = { color: 0x06b6d4, intensity: 0.95 }
      else if (hovered && name === hovered) emissive = { color: 0xf59e0b, intensity: 0.65 }
      else if (status) emissive = STATUS_EMISSIVE[status]

      const previousOverride = overrideMaterials.get(mesh.id)
      if (emissive) {
        const next = cloneMaterial(orig)
        setEmissive(next, emissive.color, emissive.intensity)
        setWireframe(next, isWireframe.value)
        mesh.material = next
        overrideMaterials.set(mesh.id, next)
      } else {
        setWireframe(orig, isWireframe.value)
        mesh.material = orig
        overrideMaterials.delete(mesh.id)
      }
      if (previousOverride) disposeMaterial(previousOverride)
    }
  }

  /** Supplies per-node health derived from real component data (keys are CAD node names). */
  const setNodeStatuses = (statuses: Record<string, CadNodeStatus>) => {
    const normalized: Record<string, CadNodeStatus> = {}
    for (const [k, v] of Object.entries(statuses || {})) normalized[k.toUpperCase()] = v
    nodeStatuses.value = normalized
    if (isStatusOverlayEnabled.value) applyMaterialHighlights()
  }

  const toggleWireframe = () => {
    isWireframe.value = !isWireframe.value
    for (const mesh of uniqueMeshes()) setWireframe(mesh.material, isWireframe.value)
    originalMaterials.forEach(m => setWireframe(m, isWireframe.value))
  }

  const toggleStatusOverlay = () => {
    isStatusOverlayEnabled.value = !isStatusOverlayEnabled.value
    applyMaterialHighlights()
  }

  const resetCamera = () => {
    if (!cameraRef.value || !controlsRef.value || !THREE_NS) return
    targetCamPos = new THREE_NS.Vector3(1.9, 1.7, 2.1)
    targetLookAt = new THREE_NS.Vector3(0, 0.95, 0)
    isAnimatingCamera = true
    selectedNodeName.value = null
    applyMaterialHighlights()
  }

  const raycastFromEvent = (event: MouseEvent): string | null => {
    if (!canvasEl || !cameraRef.value || !modelRootRef.value || !THREE_NS) return null
    const THREE = THREE_NS

    const rect = canvasEl.getBoundingClientRect()
    if (!rect.width || !rect.height) return null
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1

    const raycaster = new THREE.Raycaster()
    raycaster.setFromCamera(new THREE.Vector2(x, y), cameraRef.value)

    const intersects = raycaster.intersectObjects(uniqueMeshes(), true)
    if (intersects.length > 0) {
      const hit = intersects[0].object
      return hit.name || hit.parent?.name || null
    }
    return null
  }

  const dispose = () => {
    isDisposed = true
    loadGeneration++
    if (animationFrameId !== null) {
      cancelAnimationFrame(animationFrameId)
      animationFrameId = null
    }
    if (resizeObserver) {
      resizeObserver.disconnect()
      resizeObserver = null
    }
    if (controlsRef.value) {
      controlsRef.value.dispose()
      controlsRef.value = null
    }
    // Restore originals before disposing the tree so every material is disposed exactly once.
    restoreOriginalMaterials()
    if (sceneRef.value) {
      disposeObjectTree(sceneRef.value)
      sceneRef.value.clear?.()
    }
    if (rendererRef.value) {
      rendererRef.value.dispose()
      rendererRef.value = null
    }
    meshMap.clear()
    originalMaterials.clear()
    sceneRef.value = null
    cameraRef.value = null
    modelRootRef.value = null
    containerEl = null
    canvasEl = null
  }

  if (getCurrentInstance()) {
    onUnmounted(() => {
      dispose()
    })
  }

  return {
    isLoading,
    loadProgress,
    error,
    isWireframe,
    isStatusOverlayEnabled,
    hoveredNodeName,
    selectedNodeName,
    availableNodeNames,
    projectedNodes,
    initViewer,
    loadModel,
    focusComponent,
    selectComponent,
    hoverComponent,
    setNodeStatuses,
    toggleWireframe,
    toggleStatusOverlay,
    resetCamera,
    raycastFromEvent,
    dispose
  }
}
