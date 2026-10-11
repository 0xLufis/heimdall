import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useLocalStorage } from '@vueuse/core'

export interface MachineTwinNode {
  id: string
  name: string
  displayName?: string
  customIdentifier: string
  machineType?: string
  groupId?: string
  status: 'active' | 'inactive' | 'maintenance' | 'decommissioned' | 'dismantled'
  operationalState?: 'Running' | 'Idle' | 'Warning' | 'Faulted' | 'Maintenance' | string
  criticality?: 'Tier1_Critical' | 'Tier2_Major' | 'Tier3_Standard' | string
  technologies?: string[]
  controllers?: Array<{ id: string; hostname: string; ipAddress?: string; isOnline?: boolean }>
  responsibleTeams?: Array<{ name: string }>
  preferredTechnicianName?: string
  pinnedObjectHandle?: string
  spatialCoordinates?: { x: number; y: number; z?: number }
  telemetry?: {
    vibrationRms: number // mm/s
    temperature: number // °C
    motorCurrent: number // A
    pressure: number // bar
    cycleTime: number // s
    healthScore: number // 0-100%
    lastHeartbeat: string
  }
}

export interface DigitalTwinWidgetConfig {
  id: string
  title: string
  description: string
  category: 'core' | 'analytics' | 'telemetry' | 'maintenance' | 'edge'
  visible: boolean
  order: number
}

export type DigitalTwinLayoutPreset = 'command-center' | 'spatial-focus' | 'maintenance-lead' | 'controls-engineer' | 'minimalist'

export type DigitalTwinDensity = 'compact' | 'comfortable' | 'spacious'

export type DigitalTwinThemeAccent = 'graphite' | 'emerald' | 'teal' | 'amber' | 'rose' | 'blue'

const DEFAULT_WIDGETS: DigitalTwinWidgetConfig[] = [
  { id: 'kpi-matrix', title: 'Plant Health & Operational KPIs', description: 'Real-time OEE, Line Stops, and Fleet Availability', category: 'core', visible: true, order: 1 },
  { id: 'twin-canvas', title: 'Topographic Plant Digital Twin', description: 'Spatial multi-line factory floor visualization', category: 'core', visible: true, order: 2 },
  { id: 'fleet-matrix', title: 'Connected Machine Fleet Matrix', description: 'High-density multi-status machine card directory', category: 'core', visible: true, order: 3 },
  { id: 'live-waveforms', title: 'Real-time Telemetry & SPC Streams', description: 'Vibration envelope, thermal sparklines and tolerance limits', category: 'telemetry', visible: true, order: 4 },
  { id: 'incident-radar', title: 'Line Stop Radar & Incident Queue', description: 'Emergency downtime alerts, reaction times and tickets', category: 'maintenance', visible: true, order: 5 },
  { id: 'tech-breakdown', title: 'Technology Discipline Breakdown', description: 'Assembly, Robotics, Welding, and SMT cell distribution', category: 'analytics', visible: true, order: 6 },
  { id: 'edge-status', title: 'Edge IPC Fleet & OT Infrastructure', description: 'Industrial controller health, CPU/RAM and Copia sync', category: 'edge', visible: true, order: 7 }
]

