import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { ref } from 'vue'
import { useDigitalTwin, type MachineTwinNode } from '../../../src/frontend/web/app/composables/useDigitalTwin'
import DigitalTwinCanvas from '../../../src/frontend/web/app/components/twin/DigitalTwinCanvas.vue'
import DigitalTwinInspector from '../../../src/frontend/web/app/components/twin/DigitalTwinInspector.vue'
import DigitalTwinKpis from '../../../src/frontend/web/app/components/twin/DigitalTwinKpis.vue'
import DigitalTwinWaveform from '../../../src/frontend/web/app/components/twin/DigitalTwinWaveform.vue'
import DigitalTwinIncidentRadar from '../../../src/frontend/web/app/components/twin/DigitalTwinIncidentRadar.vue'
import DigitalTwinMachineGrid from '../../../src/frontend/web/app/components/twin/DigitalTwinMachineGrid.vue'
import DigitalTwinTechnologyMatrix from '../../../src/frontend/web/app/components/twin/DigitalTwinTechnologyMatrix.vue'
import DigitalTwinEdgeFleet from '../../../src/frontend/web/app/components/twin/DigitalTwinEdgeFleet.vue'
import DigitalTwinCustomizerModal from '../../../src/frontend/web/app/components/twin/DigitalTwinCustomizerModal.vue'
import DigitalTwinSearchBar from '../../../src/frontend/web/app/components/twin/DigitalTwinSearchBar.vue'

const mockMachines: MachineTwinNode[] = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'OP10-Load',
    displayName: 'Raw Material Infeed Station',
    customIdentifier: 'LINE1-OP10',
    machineType: 'Assembly',
    groupId: 'Line 1',
    status: 'active',
    operationalState: 'Running',
    criticality: 'Tier2_Major',
    technologies: ['Assembly'],
    controllers: [{ id: 'c-1', hostname: 'IPC-L1-01', ipAddress: '192.168.1.110', isOnline: true }],
    pinnedObjectHandle: 'CAD-LINE1-OP10',
    telemetry: {
      vibrationRms: 1.15,
      temperature: 38.5,
      motorCurrent: 7.2,
      pressure: 6.0,
      cycleTime: 24.0,
      healthScore: 98,
      lastHeartbeat: new Date().toISOString()
    }
  },
  {
    id: '22222222-2222-2222-2222-222222222222',
    name: 'OP20-Weld',
    displayName: 'Laser Robotic Welding Cell',
    customIdentifier: 'LINE1-OP20',
    machineType: 'Welding',
    groupId: 'Line 1',
    status: 'active',
    operationalState: 'Running',
    criticality: 'Tier1_Critical',
    technologies: ['Welding', 'Robotics'],
    controllers: [{ id: 'c-2', hostname: 'IPC-L1-02', ipAddress: '192.168.1.111', isOnline: true }],
    pinnedObjectHandle: 'CAD-LINE1-OP20',
    telemetry: {
      vibrationRms: 1.45,
      temperature: 52.0,
      motorCurrent: 12.4,
      pressure: 6.2,
      cycleTime: 28.0,
      healthScore: 92,
      lastHeartbeat: new Date().toISOString()
    }
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    name: 'OP30-Vision',
    displayName: 'Cognex AI Inspection Chamber',
    customIdentifier: 'LINE2-OP30',
    machineType: 'Test',
    groupId: 'Line 2',
    status: 'maintenance',
    operationalState: 'Maintenance',
    criticality: 'Tier3_Standard',
    technologies: ['Test'],
    controllers: [{ id: 'c-3', hostname: 'IPC-L2-03', ipAddress: '192.168.1.120', isOnline: true }],
    pinnedObjectHandle: 'CAD-LINE2-OP30',
    telemetry: {
      vibrationRms: 0.1,
      temperature: 24.0,
      motorCurrent: 0,
      pressure: 0,
      cycleTime: 0,
      healthScore: 50,
      lastHeartbeat: new Date().toISOString()
    }
  }
]

