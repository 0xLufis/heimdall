import { ref, shallowRef, onUnmounted } from 'vue'

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

  // Three.js runtime references (shallow to avoid Vue reactivity proxy overhead on WebGL objects)
  const sceneRef = shallowRef<any>(null)
  const cameraRef = shallowRef<any>(null)
  const rendererRef = shallowRef<any>(null)
  const controlsRef = shallowRef<any>(null)
  const modelRootRef = shallowRef<any>(null)
  const meshMap = new Map<string, any>()
  const originalMaterials = new Map<string, any>()

  let animationFrameId: number | null = null
  let resizeObserver: ResizeObserver | null = null
  let containerEl: HTMLElement | null = null
  let canvasEl: HTMLCanvasElement | null = null

  // Camera animation state
  let isAnimatingCamera = false
  let targetCamPos: any = null
  let targetLookAt: any = null
  let currentLookAt: any = null

  const initViewer = async (container: HTMLElement, canvas: HTMLCanvasElement, options: CadViewerOptions = {}) => {
    if (typeof window === 'undefined') return

    containerEl = container
    canvasEl = canvas
    isLoading.value = true
    error.value = null

    try {
      // Dynamic imports for Three.js to guarantee SSR safety
      const THREE = await import('three')
      const { OrbitControls } = await import('three/examples/jsm/controls/OrbitControls.js')
      const { GLTFLoader } = await import('three/examples/jsm/loaders/GLTFLoader.js')

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
      const ambientLight = new THREE.AmbientLight(0xffffff, 1.4)
      scene.add(ambientLight)

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
      const modelUrl = options.modelUrl || '/sample/station_digital_twin.glb'
      await loadModel(modelUrl, THREE, GLTFLoader)

      // 8. Render loop
      const animate = () => {
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

        // Update 3D screen projection coordinates for HUD overlays
        if (containerEl && camera && meshMap.size > 0) {
          const w = containerEl.clientWidth || 800
          const h = containerEl.clientHeight || 500
          const projMap: Record<string, Projected3dNode> = {}
          const processedNames = new Set<string>()

          for (const nodeName of availableNodeNames.value) {
            if (processedNames.has(nodeName)) continue
            processedNames.add(nodeName)

            const mesh = meshMap.get(nodeName)
            if (!mesh) continue

            const box = new THREE.Box3().setFromObject(mesh)
            const center = new THREE.Vector3()
            box.getCenter(center)
            center.y = box.max.y + 0.08 // Float above top of mesh bounding box

            const projected = center.clone().project(camera)
            const isVisible =
              projected.z > -1.0 &&
              projected.z < 1.0 &&
              projected.x >= -1.05 &&
              projected.x <= 1.05 &&
              projected.y >= -1.05 &&
              projected.y <= 1.05

            const screenX = (projected.x * 0.5 + 0.5) * w
            const screenY = (-projected.y * 0.5 + 0.5) * h

            projMap[nodeName] = {
              name: nodeName,
              x: Math.round(screenX),
              y: Math.round(screenY),
              isVisible,
              worldPos: [Number(center.x.toFixed(2)), Number(center.y.toFixed(2)), Number(center.z.toFixed(2))]
            }
          }
          projectedNodes.value = projMap
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
      console.error('[useCadModelViewer] Initialization failed:', err)
      error.value = err?.message || 'Failed to initialize WebGL 3D viewer'
    } finally {
      isLoading.value = false
    }
  }

  const loadModel = async (url: string, THREE: any, GLTFLoader: any) => {
    isLoading.value = true
    error.value = null
    meshMap.clear()
    originalMaterials.clear()

    const loader = new GLTFLoader()

    return new Promise<void>((resolve, reject) => {
      loader.load(
        url,
        (gltf: any) => {
          if (modelRootRef.value && sceneRef.value) {
            sceneRef.value.remove(modelRootRef.value)
          }

          const model = gltf.scene
          modelRootRef.value = model
          sceneRef.value.add(model)

          const names: string[] = []

          // Traverse and index all mesh nodes by exact name and normalized slot ID
          model.traverse((child: any) => {
            if (child.isMesh) {
              const name = child.name || child.parent?.name || 'MeshNode'
              names.push(name)
              meshMap.set(name, child)
              meshMap.set(name.toUpperCase(), child)
              meshMap.set(name.toLowerCase(), child)

              // Clone and cache original material
              if (Array.isArray(child.material)) {
                originalMaterials.set(child.id, child.material.map((m: any) => m.clone()))
              } else if (child.material) {
                originalMaterials.set(child.id, child.material.clone())
              }
            }
          })

          availableNodeNames.value = names
          isLoading.value = false
          resolve()
        },
        (xhr: ProgressEvent) => {
          if (xhr.lengthComputable) {
            loadProgress.value = Math.round((xhr.loaded / xhr.total) * 100)
          }
        },
        (err: any) => {
          console.error('[useCadModelViewer] Model load error:', err)
          error.value = `Failed to load 3D CAD asset from '${url}'`
          isLoading.value = false
          reject(err)
        }
      )
    })
  }

  const findMeshByNodePath = (nodePathOrSlot: string) => {
    if (!nodePathOrSlot) return null
    return (
      meshMap.get(nodePathOrSlot) ||
      meshMap.get(nodePathOrSlot.toUpperCase()) ||
      meshMap.get(nodePathOrSlot.toLowerCase()) ||
      null
    )
  }

  const focusComponent = async (nodePathOrSlot: string) => {
    const mesh = findMeshByNodePath(nodePathOrSlot)
    if (!mesh || !cameraRef.value || !controlsRef.value) return

    const THREE = await import('three')
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

  const selectComponent = async (nodePathOrSlot: string | null) => {
    selectedNodeName.value = nodePathOrSlot
    applyMaterialHighlights()
  }

  const hoverComponent = async (nodePathOrSlot: string | null) => {
    hoveredNodeName.value = nodePathOrSlot
    applyMaterialHighlights()
  }

  const applyMaterialHighlights = async () => {
    if (!modelRootRef.value) return
    const THREE = await import('three')

    meshMap.forEach((mesh) => {
      const orig = originalMaterials.get(mesh.id)
      if (!orig) return

      const isSelected = selectedNodeName.value && (
        mesh.name === selectedNodeName.value ||
        mesh.name.toUpperCase() === selectedNodeName.value.toUpperCase()
      )
      const isHovered = hoveredNodeName.value && (
        mesh.name === hoveredNodeName.value ||
        mesh.name.toUpperCase() === hoveredNodeName.value.toUpperCase()
      )

      if (isSelected) {
        // Glowing cyan outline / emissive highlight
        mesh.material = orig.clone()
        mesh.material.emissive = new THREE.Color(0x06b6d4)
        mesh.material.emissiveIntensity = 0.95
        mesh.material.roughness = 0.2
      } else if (isHovered) {
        // Subtle amber glow
        mesh.material = orig.clone()
        mesh.material.emissive = new THREE.Color(0xf59e0b)
        mesh.material.emissiveIntensity = 0.65
      } else if (isStatusOverlayEnabled.value) {
        // Health state coloring mode
        mesh.material = orig.clone()
        // Default healthy green emissive
        mesh.material.emissive = new THREE.Color(0x10b981)
        mesh.material.emissiveIntensity = 0.35
      } else {
        // Restore standard pristine material
        mesh.material = orig.clone()
        mesh.material.wireframe = isWireframe.value
      }
    })
  }

  const toggleWireframe = () => {
    isWireframe.value = !isWireframe.value
    meshMap.forEach((mesh) => {
      if (mesh.material) {
        if (Array.isArray(mesh.material)) {
          mesh.material.forEach((m: any) => { m.wireframe = isWireframe.value })
        } else {
          mesh.material.wireframe = isWireframe.value
        }
      }
    })
  }

  const toggleStatusOverlay = () => {
    isStatusOverlayEnabled.value = !isStatusOverlayEnabled.value
    applyMaterialHighlights()
  }

  const resetCamera = () => {
    if (!cameraRef.value || !controlsRef.value) return
    targetCamPos = new (cameraRef.value.position.constructor)(1.9, 1.7, 2.1)
    targetLookAt = new (cameraRef.value.position.constructor)(0, 0.95, 0)
    isAnimatingCamera = true
    selectedNodeName.value = null
    applyMaterialHighlights()
  }

  const raycastFromEvent = async (event: MouseEvent): Promise<string | null> => {
    if (!canvasEl || !cameraRef.value || !modelRootRef.value) return null
    const THREE = await import('three')

    const rect = canvasEl.getBoundingClientRect()
    const x = ((event.clientX - rect.left) / rect.width) * 2 - 1
    const y = -((event.clientY - rect.top) / rect.height) * 2 + 1

    const raycaster = new THREE.Raycaster()
    raycaster.setFromCamera(new THREE.Vector2(x, y), cameraRef.value)

    const meshes: any[] = []
    meshMap.forEach(m => { if (!meshes.includes(m)) meshes.push(m) })

    const intersects = raycaster.intersectObjects(meshes, true)
    if (intersects.length > 0) {
      const hit = intersects[0].object
      return hit.name || hit.parent?.name || null
    }
    return null
  }

  const dispose = () => {
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
    if (rendererRef.value) {
      rendererRef.value.dispose()
      rendererRef.value = null
    }
    meshMap.clear()
    originalMaterials.clear()
    sceneRef.value = null
    cameraRef.value = null
    modelRootRef.value = null
  }

  onUnmounted(() => {
    dispose()
  })

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
    toggleWireframe,
    toggleStatusOverlay,
    resetCamera,
    raycastFromEvent,
    dispose
  }
}
