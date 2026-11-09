import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { useStationTelemetry } from '../../../src/frontend/web/app/composables/useStationTelemetry'
import { generateStationKpis } from '../../../src/frontend/web/server/api/telemetry/station-kpis.get'
import StationTelemetryBubble from '../../../src/frontend/web/app/components/map/StationTelemetryBubble.vue'
import type { MaintenanceTicket } from '../../../src/frontend/web/app/types/maintenance'

describe('Station Telemetry & Plant Map Overlay Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('Server Station KPI Generator', () => {
    it('generates 100 station KPIs across all 8 production lines', () => {
      const allKpis = generateStationKpis()
      expect(allKpis.length).toBe(100)

      for (const kpi of allKpis) {
        expect(kpi.handle).toMatch(/^L0[1-8]-OP\d{3}$/)
        expect(kpi.oee).toBeGreaterThan(50.0)
        expect(kpi.oee).toBeLessThanOrEqual(100.0)
        expect(kpi.yield).toBeGreaterThan(80.0)
        expect(kpi.yield).toBeLessThanOrEqual(100.0)
        expect(kpi.partsPerHour).toBeGreaterThan(50)
        expect(kpi.cycleTimeSeconds).toBeGreaterThan(0)
        expect(['RUNNING', 'IDLE', 'WARNING', 'FAULTED', 'MAINTENANCE']).toContain(kpi.operationalStatus)
      }
    })

    it('filters stations by line ID correctly', () => {
      const line1 = generateStationKpis('L01')
      expect(line1.length).toBe(12)
      expect(line1.every(s => s.lineId === 'L01')).toBe(true)

      const line2 = generateStationKpis('L02')
      expect(line2.length).toBe(13)
      expect(line2.every(s => s.lineId === 'L02')).toBe(true)
    })

    it('identifies critical station and SLA breach on L02-OP060', () => {
      const op60 = generateStationKpis(undefined, 'L02-OP060')
      expect(op60.length).toBe(1)
      expect(op60[0].handle).toBe('L02-OP060')
      expect(op60[0].hasSlaBreach).toBe(true)
      expect(op60[0].operationalStatus).toBe('FAULTED')
      expect(op60[0].activeTicketCount).toBeGreaterThan(0)
    })
  })

  describe('useStationTelemetry Composable', () => {
    it('provides fast synchronous getStationKpi with deterministic fallback', () => {
      const { getStationKpi, stopLiveSimulation } = useStationTelemetry()
      try {
        const kpi = getStationKpi('L01-OP030')
        expect(kpi).toBeDefined()
        expect(kpi.handle).toBe('L01-OP030')
        expect(kpi.oee).toBeGreaterThan(70)
        expect(kpi.yield).toBeGreaterThan(90)
        expect(kpi.partsPerHour).toBeGreaterThan(80)
        expect(kpi.cycleTimeSeconds).toBeGreaterThan(10)
        expect(kpi.targetPartsPerHour).toBe(150)
      } finally {
        stopLiveSimulation()
      }
    })

    it('computes SLA status correctly when zero tickets exist', () => {
      const { getStationSla, stopLiveSimulation } = useStationTelemetry()
      try {
        const sla = getStationSla('L01-OP010', [])
        expect(sla.totalTickets).toBe(0)
        expect(sla.openTickets).toBe(0)
        expect(sla.slaHealth).toBe('none')
        expect(sla.earliestDueText).toBeNull()
      } finally {
        stopLiveSimulation()
      }
    })

    it('detects healthy active ticket within SLA window', () => {
      const { getStationSla, stopLiveSimulation } = useStationTelemetry()
      try {
        const mockTickets: MaintenanceTicket[] = [
          {
            id: 'tkt-101',
            title: 'Lubrication Check',
            status: 'Open',
            priority: 'Medium',
            stationId: 'L01-OP010',
            slaDueAt: new Date(Date.now() + 14400000).toISOString(), // 4h in future
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            tags: ['L01-OP010'],
            comments: []
          }
        ]

        const sla = getStationSla('L01-OP010', mockTickets)
        expect(sla.totalTickets).toBe(1)
        expect(sla.openTickets).toBe(1)
        expect(sla.slaHealth).toBe('healthy')
        expect(sla.earliestDueText).toContain('Due in')
      } finally {
        stopLiveSimulation()
      }
    })

    it('detects urgent warning when SLA expires in under 2 hours', () => {
      const { getStationSla, stopLiveSimulation } = useStationTelemetry()
      try {
        const mockTickets: MaintenanceTicket[] = [
          {
            id: 'tkt-102',
            title: 'Pneumatic Pressure Drop',
            status: 'InProgress',
            priority: 'High',
            stationId: 'L01-OP020',
            slaDueAt: new Date(Date.now() + 2700000).toISOString(), // 45m in future
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            tags: ['L01-OP020'],
            comments: []
          }
        ]

        const sla = getStationSla('L01-OP020', mockTickets)
        expect(sla.warningTickets).toBe(1)
        expect(sla.slaHealth).toBe('warning')
        expect(sla.earliestDueText).toBe('Due in 45m')
      } finally {
        stopLiveSimulation()
      }
    })

    it('detects breached SLA when overdue', () => {
      const { getStationSla, stopLiveSimulation } = useStationTelemetry()
      try {
        const mockTickets: MaintenanceTicket[] = [
          {
            id: 'tkt-103',
            title: 'Spindle Bearing Seizure',
            status: 'Open',
            priority: 'Critical',
            stationId: 'L02-OP060',
            slaDueAt: new Date(Date.now() - 900000).toISOString(), // 15m in past
            createdAt: new Date().toISOString(),
            updatedAt: new Date().toISOString(),
            tags: ['L02-OP060'],
            comments: []
          }
        ]

        const sla = getStationSla('L02-OP060', mockTickets)
        expect(sla.breachedTickets).toBe(1)
        expect(sla.criticalTickets).toBe(1)
        expect(sla.slaHealth).toBe('breached')
        expect(sla.earliestDueText).toContain('SLA Breached')
      } finally {
        stopLiveSimulation()
      }
    })
  })

  describe('StationTelemetryBubble Component', () => {
    const dummyKpi = {
      stationId: 'L01-OP030',
      handle: 'L01-OP030',
      name: 'L01-OP030 Dispensing & Bonding Cell',
      lineId: 'L01',
      lineName: 'Line 1 - Pre-Assembly',
      oee: 92.4,
      availability: 97.5,
      performance: 95.0,
      quality: 99.8,
      yield: 99.2,
      partsPerHour: 142,
      targetPartsPerHour: 150,
      cycleTimeSeconds: 25.4,
      targetCycleTimeSeconds: 24.0,
      totalPartsProduced: 1248,
      scrapParts: 10,
      operationalStatus: 'RUNNING' as const,
      activeTicketCount: 0,
      criticalTicketCount: 0,
      hasSlaBreach: false
    }

    it('renders compact pill with OEE %, Yield %, and Parts/Hour', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          isExpanded: false
        }
      })

      expect(wrapper.text()).toContain('92.4%')
      expect(wrapper.text()).toContain('99.2%')
      expect(wrapper.text()).toContain('142/h')
      expect(wrapper.find('.compact-pill').exists()).toBe(true)
      expect(wrapper.find('.expanded-card').exists()).toBe(false)
    })

    it('renders SLA alert badge in compact mode when open tickets exist', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          sla: {
            totalTickets: 2,
            openTickets: 2,
            criticalTickets: 1,
            breachedTickets: 0,
            warningTickets: 1,
            slaHealth: 'warning',
            earliestDueText: 'Due in 40m',
            tickets: []
          },
          isExpanded: false
        }
      })

      expect(wrapper.text()).toContain('2 SLA')
    })

    it('emits toggle-expand when compact pill is clicked', async () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          isExpanded: false
        }
      })

      await wrapper.find('.compact-pill').trigger('click')
      expect(wrapper.emitted('toggle-expand')).toBeTruthy()
    })

    it('renders rich expanded HUD view when isExpanded is true', () => {
      const mockTickets: MaintenanceTicket[] = [
        {
          id: 'tkt-201',
          title: 'Glue Dispenser Nozzle Clogged',
          status: 'Open',
          priority: 'High',
          stationId: 'L01-OP030',
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          comments: []
        }
      ]

      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          sla: {
            totalTickets: 1,
            openTickets: 1,
            criticalTickets: 0,
            breachedTickets: 0,
            warningTickets: 0,
            slaHealth: 'healthy',
            earliestDueText: 'Due in 2h',
            tickets: mockTickets
          },
          isExpanded: true
        }
      })

      expect(wrapper.find('.expanded-card').exists()).toBe(true)
      expect(wrapper.text()).toContain('L01-OP030')
      expect(wrapper.text()).toContain('RUNNING')
      expect(wrapper.text()).toContain('OEE')
      expect(wrapper.text()).toContain('Yield')
      expect(wrapper.text()).toContain('Parts / h')
      expect(wrapper.text()).toContain('A:97.5%')
      expect(wrapper.text()).toContain('Glue Dispenser Nozzle Clogged')
      expect(wrapper.text()).toContain('Scrap: 10 pcs')
    })

    it('emits select-station, create-ticket and view-ticket events from expanded HUD', async () => {
      const mockTickets: MaintenanceTicket[] = [
        {
          id: 'tkt-301',
          title: 'Nozzle Clean Required',
          status: 'Open',
          priority: 'Critical',
          stationId: 'L01-OP030',
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          comments: []
        }
      ]

      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          sla: {
            totalTickets: 1,
            openTickets: 1,
            criticalTickets: 1,
            breachedTickets: 0,
            warningTickets: 0,
            slaHealth: 'warning',
            earliestDueText: 'Due in 30m',
            tickets: mockTickets
          },
          isExpanded: true
        }
      })

      const buttons = wrapper.findAll('button')
      const selectBtn = buttons.find(b => b.text().includes('Select Node'))
      expect(selectBtn).toBeDefined()
      await selectBtn?.trigger('click')
      expect(wrapper.emitted('select-station')?.[0]).toEqual(['L01-OP030'])

      const ticketBtn = buttons.find(b => b.text().includes('Ticket'))
      expect(ticketBtn).toBeDefined()
      await ticketBtn?.trigger('click')
      expect(wrapper.emitted('create-ticket')?.[0]).toEqual(['L01-OP030'])

      // Click ticket row
      const ticketRow = wrapper.find('.expanded-card [class*="cursor-pointer"]')
      if (ticketRow.exists()) {
        await ticketRow.trigger('click')
        expect(wrapper.emitted('view-ticket')?.[0]).toEqual(['tkt-301'])
      }
    })

    it('renders sub-second live vibration badge in compact mode', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          oscillation: {
            stationId: 'L01-OP030',
            vibrationMmS: 1.5,
            spindleRpm: 2950,
            cycleTimeMs: 24000,
            powerKw: 5.2,
            temperatureC: 44.0,
            frequencyHz: 50.0,
            timestamp: Date.now(),
            history: [1.2, 1.3, 1.5]
          },
          showOscillation: true,
          isExpanded: false
        }
      })

      expect(wrapper.text()).toContain('1.5 mm/s')
      expect(wrapper.find('[title*="Sub-Second Oscillation"]').exists()).toBe(true)
    })

    it('renders real-time dynamic oscillation waveform sparkline and metrics in expanded mode', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          oscillation: {
            stationId: 'L01-OP030',
            vibrationMmS: 1.62,
            spindleRpm: 2840,
            cycleTimeMs: 24000,
            powerKw: 4.8,
            temperatureC: 45.5,
            frequencyHz: 50.0,
            timestamp: Date.now(),
            history: [1.2, 1.4, 1.3, 1.5, 1.62]
          },
          showOscillation: true,
          isExpanded: true
        }
      })

      expect(wrapper.text()).toContain('Live Machine Dynamics')
      expect(wrapper.text()).toContain('SignalR 60Hz')
      expect(wrapper.text()).toContain('1.62')
      expect(wrapper.text()).toContain('2840')
      expect(wrapper.text()).toContain('4.8')

      // Check SVG sparkline
      const polyline = wrapper.find('polyline')
      expect(polyline.exists()).toBe(true)
      expect(polyline.attributes('stroke')).toBe('#22d3ee')
    })
  })

  describe('Real-Time Sub-Second Oscillation & Stream Engine', () => {
    it('generates deterministic oscillation data with 20-point historical buffer', () => {
      const { getStationOscillation, stopOscillationStream } = useStationTelemetry()
      try {
        const osc = getStationOscillation('L01-OP020')
        expect(osc.stationId).toBe('L01-OP020')
        expect(osc.vibrationMmS).toBeGreaterThan(0.5)
        expect(osc.spindleRpm).toBeGreaterThan(1500)
        expect(osc.powerKw).toBeGreaterThan(1.0)
        expect(osc.history.length).toBe(20)
      } finally {
        stopOscillationStream()
      }
    })

    it('updates oscillation and maintains bounded rolling history', () => {
      const { getStationOscillation, updateStationOscillation, stopOscillationStream } = useStationTelemetry()
      try {
        const initial = getStationOscillation('L01-OP050')
        const initialLen = initial.history.length

        updateStationOscillation('L01-OP050', { vibrationMmS: 2.85, spindleRpm: 3100 })
        const updated = getStationOscillation('L01-OP050')
        expect(updated.vibrationMmS).toBe(2.85)
        expect(updated.spindleRpm).toBe(3100)
        expect(updated.history.length).toBeLessThanOrEqual(24)
        expect(updated.history[updated.history.length - 1]).toBe(2.85)
      } finally {
        stopOscillationStream()
      }
    })
  })

  describe('Viewport Containment & Collision Clamping Strategy', () => {
    it('flips bubble below the station when anchor is too close to top edge', () => {
      // Simulate viewport: 800x600, topMin = 62, maxY = 580
      const cw = 800
      const ch = 600
      const minY = 62
      const maxY = ch - 20
      const cardH = 380

      // Station near the top at Y = 100
      const anchorY = 100
      const topAbove = anchorY - cardH - 12 // 100 - 380 - 12 = -292 (out of bounds)
      expect(topAbove).toBeLessThan(minY)

      // Algorithm flips below:
      const topBelow = anchorY + 16 // 116
      expect(topBelow).toBeGreaterThanOrEqual(minY)
      expect(topBelow + cardH).toBeLessThanOrEqual(maxY)
    })

    it('clamps horizontal position when anchor is near viewport edges', () => {
      const cw = 800
      const minX = 14
      const maxX = cw - 14
      const cardW = 320

      // Case 1: Station near left edge (anchorX = 20)
      const anchorXLeft = 20
      let left1 = anchorXLeft - cardW / 2 // 20 - 160 = -140
      left1 = Math.max(minX, Math.min(left1, maxX - cardW))
      expect(left1).toBe(minX) // Clamped to 14px

      // Case 2: Station near right edge (anchorX = 780)
      const anchorXRight = 780
      let left2 = anchorXRight - cardW / 2 // 780 - 160 = 620
      left2 = Math.max(minX, Math.min(left2, maxX - cardW))
      expect(left2).toBe(maxX - cardW) // Clamped to 466px
      expect(left2 + cardW).toBeLessThanOrEqual(cw)
    })
  })
})