const mockTickets = [
  {
    id: 't-1',
    ticketNumber: 'TICK-101',
    title: 'Spindle Harmonic Deviation',
    description: 'Vibration spike on high speed run',
    priority: 'Critical',
    status: 'Open',
    isLineStop: true,
    lineStopDurationMinutes: 18.5,
    machineId: '22222222-2222-2222-2222-222222222222',
    customIdentifier: 'LINE1-OP20',
    assignedTo: 'Gábor Varga'
  }
]

const mockControllers = [
  { id: 'c-1', hostname: 'IPC-L1-01', ipAddress: '192.168.1.110', telemetry: { cpuUsagePercent: 12, ramUsagePercent: 44 } },
  { id: 'c-2', hostname: 'IPC-L1-02', ipAddress: '192.168.1.111', telemetry: { cpuUsagePercent: 22, ramUsagePercent: 55 } }
]

describe('Canonical Digital Twin Web App Test Suite', () => {
  beforeEach(() => {
    vi.stubGlobal('useRouter', () => ({ push: vi.fn() }))
    vi.stubGlobal('$fetch', vi.fn((url: string) => {
      if (url.includes('/api/machines')) return Promise.resolve(mockMachines)
      if (url.includes('/api/tickets')) return Promise.resolve(mockTickets)
      if (url.includes('/api/clients')) return Promise.resolve(mockControllers)
      return Promise.resolve([])
    }))
  })

  describe('useDigitalTwin Composable Operations', () => {
    it('initializes machines, production lines, and plant health metrics', async () => {
      const dt = useDigitalTwin()
      await dt.fetchData()

      expect(dt.machines.value.length).toBe(3)
      expect(dt.productionLines.value).toContain('Line 1')
      expect(dt.productionLines.value).toContain('Line 2')
      expect(dt.technologyDisciplines.value).toContain('Assembly')
      expect(dt.technologyDisciplines.value).toContain('Welding')
    })

    it('filters machine fleet by query, line, status, and technology', async () => {
      const dt = useDigitalTwin()
      await dt.fetchData()

      // Query filter
      dt.searchQuery.value = 'Weld'
      expect(dt.filteredMachines.value.length).toBe(1)
      expect(dt.filteredMachines.value[0].customIdentifier).toBe('LINE1-OP20')

      // Reset
      dt.resetFilters()
      expect(dt.filteredMachines.value.length).toBe(3)

      // Line filter
      dt.selectedLine.value = 'Line 2'
      expect(dt.filteredMachines.value.length).toBe(1)
      expect(dt.filteredMachines.value[0].customIdentifier).toBe('LINE2-OP30')

      // Status filter
      dt.selectedLine.value = 'all'
      dt.selectedStatus.value = 'maintenance'
      expect(dt.filteredMachines.value.length).toBe(1)
      expect(dt.filteredMachines.value[0].status).toBe('maintenance')
    })

    it('switches layout presets and updates widget visibility', () => {
      const dt = useDigitalTwin()

      // Command Center -> All visible
      dt.setLayoutPreset('command-center')
      expect(dt.widgetsConfig.value.every(w => w.visible)).toBe(true)

      // Minimalist -> Only canvas & kpis visible
      dt.setLayoutPreset('minimalist')
      const visibleIds = dt.widgetsConfig.value.filter(w => w.visible).map(w => w.id)
      expect(visibleIds).toContain('twin-canvas')
      expect(visibleIds).toContain('kpi-matrix')
      expect(visibleIds).not.toContain('live-waveforms')
    })
  })

  describe('DigitalTwinCanvas Component', () => {
    it('renders factory lines, station nodes, and flow tracks', () => {
      const wrapper = mount(DigitalTwinCanvas, {
        props: {
          machines: mockMachines,
          selectedMachineId: '22222222-2222-2222-2222-222222222222',
          activeLayer: 'status',
          lineStopCount: 1
        }
      })

      expect(wrapper.text()).toContain('Digital Twin Topography')
      expect(wrapper.text()).toContain('Line 1')
      expect(wrapper.text()).toContain('LINE1-OP10')
      expect(wrapper.text()).toContain('LINE1-OP20')
      expect(wrapper.text()).toContain('LINE2-OP30')
    })

    it('emits select-machine event when node is clicked', async () => {
      const wrapper = mount(DigitalTwinCanvas, {
        props: {
          machines: mockMachines,
          selectedMachineId: null,
          activeLayer: 'status',
          lineStopCount: 0
        }
      })

      const card = wrapper.find('[class*="cursor-pointer"]')
      expect(card.exists()).toBe(true)
      await card.trigger('click')
      expect(wrapper.emitted('select-machine')).toBeTruthy()
    })

    it('emits set-layer event when layer buttons are clicked', async () => {
      const wrapper = mount(DigitalTwinCanvas, {
        props: {
          machines: mockMachines,
          selectedMachineId: null,
          activeLayer: 'status',
          lineStopCount: 0
        }
      })

      const heatmapBtn = wrapper.findAll('button').find(b => b.text().includes('Heatmap'))
      expect(heatmapBtn?.exists()).toBe(true)
      await heatmapBtn?.trigger('click')
      expect(wrapper.emitted('set-layer')?.[0]).toEqual(['thermal'])
    })
  })

  describe('DigitalTwinInspector Component', () => {
    it('renders machine shadow, telemetry gauges, and edge controller metadata', () => {
      const wrapper = mount(DigitalTwinInspector, {
        props: {
          machine: mockMachines[1], // OP20-Weld
          activeTickets: mockTickets
        }
      })

      expect(wrapper.text()).toContain('DIGITAL TWIN SHADOW')
      expect(wrapper.text()).toContain('LINE1-OP20')
      expect(wrapper.text()).toContain('Laser Robotic Welding Cell')
      expect(wrapper.text()).toContain('1.45 mm/s')
      expect(wrapper.text()).toContain('52 °C')
      expect(wrapper.text()).toContain('CAD-LINE1-OP20')
    })

    it('switches tabs between telemetry, controller, and maintenance', async () => {
      const wrapper = mount(DigitalTwinInspector, {
        props: {
          machine: mockMachines[1],
          activeTickets: mockTickets
        }
      })

      // Click Edge IPC Tab
      const controllerTab = wrapper.findAll('button').find(b => b.text().includes('Edge IPC'))
      await controllerTab?.trigger('click')
      expect(wrapper.text()).toContain('IPC-L1-02')
      expect(wrapper.text()).toContain('192.168.1.111')

      // Click Tickets Tab
      const ticketsTab = wrapper.findAll('button').find(b => b.text().includes('Tickets'))
      await ticketsTab?.trigger('click')
      expect(wrapper.text()).toContain('TICK-101')
      expect(wrapper.text()).toContain('Spindle Harmonic Deviation')
      expect(wrapper.text()).toContain('LINE STOPPED')
    })
  })

  describe('DigitalTwinKpis Component', () => {
    it('renders plant OEE, active fleet, and line stop alarms', () => {
      const metrics = {
        totalMachines: 10,
        activeMachines: 8,
        warningMachines: 1,
        maintenanceMachines: 1,
        decommissionedMachines: 0,
        lineStopsCount: 2,
        oeePercent: 99.4,
        availabilityPercent: 98.2,
        avgTemperature: 42.1,
        avgVibrationRms: 1.22,
        mttrMinutes: 14.2,
        mtbfHours: 410.0
      }

      const wrapper = mount(DigitalTwinKpis, {
        props: { metrics }
      })

      expect(wrapper.text()).toContain('Plant OEE')
      expect(wrapper.text()).toContain('99.4')
      expect(wrapper.text()).toContain('8')
      expect(wrapper.text()).toContain('/ 10 Nodes')
      expect(wrapper.text()).toContain('2')
      expect(wrapper.text()).toContain('Line Stops')
      expect(wrapper.text()).toContain('IMMEDIATE DISPATCH')
    })
  })

  describe('DigitalTwinWaveform Component', () => {
    it('renders real-time sparkline SVG with Upper Control Limit markers', () => {
      const noiseMap = {
        [mockMachines[1].id]: {
          waveHistory: [1.1, 1.2, 1.4, 1.3, 1.5, 1.2, 1.45]
        }
      }

      const wrapper = mount(DigitalTwinWaveform, {
        props: {
          machines: mockMachines,
          selectedMachine: mockMachines[1],
          telemetryNoiseMap: noiseMap
        }
      })

      expect(wrapper.text()).toContain('Real-Time Waveform & SPC Bounds')
      expect(wrapper.text()).toContain('UCL: 2.80 mm/s')
      expect(wrapper.find('svg').exists()).toBe(true)
      expect(wrapper.find('polyline').exists()).toBe(true)
    })
  })

  describe('DigitalTwinIncidentRadar Component', () => {
    it('renders active line stop emergency downtime banner and ticket queue', () => {
      const wrapper = mount(DigitalTwinIncidentRadar, {
        props: {
          tickets: mockTickets,
          lineStopCount: 1
        }
      })

      expect(wrapper.text()).toContain('Incident Radar & Downtime')
      expect(wrapper.text()).toContain('CRITICAL LINE STOP ACTIVE')
      expect(wrapper.text()).toContain('18.5 min downtime')
      expect(wrapper.text()).toContain('TICK-101')
    })
  })

  describe('DigitalTwinMachineGrid Component', () => {
    it('renders search bar, view switcher, and machine cards', async () => {
      const wrapper = mount(DigitalTwinMachineGrid, {
        props: {
          machines: mockMachines,
          selectedMachineId: '11111111-1111-1111-1111-111111111111',
          searchQuery: '',
          selectedLine: 'all',
          selectedStatus: 'all',
          selectedTechnology: 'all',
          selectedCriticality: 'all',
          productionLines: ['Line 1', 'Line 2'],
          technologyDisciplines: ['Assembly', 'Welding', 'Test']
        }
      })

      expect(wrapper.text()).toContain('Connected Machine Fleet Matrix')
      expect(wrapper.text()).toContain('3 Nodes')
      expect(wrapper.text()).toContain('LINE1-OP10')
      expect(wrapper.text()).toContain('LINE1-OP20')
      expect(wrapper.find('input[placeholder*="Search station"]').exists()).toBe(true)
    })
  })

  describe('DigitalTwinCustomizerModal Component', () => {
    it('renders layout presets, widget toggles, and density selectors', () => {
      const widgets = [
        { id: 'twin-canvas', title: 'Topographic Plant Digital Twin', description: 'Spatial layout', category: 'core' as const, visible: true, order: 1 },
        { id: 'kpi-matrix', title: 'Plant Health KPIs', description: 'OEE and Fleet health', category: 'core' as const, visible: false, order: 2 }
      ]

      const wrapper = mount(DigitalTwinCustomizerModal, {
        props: {
          isOpen: true,
          widgets,
          layoutPreset: 'command-center',
          density: 'comfortable',
          themeAccent: 'graphite',
          autoRefreshSeconds: 2,
          isSimulationActive: true
        }
      })

      expect(wrapper.text()).toContain('Digital Twin Studio & Customizer')
      expect(wrapper.text()).toContain('Role & View Presets')
      expect(wrapper.text()).toContain('Command Center')
      expect(wrapper.text()).toContain('Spatial Topography Focus')
      expect(wrapper.text()).toContain('Topographic Plant Digital Twin')
    })
  })

  describe('DigitalTwinSearchBar Component', () => {
    it('renders search trigger button and modal with keyboard shortcut', async () => {
      const wrapper = mount(DigitalTwinSearchBar, {
        props: {
          machines: mockMachines,
          tickets: mockTickets,
          controllers: mockControllers
        }
      })

      const triggerBtn = wrapper.find('button')
      expect(triggerBtn.exists()).toBe(true)
      expect(triggerBtn.text()).toContain('Search')

      // Click to open
      await triggerBtn.trigger('click')
      expect(wrapper.find('input').exists()).toBe(true)
    })
  })
})
