import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'

// The BFF route must proxy to the backend; capture the call instead of opening a socket.
const proxyToBackendMock = vi.fn((..._args: unknown[]) => [{ handle: 'L01-OP010' }])
vi.mock('~~/server/utils/backendProxy', () => ({
  proxyToBackend: (...args: unknown[]) => proxyToBackendMock(...args)
}))

// Live hub events: capture the listener the composable registers.
const hubListeners = new Set<(ev: any) => void>()
vi.mock('~/services/maintenance', () => ({
  getMaintenanceService: () => ({
    subscribeToEvents: (listener: (ev: any) => void) => {
      hubListeners.add(listener)
      return () => hubListeners.delete(listener)
    }
  })
}))

import { defineComponent, h, nextTick } from 'vue'
import stationKpisHandler from '~~/server/api/telemetry/station-kpis.get'
import {
  useStationTelemetry,
  normalizeStationKpi,
  normalizeStationOscillation,
  handleStationTelemetryEvent,
  OSCILLATION_HISTORY_LIMIT,
  type StationTelemetryKpi
} from '../../../src/frontend/web/app/composables/useStationTelemetry'
import {
  computeOverlayPlacement,
  projectSvgPointToScreen,
  PLANT_MAP_CARD_SIZE,
  PLANT_MAP_OVERLAY_INSETS,
  PLANT_MAP_PILL_SIZE
} from '../../../src/frontend/web/app/utils/telemetryOverlayPlacement'
import StationTelemetryBubble from '../../../src/frontend/web/app/components/map/StationTelemetryBubble.vue'
import type { MaintenanceTicket } from '../../../src/frontend/web/app/types/maintenance'

/** Backend `StationKpiDto` as serialized by ASP.NET Core (camelCase). */
const backendKpiDto = (handle: string, overrides: Record<string, unknown> = {}) => ({
  stationId: handle,
  handle,
  name: `${handle} Cell`,
  lineId: handle.slice(0, 3),
  lineName: 'Line 1',
  oee: 88.4,
  availability: 96.1,
  performance: 93.2,
  quality: 98.7,
  yield: 98.5,
  partsPerHour: 141,
  targetPartsPerHour: 150,
  cycleTimeSeconds: 25.5,
  targetCycleTimeSeconds: 24,
  totalPartsProduced: 1204,
  scrapParts: 18,
  operationalStatus: 'RUNNING',
  activeTicketCount: 0,
  criticalTicketCount: 0,
  hasSlaBreach: false,
  ...overrides
})

const fetchMock = vi.fn()