export function useDigitalTwin() {
  const machines = ref<MachineTwinNode[]>([])
  const tickets = ref<any[]>([])
  const controllers = ref<any[]>([])
  const isLoading = ref(true)
  const error = ref<string | null>(null)

  // Selection & Layer states
  const selectedMachineId = ref<string | null>('22222222-2222-2222-2222-222222222222') // default to OP20-Weld
  const activeLayer = ref<'status' | 'thermal' | 'network' | 'incidents'>('status')

  // Search & Filtering states
  const searchQuery = ref('')
  const selectedLine = ref('all')
  const selectedStatus = ref('all')
  const selectedTechnology = ref('all')
  const selectedCriticality = ref('all')

  // User Customizability & Personalization Preferences (persisted in localStorage)
  const layoutPreset = useLocalStorage<DigitalTwinLayoutPreset>('heimdall_twin_layout_preset', 'command-center')
  const density = useLocalStorage<DigitalTwinDensity>('heimdall_twin_density', 'comfortable')
  const themeAccent = useLocalStorage<DigitalTwinThemeAccent>('heimdall_twin_theme_accent', 'graphite')
  const autoRefreshSeconds = useLocalStorage<number>('heimdall_twin_refresh_sec', 2)
  const isSimulationActive = useLocalStorage<boolean>('heimdall_twin_simulation_active', true)
  const widgetsConfig = useLocalStorage<DigitalTwinWidgetConfig[]>('heimdall_twin_widgets', DEFAULT_WIDGETS)

  // Simulated telemetry dictionary updated periodically
  const telemetryNoiseMap = ref<Record<string, {
    vibrationRms: number
    temperature: number
    motorCurrent: number
    pressure: number
    cycleTime: number
    healthScore: number
    waveHistory: number[]
  }>>({})

  // Fetch initial data
  const fetchData = async () => {
    try {
      isLoading.value = true
      error.value = null

      const [machinesRes, ticketsRes, clientsRes] = await Promise.all([
        $fetch<any[]>('/api/machines', { query: { includeDecommissioned: true } }).catch(() => []),
        $fetch<any[]>('/api/tickets').catch(() => []),
        $fetch<any[]>('/api/clients').catch(() => [])
      ])

      const rawMachines = Array.isArray(machinesRes) ? machinesRes : []
      tickets.value = Array.isArray(ticketsRes) ? ticketsRes : []
      controllers.value = Array.isArray(clientsRes) ? clientsRes : []

      // Normalize machines into MachineTwinNode
      machines.value = rawMachines.map((m, idx) => {
        // Derive initial simulated telemetry values based on machine type & status
        const isMaint = m.status === 'maintenance'
        const isDecom = m.status === 'decommissioned' || m.status === 'dismantled'
        const isInactive = m.status === 'inactive'
        const isWarning = m.customIdentifier?.includes('OP30') || m.customIdentifier?.includes('OP50')

        const baseVib = isWarning ? 3.4 : (isMaint ? 0.2 : (isDecom ? 0.0 : 1.2))
        const baseTemp = isWarning ? 68.5 : (isMaint ? 24.0 : (isDecom ? 20.0 : 42.0))
        const baseCurrent = isMaint || isDecom ? 0 : 8.5
        const basePressure = isMaint || isDecom ? 0 : 6.1
        const baseCycle = isMaint || isDecom ? 0 : (24.0 + (idx % 5) * 2)
        const baseHealth = isDecom ? 0 : (isMaint ? 45 : (isWarning ? 62 : 96))

        if (!telemetryNoiseMap.value[m.id]) {
          telemetryNoiseMap.value[m.id] = {
            vibrationRms: baseVib,
            temperature: baseTemp,
            motorCurrent: baseCurrent,
            pressure: basePressure,
            cycleTime: baseCycle,
            healthScore: baseHealth,
            waveHistory: Array.from({ length: 15 }, () => baseVib + (Math.random() * 0.4 - 0.2))
          }
        }

        const telem = telemetryNoiseMap.value[m.id]

        // Assign spatial coordinates if not present
        const lineIdx = m.groupId?.toLowerCase().includes('2') ? 1 : 0
        const posX = 80 + (idx % 6) * 140
        const posY = 100 + lineIdx * 160

        return {
          id: m.id,
          name: m.name || m.customIdentifier,
          displayName: m.displayName || m.name,
          customIdentifier: m.customIdentifier,
          machineType: m.machineType || 'Assembly',
          groupId: m.groupId || 'Line 1',
          status: m.status || 'active',
          operationalState: isDecom ? 'Decommissioned' : (isMaint ? 'Maintenance' : (isWarning ? 'Warning' : (isInactive ? 'Idle' : 'Running'))),
          criticality: m.customIdentifier?.includes('OP20') ? 'Tier1_Critical' : (m.customIdentifier?.includes('OP10') ? 'Tier2_Major' : 'Tier3_Standard'),
          technologies: m.technologies && m.technologies.length ? m.technologies : [m.machineType || 'Assembly'],
          controllers: m.controllers || [],
          responsibleTeams: m.responsibleTeams || [],
          preferredTechnicianName: m.preferredTechnicianName || 'Gábor Varga',
          pinnedObjectHandle: m.pinnedObjectHandle || `CAD-${m.customIdentifier}`,
          spatialCoordinates: { x: posX, y: posY },
          telemetry: {
            vibrationRms: telem.vibrationRms,
            temperature: telem.temperature,
            motorCurrent: telem.motorCurrent,
            pressure: telem.pressure,
            cycleTime: telem.cycleTime,
            healthScore: telem.healthScore,
            lastHeartbeat: new Date().toISOString()
          }
        }
      })

      if (!selectedMachineId.value && machines.value.length > 0) {
        selectedMachineId.value = machines.value[0].id
      }
    } catch (err: any) {
      error.value = err?.message || 'Failed to initialize Digital Twin telemetry data'
    } finally {
      isLoading.value = false
    }
  }

  // Periodic Telemetry Simulator Pulse
  let simTimer: any = null
  const pulseTelemetry = () => {
    if (!isSimulationActive.value) return

    for (const m of machines.value) {
      const state = telemetryNoiseMap.value[m.id]
      if (!state || m.status === 'decommissioned' || m.status === 'dismantled') continue

      if (m.status === 'active') {
        const deltaVib = (Math.random() - 0.5) * 0.15
        state.vibrationRms = Math.max(0.4, Number((state.vibrationRms + deltaVib).toFixed(2)))
        const deltaTemp = (Math.random() - 0.5) * 0.3
        state.temperature = Math.max(25, Number((state.temperature + deltaTemp).toFixed(1)))
        const deltaCurr = (Math.random() - 0.5) * 0.2
        state.motorCurrent = Math.max(1, Number((state.motorCurrent + deltaCurr).toFixed(2)))

        state.waveHistory.push(state.vibrationRms)
        if (state.waveHistory.length > 20) {
          state.waveHistory.shift()
        }

        if (m.telemetry) {
          m.telemetry.vibrationRms = state.vibrationRms
          m.telemetry.temperature = state.temperature
          m.telemetry.motorCurrent = state.motorCurrent
          m.telemetry.lastHeartbeat = new Date().toISOString()
        }
      }
    }
  }

  // Unique Production Lines
  const productionLines = computed(() => {
    const lines = new Set<string>()
    machines.value.forEach(m => {
      if (m.groupId) lines.add(m.groupId)
    })
    return Array.from(lines).sort()
  })

  // Unique Technology disciplines
  const technologyDisciplines = computed(() => {
    const techs = new Set<string>()
    machines.value.forEach(m => {
      if (m.machineType) techs.add(m.machineType)
      m.technologies?.forEach(t => techs.add(t))
    })
    return Array.from(techs).sort()
  })

  // Selected Machine Twin Node
  const selectedMachine = computed(() => {
    if (!selectedMachineId.value) return null
    return machines.value.find(m => m.id === selectedMachineId.value) || null
  })

  // Active Line Stops & Urgent Tickets
  const activeLineStopTickets = computed(() => {
    return tickets.value.filter(t => t.isLineStop === true && t.status !== 'Closed' && t.status !== 'Resolved')
  })

  const totalOpenTicketsCount = computed(() => {
    return tickets.value.filter(t => t.status !== 'Closed' && t.status !== 'Resolved').length
  })

  // Filtered Machines according to Search & Facets
  const filteredMachines = computed(() => {
    const q = searchQuery.value.trim().toLowerCase()

    return machines.value.filter(m => {
      // Query filter
      if (q) {
        const matchesQuery =
          m.name.toLowerCase().includes(q) ||
          m.customIdentifier.toLowerCase().includes(q) ||
          (m.displayName && m.displayName.toLowerCase().includes(q)) ||
          (m.machineType && m.machineType.toLowerCase().includes(q)) ||
          (m.pinnedObjectHandle && m.pinnedObjectHandle.toLowerCase().includes(q)) ||
          (m.preferredTechnicianName && m.preferredTechnicianName.toLowerCase().includes(q))
        if (!matchesQuery) return false
      }

      // Line filter
      if (selectedLine.value !== 'all' && m.groupId !== selectedLine.value) {
        return false
      }

      // Status filter
      if (selectedStatus.value !== 'all') {
        if (selectedStatus.value === 'line-stop') {
          // Check if associated with an active line stop ticket
          const hasLineStop = activeLineStopTickets.value.some(t => t.machineId === m.id || t.stationId === m.id || t.customIdentifier === m.customIdentifier)
          if (!hasLineStop) return false
        } else if (m.status !== selectedStatus.value && m.operationalState?.toLowerCase() !== selectedStatus.value.toLowerCase()) {
          return false
        }
      }

      // Technology filter
      if (selectedTechnology.value !== 'all') {
        const hasTech = m.machineType === selectedTechnology.value || m.technologies?.includes(selectedTechnology.value)
        if (!hasTech) return false
      }

      // Criticality filter
      if (selectedCriticality.value !== 'all' && m.criticality !== selectedCriticality.value) {
        return false
      }

      return true
    })
  })

  // Plant Health Metrics Aggregation
  const plantMetrics = computed(() => {
    const total = machines.value.length || 1
    const active = machines.value.filter(m => m.status === 'active' && m.operationalState === 'Running').length
    const warning = machines.value.filter(m => m.operationalState === 'Warning').length
    const maintenance = machines.value.filter(m => m.status === 'maintenance').length
    const decommissioned = machines.value.filter(m => m.status === 'decommissioned' || m.status === 'dismantled').length
    const lineStops = activeLineStopTickets.value.length

    // Overall Equipment Effectiveness (OEE) synthetic score
    const availability = total > 0 ? ((active + warning * 0.7) / (total - decommissioned || 1)) * 100 : 0
    const performance = 98.4
    const quality = 99.2
    const oee = Math.min(99.9, Math.max(0, (availability * performance * quality) / 10000))

    // Average fleet temperature & vibration
    let avgTemp = 0
    let avgVib = 0
    let countActive = 0
    machines.value.forEach(m => {
      if (m.telemetry && m.status === 'active') {
        avgTemp += m.telemetry.temperature
        avgVib += m.telemetry.vibrationRms
        countActive++
      }
    })

    return {
      totalMachines: total,
      activeMachines: active,
      warningMachines: warning,
      maintenanceMachines: maintenance,
      decommissionedMachines: decommissioned,
      lineStopsCount: lineStops,
      oeePercent: Number(oee.toFixed(1)),
      availabilityPercent: Number(availability.toFixed(1)),
      avgTemperature: countActive ? Number((avgTemp / countActive).toFixed(1)) : 41.5,
      avgVibrationRms: countActive ? Number((avgVib / countActive).toFixed(2)) : 1.25,
      mttrMinutes: 18.5,
      mtbfHours: 412.0
    }
  })

  // Selection actions
  const selectMachine = (id: string) => {
    selectedMachineId.value = id
  }

  const setLayer = (layer: 'status' | 'thermal' | 'network' | 'incidents') => {
    activeLayer.value = layer
  }

  // Layout customizer actions
  const setLayoutPreset = (preset: DigitalTwinLayoutPreset) => {
    layoutPreset.value = preset
    if (preset === 'command-center') {
      widgetsConfig.value = widgetsConfig.value.map(w => ({ ...w, visible: true }))
    } else if (preset === 'spatial-focus') {
      widgetsConfig.value = widgetsConfig.value.map(w => ({
        ...w,
        visible: ['twin-canvas', 'kpi-matrix', 'fleet-matrix'].includes(w.id)
      }))
    } else if (preset === 'maintenance-lead') {
      widgetsConfig.value = widgetsConfig.value.map(w => ({
        ...w,
        visible: ['kpi-matrix', 'incident-radar', 'fleet-matrix', 'twin-canvas'].includes(w.id)
      }))
    } else if (preset === 'controls-engineer') {
      widgetsConfig.value = widgetsConfig.value.map(w => ({
        ...w,
        visible: ['kpi-matrix', 'live-waveforms', 'edge-status', 'twin-canvas'].includes(w.id)
      }))
    } else if (preset === 'minimalist') {
      widgetsConfig.value = widgetsConfig.value.map(w => ({
        ...w,
        visible: ['kpi-matrix', 'twin-canvas'].includes(w.id)
      }))
    }
  }

  const toggleWidget = (widgetId: string) => {
    const w = widgetsConfig.value.find(item => item.id === widgetId)
    if (w) {
      w.visible = !w.visible
    }
  }

  const setDensity = (val: DigitalTwinDensity) => {
    density.value = val
  }

  const setThemeAccent = (val: DigitalTwinThemeAccent) => {
    themeAccent.value = val
  }

  const resetFilters = () => {
    searchQuery.value = ''
    selectedLine.value = 'all'
    selectedStatus.value = 'all'
    selectedTechnology.value = 'all'
    selectedCriticality.value = 'all'
  }

  // Lifecycle
  onMounted(() => {
    fetchData()
    simTimer = setInterval(pulseTelemetry, Math.max(1000, autoRefreshSeconds.value * 1000))
  })

  onUnmounted(() => {
    if (simTimer) clearInterval(simTimer)
  })

  watch(autoRefreshSeconds, (sec) => {
    if (simTimer) clearInterval(simTimer)
    simTimer = setInterval(pulseTelemetry, Math.max(1000, sec * 1000))
  })

  return {
    machines,
    tickets,
    controllers,
    isLoading,
    error,
    selectedMachineId,
    selectedMachine,
    activeLayer,
    searchQuery,
    selectedLine,
    selectedStatus,
    selectedTechnology,
    selectedCriticality,
    productionLines,
    technologyDisciplines,
    filteredMachines,
    plantMetrics,
    activeLineStopTickets,
    totalOpenTicketsCount,
    telemetryNoiseMap,
    layoutPreset,
    density,
    themeAccent,
    autoRefreshSeconds,
    isSimulationActive,
    widgetsConfig,
    fetchData,
    selectMachine,
    setLayer,
    setLayoutPreset,
    toggleWidget,
    setDensity,
    setThemeAccent,
    resetFilters
  }
}