describe('Station Telemetry & Plant Map Overlay Suite', () => {
  beforeEach(async () => {
    fetchMock.mockReset()
    vi.stubGlobal('$fetch', fetchMock)
    // Reset the shared KPI store to "backend has no data".
    fetchMock.mockResolvedValueOnce([])
    await useStationTelemetry().fetchStationKpis()
    fetchMock.mockReset()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.useRealTimers()
  })

  describe('BFF /api/telemetry/station-kpis', () => {
    it('proxies to the backend analytics endpoint instead of synthesizing KPIs', async () => {
      proxyToBackendMock.mockClear()
      const event = { node: { req: { headers: {} } }, context: {} } as any
      const result = await (stationKpisHandler as any)(event)

      expect(proxyToBackendMock).toHaveBeenCalledTimes(1)
      const [passedEvent, prefix, path] = proxyToBackendMock.mock.calls[0]!
      expect(passedEvent).toBe(event)
      expect(prefix).toBe('v1')
      expect(path).toBe('analytics/station-kpis')
      expect(result).toEqual([{ handle: 'L01-OP010' }])
    })
  })

  describe('DTO normalization (C# StationKpiDto / SignalR broadcasts → TS)', () => {
    it('maps a camelCase backend DTO 1:1', () => {
      const kpi = normalizeStationKpi(backendKpiDto('L01-OP030'))!
      expect(kpi.handle).toBe('L01-OP030')
      expect(kpi.oee).toBe(88.4)
      expect(kpi.targetPartsPerHour).toBe(150)
      expect(kpi.operationalStatus).toBe('RUNNING')
    })

    it('coerces numeric strings, rejects unknown statuses and drops identity-less payloads', () => {
      const kpi = normalizeStationKpi({ handle: 'L02-OP010', oee: '77.5', operationalStatus: 'exploded' })!
      expect(kpi.oee).toBe(77.5)
      expect(kpi.operationalStatus).toBe('UNKNOWN')
      // Unreported values stay null instead of turning into 0.
      expect(kpi.targetPartsPerHour).toBeNull()
      expect(kpi.yield).toBeNull()
      expect(normalizeStationKpi({ oee: 90 })).toBeNull()
      expect(normalizeStationKpi(null)).toBeNull()
    })

    it('merges a partial StationTelemetryBroadcastDto onto the previous record', () => {
      const previous = normalizeStationKpi(backendKpiDto('L01-OP040'))!
      // StationTelemetryBroadcastDto has no lineName / targets: they must be retained.
      const merged = normalizeStationKpi({ stationId: 'L01-OP040', handle: 'L01-OP040', oee: 70.1, operationalStatus: 'faulted' }, previous)!
      expect(merged.oee).toBe(70.1)
      expect(merged.operationalStatus).toBe('FAULTED')
      expect(merged.lineName).toBe('Line 1')
      expect(merged.targetPartsPerHour).toBe(150)
    })

    it('keeps explicit backend nulls as null on a full fetch, but as "unchanged" on a live update', () => {
      const dto = backendKpiDto('L01-OP050', { oee: null, partsPerHour: null, operationalStatus: 'UNKNOWN', lastTelemetryAt: null })
      const fresh = normalizeStationKpi(dto)!
      expect(fresh.oee).toBeNull()
      expect(fresh.partsPerHour).toBeNull()
      expect(fresh.operationalStatus).toBe('UNKNOWN')
      expect(fresh.lastTelemetryAt).toBeNull()

      const previous = normalizeStationKpi(backendKpiDto('L01-OP050'))!
      const live = normalizeStationKpi({ stationId: 'L01-OP050', handle: 'L01-OP050', oee: null, yield: 97.2 }, previous)!
      expect(live.oee).toBe(88.4)
      expect(live.yield).toBe(97.2)
    })

    it('indexes live oscillation samples by machine id and floor-plan handle; unreported quantities stay null', () => {
      handleStationTelemetryEvent({
        type: 'StationOscillationReceived',
        stationName: 'c0ffee00-0000-0000-0000-0000000000aa',
        summary: { stationId: 'c0ffee00-0000-0000-0000-0000000000aa', handle: '3B4F', vibrationMmS: 1.85, powerKw: null, timestamp: 1_700_000_000_000 },
        timestamp: ''
      } as any)
      const { getStationOscillation } = useStationTelemetry()
      const byHandle = getStationOscillation('3b4f')!
      expect(byHandle).toBe(getStationOscillation('C0FFEE00-0000-0000-0000-0000000000AA'))
      expect(byHandle.vibrationMmS).toBe(1.85)
      expect(byHandle.powerKw).toBeNull()
      expect(byHandle.spindleRpm).toBeNull()
    })

    it('uses the backend sample timestamp (epoch ms or ISO string) for oscillation data', () => {
      const a = normalizeStationOscillation({ stationId: 'X', vibrationMmS: 1.1, timestamp: 1_700_000_000_000 }, 'X')!
      expect(a.timestamp).toBe(1_700_000_000_000)
      const b = normalizeStationOscillation({ vibrationMmS: 1.2, timestamp: '2026-11-09T10:00:00Z' }, 'X')!
      expect(b.timestamp).toBe(Date.parse('2026-11-09T10:00:00Z'))
      expect(normalizeStationOscillation({ spindleRpm: 3000 }, 'X')).toBeNull()
    })
  })

  describe('useStationTelemetry composable', () => {
    it('returns null (not fabricated values) for stations without telemetry', () => {
      const { getStationKpi, getStationOscillation, hasStationKpiData } = useStationTelemetry()
      expect(getStationKpi('L01-OP030')).toBeNull()
      expect(getStationOscillation('NO-DATA-STATION')).toBeNull()
      expect(hasStationKpiData.value).toBe(false)
    })

    it('loads KPIs from the BFF and indexes them by handle and stationId without double counting', async () => {
      fetchMock.mockResolvedValueOnce([
        backendKpiDto('L01-OP010', { stationId: 'c0ffee00-0000-0000-0000-000000000001' }),
        backendKpiDto('L01-OP020')
      ])
      const { fetchStationKpis, getStationKpi, stationKpiList, stationKpisError } = useStationTelemetry()
      await fetchStationKpis()

      expect(fetchMock).toHaveBeenCalledWith('/api/telemetry/station-kpis')
      expect(getStationKpi('l01-op010')?.oee).toBe(88.4)
      expect(getStationKpi('C0FFEE00-0000-0000-0000-000000000001')?.handle).toBe('L01-OP010')
      expect(stationKpiList.value).toHaveLength(2)
      expect(stationKpisError.value).toBeNull()
    })

    it('keeps last known data and exposes the error when the backend is unavailable', async () => {
      fetchMock.mockResolvedValueOnce([backendKpiDto('L03-OP010')])
      const { fetchStationKpis, getStationKpi, stationKpisError } = useStationTelemetry()
      await fetchStationKpis()

      fetchMock.mockRejectedValueOnce(Object.assign(new Error('502'), { data: { message: 'The Heimdall backend is currently unavailable.' } }))
      await fetchStationKpis()

      expect(getStationKpi('L03-OP010')?.oee).toBe(88.4)
      expect(stationKpisError.value).toBe('The Heimdall backend is currently unavailable.')
    })

    it('a mounted consumer only subscribes to the hub and never mutates telemetry on its own', async () => {
      vi.useFakeTimers()
      fetchMock.mockResolvedValue([backendKpiDto('L04-OP010')])
      const Consumer = defineComponent({ setup() { useStationTelemetry(); return () => h('div') } })
      const wrapper = mount(Consumer)
      // Let the onMounted fetch + dynamic import of the hub service settle.
      await vi.dynamicImportSettled()
      await nextTick()

      const { getStationKpi, getStationOscillation, isLiveStreamSubscribed } = useStationTelemetry()
      expect(isLiveStreamSubscribed.value).toBe(true)
      expect(hubListeners.size).toBe(1)

      for (const listener of hubListeners) {
        listener({ type: 'StationOscillationReceived', stationName: 'L04-OP010', summary: { stationId: 'L04-OP010', vibrationMmS: 1.4, timestamp: 1 } })
      }
      const kpiBefore = JSON.stringify(getStationKpi('L04-OP010'))
      const oscBefore = JSON.stringify(getStationOscillation('L04-OP010'))
      expect(kpiBefore).not.toBe('null')

      vi.advanceTimersByTime(30_000)
      expect(JSON.stringify(getStationKpi('L04-OP010'))).toBe(kpiBefore)
      expect(JSON.stringify(getStationOscillation('L04-OP010'))).toBe(oscBefore)

      wrapper.unmount()
      expect(hubListeners.size).toBe(0)
      expect(isLiveStreamSubscribed.value).toBe(false)
    })

    it('does not leak a hub listener when stopped before the subscription resolves', async () => {
      const { startOscillationStream, stopOscillationStream } = useStationTelemetry()
      startOscillationStream()
      stopOscillationStream()
      await vi.dynamicImportSettled()
      expect(hubListeners.size).toBe(0)
    })

    it('applies live SignalR station events and caps the vibration ring buffer', () => {
      const { getStationOscillation, getStationKpi } = useStationTelemetry()
      for (let i = 0; i < OSCILLATION_HISTORY_LIMIT + 10; i++) {
        handleStationTelemetryEvent({
          type: 'StationOscillationReceived',
          stationName: 'L05-OP090',
          summary: { stationId: 'L05-OP090', vibrationMmS: i, spindleRpm: 2800, timestamp: 1_700_000_000_000 + i },
          timestamp: new Date().toISOString()
        } as any)
      }
      const osc = getStationOscillation('L05-OP090')!
      expect(osc.history).toHaveLength(OSCILLATION_HISTORY_LIMIT)
      expect(osc.history[osc.history.length - 1]).toBe(OSCILLATION_HISTORY_LIMIT + 9)
      expect(osc.vibrationMmS).toBe(OSCILLATION_HISTORY_LIMIT + 9)
      expect(osc.timestamp).toBe(1_700_000_000_000 + OSCILLATION_HISTORY_LIMIT + 9)

      handleStationTelemetryEvent({
        type: 'StationTelemetryReceived',
        stationName: 'L05-OP090',
        summary: { handle: 'L05-OP090', oee: 81.3, operationalStatus: 'WARNING' },
        timestamp: new Date().toISOString()
      } as any)
      expect(getStationKpi('L05-OP090')?.oee).toBe(81.3)
      expect(getStationKpi('L05-OP090')?.operationalStatus).toBe('WARNING')
    })

    it('computes SLA status correctly when zero tickets exist', () => {
      const sla = useStationTelemetry().getStationSla('L01-OP010', [])
      expect(sla.totalTickets).toBe(0)
      expect(sla.openTickets).toBe(0)
      expect(sla.slaHealth).toBe('none')
      expect(sla.earliestDueText).toBeNull()
    })

    const ticket = (overrides: Partial<MaintenanceTicket>): MaintenanceTicket => ({
      id: 'tkt',
      title: 'Ticket',
      status: 'Open',
      priority: 'Medium',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      comments: [],
      ...overrides
    } as MaintenanceTicket)

    it('detects healthy active ticket within SLA window', () => {
      const sla = useStationTelemetry().getStationSla('L01-OP010', [
        ticket({ id: 'tkt-101', stationId: 'L01-OP010', slaDueAt: new Date(Date.now() + 14400000).toISOString() })
      ])
      expect(sla.totalTickets).toBe(1)
      expect(sla.openTickets).toBe(1)
      expect(sla.slaHealth).toBe('healthy')
      expect(sla.earliestDueText).toContain('Due in')
    })

    it('detects urgent warning when SLA expires in under 2 hours', () => {
      const sla = useStationTelemetry().getStationSla('L01-OP020', [
        ticket({ id: 'tkt-102', status: 'InProgress', priority: 'High', tags: ['L01-OP020'], slaDueAt: new Date(Date.now() + 2700000).toISOString() })
      ])
      expect(sla.warningTickets).toBe(1)
      expect(sla.slaHealth).toBe('warning')
      expect(sla.earliestDueText).toBe('Due in 45m')
    })

    it('detects breached SLA when overdue', () => {
      const sla = useStationTelemetry().getStationSla('L02-OP060', [
        ticket({ id: 'tkt-103', priority: 'Critical', stationId: 'L02-OP060', slaDueAt: new Date(Date.now() - 900000).toISOString() })
      ])
      expect(sla.breachedTickets).toBe(1)
      expect(sla.criticalTickets).toBe(1)
      expect(sla.slaHealth).toBe('breached')
      expect(sla.earliestDueText).toContain('SLA Breached')
    })

    it('does not attribute tickets of a longer handle to a prefix handle', () => {
      const tickets = [
        ticket({ id: 'a', stationId: 'L01-OP010' }),
        ticket({ id: 'b', tags: ['L01-OP0100'] }),
        ticket({ id: 'c', title: 'Spindle noise on L01-OP010 axis' })
      ]
      const sla = useStationTelemetry().getStationSla('L01-OP01', tickets)
      expect(sla.totalTickets).toBe(0)
      const exact = useStationTelemetry().getStationSla('L01-OP010', tickets)
      expect(exact.tickets.map(t => t.id).sort()).toEqual(['a', 'c'])
    })
  })

  describe('StationTelemetryBubble component', () => {
    const dummyKpi: StationTelemetryKpi = {
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
      operationalStatus: 'RUNNING',
      activeTicketCount: 0,
      criticalTicketCount: 0,
      hasSlaBreach: false,
      lastTelemetryAt: '2026-11-09T10:00:00Z'
    }

    it('renders compact pill with OEE %, Yield %, and Parts/Hour', () => {
      const wrapper = mount(StationTelemetryBubble, { props: { handle: 'L01-OP030', kpi: dummyKpi, isExpanded: false } })
      expect(wrapper.text()).toContain('92.4%')
      expect(wrapper.text()).toContain('99.2%')
      expect(wrapper.text()).toContain('142/h')
      expect(wrapper.find('.compact-pill').exists()).toBe(true)
      expect(wrapper.find('.expanded-card').exists()).toBe(false)
      expect(wrapper.find('.no-telemetry').exists()).toBe(false)
    })

    it('renders an explicit no-telemetry state instead of numbers when the station has no data', () => {
      const compact = mount(StationTelemetryBubble, { props: { handle: 'L09-OP010', kpi: null } })
      expect(compact.text()).toContain('No telemetry')
      expect(compact.text()).not.toMatch(/\d+(\.\d+)?%/)

      const expanded = mount(StationTelemetryBubble, { props: { handle: 'L09-OP010', kpi: null, isExpanded: true } })
      expect(expanded.find('.no-telemetry').exists()).toBe(true)
      expect(expanded.text()).toContain('NO DATA')
      expect(expanded.text()).not.toContain('Live Machine Dynamics')
    })

    it('shows a dash for production values the station has not reported', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: { handle: 'L01-OP030', kpi: { ...dummyKpi, oee: null, yield: null, partsPerHour: null, scrapParts: null }, isExpanded: true }
      })
      expect(wrapper.text()).toContain('—%')
      expect(wrapper.text()).toContain('Scrap: — pcs')
      expect(wrapper.text()).not.toMatch(/\b0%/)
      expect(wrapper.html()).not.toContain('NaN')
    })

    it('does not render NaN widths when the backend reports no parts/hour target', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: { handle: 'L01-OP030', kpi: { ...dummyKpi, targetPartsPerHour: null }, isExpanded: true }
      })
      expect(wrapper.html()).not.toContain('NaN')
    })

    it('honours the size limits computed by the viewport clamp', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: { handle: 'L01-OP030', kpi: dummyKpi, isExpanded: true, maxWidthPx: 250, maxHeightPx: 200, arrowOffsetPx: 40 }
      })
      const style = wrapper.find('.expanded-card').attributes('style') || ''
      expect(style).toContain('width: 250px')
      expect(style).toContain('max-height: 200px')
    })

    it('renders SLA alert badge in compact mode when open tickets exist', () => {
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          sla: { totalTickets: 2, openTickets: 2, criticalTickets: 1, breachedTickets: 0, warningTickets: 1, slaHealth: 'warning', earliestDueText: 'Due in 40m', tickets: [] },
          isExpanded: false
        }
      })
      expect(wrapper.text()).toContain('2 SLA')
    })

    it('emits toggle-expand when compact pill is clicked', async () => {
      const wrapper = mount(StationTelemetryBubble, { props: { handle: 'L01-OP030', kpi: dummyKpi, isExpanded: false } })
      await wrapper.find('.compact-pill').trigger('click')
      expect(wrapper.emitted('toggle-expand')).toBeTruthy()
    })

    it('renders rich expanded HUD view when isExpanded is true', () => {
      const tickets = [{ id: 'tkt-201', title: 'Glue Dispenser Nozzle Clogged', status: 'Open', priority: 'High', stationId: 'L01-OP030', createdAt: '', updatedAt: '', comments: [] }] as unknown as MaintenanceTicket[]
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          sla: { totalTickets: 1, openTickets: 1, criticalTickets: 0, breachedTickets: 0, warningTickets: 0, slaHealth: 'healthy', earliestDueText: 'Due in 2h', tickets },
          isExpanded: true
        }
      })
      expect(wrapper.find('.expanded-card').exists()).toBe(true)
      expect(wrapper.text()).toContain('RUNNING')
      expect(wrapper.text()).toContain('A:97.5%')
      expect(wrapper.text()).toContain('Glue Dispenser Nozzle Clogged')
      expect(wrapper.text()).toContain('Scrap: 10 pcs')
    })

    it('emits select-station, create-ticket and view-ticket events from expanded HUD', async () => {
      const tickets = [{ id: 'tkt-301', title: 'Nozzle Clean Required', status: 'Open', priority: 'Critical', stationId: 'L01-OP030', createdAt: '', updatedAt: '', comments: [] }] as unknown as MaintenanceTicket[]
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          sla: { totalTickets: 1, openTickets: 1, criticalTickets: 1, breachedTickets: 0, warningTickets: 0, slaHealth: 'warning', earliestDueText: 'Due in 30m', tickets },
          isExpanded: true
        }
      })

      const buttons = wrapper.findAll('button')
      await buttons.find(b => b.text().includes('Select Node'))!.trigger('click')
      expect(wrapper.emitted('select-station')?.[0]).toEqual(['L01-OP030'])

      await buttons.find(b => b.text().includes('Ticket'))!.trigger('click')
      expect(wrapper.emitted('create-ticket')?.[0]).toEqual(['L01-OP030'])

      const ticketRow = wrapper.findAll('.expanded-card [class*="cursor-pointer"]').find(r => r.text().includes('Nozzle Clean Required'))
      expect(ticketRow).toBeDefined()
      await ticketRow!.trigger('click')
      expect(wrapper.emitted('view-ticket')?.[0]).toEqual(['tkt-301'])
    })

    it('renders the live vibration badge only from a received sample', () => {
      const withSample = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          oscillation: { stationId: 'L01-OP030', handle: 'L01-OP030', vibrationMmS: 1.5, spindleRpm: 2950, cycleTimeMs: 24000, powerKw: 5.2, temperatureC: 44.0, frequencyHz: 50.0, timestamp: Date.now(), history: [1.2, 1.3, 1.5] },
          showOscillation: true
        }
      })
      expect(withSample.text()).toContain('1.5 mm/s')
      expect(withSample.find('[title*="Sub-Second Oscillation"]').exists()).toBe(true)

      const withoutSample = mount(StationTelemetryBubble, { props: { handle: 'NO-SAMPLE-STATION', kpi: dummyKpi, showOscillation: true } })
      expect(withoutSample.text()).not.toContain('mm/s')
    })

    it('renders oscillation waveform, metrics and sample time in expanded mode', () => {
      const ts = Date.parse('2026-11-09T10:15:30Z')
      const wrapper = mount(StationTelemetryBubble, {
        props: {
          handle: 'L01-OP030',
          kpi: dummyKpi,
          oscillation: { stationId: 'L01-OP030', handle: 'L01-OP030', vibrationMmS: 1.62, spindleRpm: 2840, cycleTimeMs: 24000, powerKw: 4.8, temperatureC: 45.5, frequencyHz: 50.0, timestamp: ts, history: [1.2, 1.4, 1.3, 1.5, 1.62] },
          showOscillation: true,
          isExpanded: true
        }
      })

      expect(wrapper.text()).toContain('Live Machine Dynamics')
      expect(wrapper.text()).toContain(new Date(ts).toLocaleTimeString())
      expect(wrapper.text()).toContain('1.62')
      expect(wrapper.text()).toContain('2840')
      expect(wrapper.text()).toContain('4.8')

      const polyline = wrapper.find('polyline')
      expect(polyline.attributes('points')!.split(' ')).toHaveLength(5)
      const gradientId = wrapper.find('linearGradient').attributes('id')!
      expect(gradientId).toMatch(/^[A-Za-z0-9_-]+$/)
      expect(wrapper.find('polygon').attributes('fill')).toBe(`url(#${gradientId})`)
    })
  })

  describe('Viewport containment & clamping (computeOverlayPlacement)', () => {
    const contained = (p: ReturnType<typeof computeOverlayPlacement>, cw: number, ch: number) => {
      const i = PLANT_MAP_OVERLAY_INSETS
      expect(p.left).toBeGreaterThanOrEqual(i.left)
      expect(p.top).toBeGreaterThanOrEqual(i.top)
      expect(p.left + p.width).toBeLessThanOrEqual(cw - i.right)
      expect(p.top + p.height).toBeLessThanOrEqual(ch - i.bottom)
    }

    it('keeps pills and expanded cards fully inside the safe area for any on-screen anchor', () => {
      for (const [cw, ch] of [[800, 600], [1280, 720], [360, 300], [300, 200]] as const) {
        for (let ax = 0; ax <= cw; ax += cw / 10) {
          for (let ay = 0; ay <= ch; ay += ch / 10) {
            for (const size of [PLANT_MAP_PILL_SIZE, PLANT_MAP_CARD_SIZE]) {
              const p = computeOverlayPlacement({ anchorX: ax, anchorY: ay, containerWidth: cw, containerHeight: ch, size, insets: PLANT_MAP_OVERLAY_INSETS })
              expect(p.visible).toBe(true)
              contained(p, cw, ch)
              expect(p.width).toBeLessThanOrEqual(size.width)
              expect(p.height).toBeLessThanOrEqual(size.height)
            }
          }
        }
      }
    })

    it('places the card above the anchor when it fits and flips below near the top edge', () => {
      const base = { containerWidth: 800, containerHeight: 1000, size: PLANT_MAP_CARD_SIZE, insets: PLANT_MAP_OVERLAY_INSETS, gapAbove: 12, gapBelow: 16 }
      const above = computeOverlayPlacement({ ...base, anchorX: 400, anchorY: 800 })
      expect(above.isFlippedBottom).toBe(false)
      expect(above.top + above.height).toBe(800 - 12)

      const below = computeOverlayPlacement({ ...base, anchorX: 400, anchorY: 100 })
      expect(below.isFlippedBottom).toBe(true)
      expect(below.top).toBe(116)
    })

    it('clamps horizontally at both edges and points the arrow at the anchor', () => {
      const base = { anchorY: 500, containerWidth: 800, containerHeight: 600, size: PLANT_MAP_CARD_SIZE, insets: PLANT_MAP_OVERLAY_INSETS }
      const left = computeOverlayPlacement({ ...base, anchorX: 20 })
      expect(left.left).toBe(14)
      expect(left.arrowOffset).toBe(12)

      const right = computeOverlayPlacement({ ...base, anchorX: 760 })
      expect(right.left + right.width).toBe(800 - 14)
      expect(right.left + right.arrowOffset).toBe(760)
    })

    it('shrinks the card to the safe area in small viewports', () => {
      const p = computeOverlayPlacement({ anchorX: 150, anchorY: 150, containerWidth: 300, containerHeight: 250, size: PLANT_MAP_CARD_SIZE, insets: PLANT_MAP_OVERLAY_INSETS })
      expect(p.width).toBe(300 - 28)
      expect(p.height).toBe(250 - 62 - 20)
      contained(p, 300, 250)
    })

    it('hides overlays for anchors outside the canvas or when there is no safe area', () => {
      const base = { containerWidth: 800, containerHeight: 600, size: PLANT_MAP_PILL_SIZE, insets: PLANT_MAP_OVERLAY_INSETS }
      expect(computeOverlayPlacement({ ...base, anchorX: -5, anchorY: 300 }).visible).toBe(false)
      expect(computeOverlayPlacement({ ...base, anchorX: 400, anchorY: 601 }).visible).toBe(false)
      expect(computeOverlayPlacement({ ...base, anchorX: Number.NaN, anchorY: 300 }).visible).toBe(false)
      expect(computeOverlayPlacement({ ...base, containerHeight: 70, anchorX: 400, anchorY: 30 }).visible).toBe(false)
    })

    it('projects drawing coordinates like an <svg> with xMidYMid meet', () => {
      // 200x100 drawing in an 800x600 box: scale 4, letterboxed vertically by (600 - 400) / 2.
      const vb = { x: 0, y: 0, width: 200, height: 100 }
      expect(projectSvgPointToScreen({ x: 0, y: 0 }, vb, 800, 600)).toEqual({ x: 0, y: 100 })
      expect(projectSvgPointToScreen({ x: 200, y: 100 }, vb, 800, 600)).toEqual({ x: 800, y: 500 })
      expect(projectSvgPointToScreen({ x: 150, y: 60 }, { x: 100, y: 50, width: 100, height: 100 }, 400, 200)).toEqual({ x: 200, y: 20 })
      expect(projectSvgPointToScreen({ x: 1, y: 1 }, { x: 0, y: 0, width: 0, height: 10 }, 800, 600)).toBeNull()
    })
  })
})
