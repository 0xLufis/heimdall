import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, reactive } from 'vue'

// Hub events: capture every listener (telemetry store and map overlays) like the real hub fans out.
const hubListeners = new Set<(ev: any) => void>()
vi.mock('~/services/maintenance', () => ({
  getMaintenanceService: () => ({
    subscribeToEvents: (listener: (ev: any) => void) => {
      hubListeners.add(listener)
      return () => hubListeners.delete(listener)
    }
  })
}))

import { stationPinState, describeStationPin, PIN_LOW_OEE_PERCENT } from '../../../src/frontend/web/app/utils/stationPinState'
import { useStaggeredRefresh } from '../../../src/frontend/web/app/composables/useStaggeredRefresh'
import MapStationOverlays from '../../../src/frontend/web/app/components/map/MapStationOverlays.vue'
import { indexTicketsByStation, useStationTelemetry, type StationSlaSummary, type StationTelemetryKpi } from '../../../src/frontend/web/app/composables/useStationTelemetry'

const kpi = (handle: string, overrides: Partial<StationTelemetryKpi> = {}): StationTelemetryKpi => ({
  stationId: handle, handle, name: handle, lineId: 'L01', lineName: 'Line 1',
  oee: 88, availability: 95, performance: 94, quality: 99, yield: 99, partsPerHour: 140, targetPartsPerHour: 150,
  cycleTimeSeconds: 25, targetCycleTimeSeconds: 24, totalPartsProduced: 1000, scrapParts: 5,
  vibrationMmS: null, spindleRpm: null, cycleTimeMs: null, powerKw: null, temperatureC: null, frequencyHz: null,
  operationalStatus: 'RUNNING', activeTicketCount: 0, criticalTicketCount: 0, hasSlaBreach: false, lastTelemetryAt: null,
  ...overrides
})

const sla = (overrides: Partial<StationSlaSummary> = {}): StationSlaSummary => ({
  totalTickets: 0, openTickets: 0, criticalTickets: 0, breachedTickets: 0, warningTickets: 0,
  slaHealth: 'none', earliestDueText: null, tickets: [], ...overrides
})

describe('Plant map pins', () => {
  describe('stationPinState', () => {
    it('picks the most urgent tone: fault and SLA breach, then warnings, then the machine state', () => {
      expect(stationPinState(kpi('A', { operationalStatus: 'FAULTED' }), sla({ slaHealth: 'warning', openTickets: 1 })).tone).toBe('critical')
      expect(stationPinState(kpi('A'), sla({ slaHealth: 'breached', openTickets: 2, breachedTickets: 2, earliestDueText: '2 SLA Breached' }))).toMatchObject({ tone: 'critical', reason: '2 SLA Breached' })
      expect(stationPinState(kpi('A', { operationalStatus: 'WARNING' }), sla()).tone).toBe('warning')
      expect(stationPinState(kpi('A'), sla({ slaHealth: 'warning', openTickets: 1, criticalTickets: 1 }))).toMatchObject({ tone: 'warning', reason: '1 critical ticket' })
      expect(stationPinState(kpi('A'), sla({ slaHealth: 'warning', openTickets: 1, earliestDueText: 'Due in 45m' }))).toMatchObject({ tone: 'warning', reason: 'SLA due in 45m' })
      expect(stationPinState(kpi('A', { oee: PIN_LOW_OEE_PERCENT - 1 }), sla()).tone).toBe('warning')
      expect(stationPinState(kpi('A', { oee: PIN_LOW_OEE_PERCENT }), sla()).tone).toBe('running')
      expect(stationPinState(kpi('A', { operationalStatus: 'MAINTENANCE' }), sla()).tone).toBe('maintenance')
      expect(stationPinState(kpi('A', { operationalStatus: 'IDLE', oee: 10 }), sla()).tone).toBe('idle')
      expect(stationPinState(kpi('A', { operationalStatus: 'UNKNOWN' }), sla())).toMatchObject({ tone: 'unknown', reason: 'State not reported' })
      expect(stationPinState(null, sla())).toMatchObject({ tone: 'unknown', reason: 'No telemetry', status: 'UNKNOWN' })
    })

    it('counts as an alert for critical or warning tones and for any open ticket', () => {
      expect(stationPinState(kpi('A'), sla()).alert).toBe(false)
      expect(stationPinState(kpi('A'), sla({ openTickets: 1, slaHealth: 'healthy' }))).toMatchObject({ tone: 'running', alert: true, openTickets: 1 })
      expect(stationPinState(kpi('A', { operationalStatus: 'WARNING' }), sla()).alert).toBe(true)
    })

    it('describes a pin for its tooltip', () => {
      expect(describeStationPin('L01-OP100', stationPinState(kpi('L01-OP100'), sla({ openTickets: 2, slaHealth: 'healthy' })))).toBe('L01-OP100 · Running · 2 open tickets')
      expect(describeStationPin('L01-OP100', undefined)).toBe('L01-OP100')
    })
  })

  describe('indexTicketsByStation', () => {
    it('gives getStationSla exactly the same result as scanning every ticket', () => {
      const t = (id: string, fields: Record<string, unknown>) => ({ id, title: '', status: 'Open', priority: 'Medium', createdAt: '', updatedAt: '', comments: [], ...fields }) as any
      const tickets = [
        t('1', { stationId: 'l01-op010', priority: 'Critical' }),
        t('2', { title: 'Leak at L01-OP010, check seals' }),
        t('3', { title: 'Fixture L01-OP0100 worn' }), // near miss for L01-OP010
        t('4', { tags: ['l02-op020', 'hydraulics'], slaDueAt: new Date(Date.now() - 60_000).toISOString() }),
        t('5', { title: 'Station L03_OP030 jammed' }), // key outside the token alphabet
        t('6', { stationId: 'L01-OP010', status: 'Closed' }),
        t('7', { title: 'l01-op010 and L02-OP020 both stopped', slaDueAt: new Date(Date.now() + 30 * 60_000).toISOString() })
      ]
      const lookup = indexTicketsByStation(tickets)
      const { getStationSla } = useStationTelemetry()
      for (const key of ['L01-OP010', 'l02-op020', 'L01-OP0100', 'L03_OP030', 'HYDRAULICS', 'NOPE', '']) {
        expect(getStationSla(key, lookup(key)), key).toEqual(getStationSla(key, tickets))
      }
      expect(lookup('L01-OP010').map(x => x.id)).toEqual(['1', '2', '6', '7'])
    })
  })

  describe('useStaggeredRefresh', () => {
    afterEach(() => vi.useRealTimers())

    it('recomputes each key once per interval, one slot at a time, and writes only changes', async () => {
      vi.useFakeTimers()
      const source: Record<string, number> = { a: 1, b: 1, c: 1, d: 1, e: 1, f: 1 }
      let api!: ReturnType<typeof useStaggeredRefresh<{ v: number }>>
      const Host = defineComponent({
        setup() {
          api = useStaggeredRefresh<{ v: number }>({
            keys: () => Object.keys(source),
            compute: key => ({ v: source[key]! }),
            equals: (x, y) => x.v === y.v,
            intervalMs: 500,
            slots: 5
          })
          return () => h('div')
        }
      })
      const wrapper = mount(Host)
      // Known keys have a value at once.
      expect(Object.fromEntries(Object.entries(api.states).map(([k, s]) => [k, s.v]))).toEqual({ a: 1, b: 1, c: 1, d: 1, e: 1, f: 1 })
      const unchanged = api.states.b

      for (const key of Object.keys(source)) source[key] = 2
      vi.advanceTimersByTime(100) // slot 0: indexes 0 and 5
      expect(['a', 'b', 'c', 'd', 'e', 'f'].map(k => api.states[k]!.v)).toEqual([2, 1, 1, 1, 1, 2])
      vi.advanceTimersByTime(100) // slot 1
      expect(api.states.b!.v).toBe(2)
      expect(api.states.c!.v).toBe(1)
      vi.advanceTimersByTime(300) // slots 2-4: every key refreshed within 500 ms
      expect(Object.values(api.states).every(s => s.v === 2)).toBe(true)

      // Unchanged values keep their object (no redraw).
      source.b = 2
      const before = api.states.b
      vi.advanceTimersByTime(500)
      expect(api.states.b).toBe(before)
      expect(before).not.toBe(unchanged)

      // Urgent refresh: right away, for the given keys only.
      source.c = 3
      source.d = 3
      api.refreshNow(['c'])
      expect(api.states.c!.v).toBe(3)
      expect(api.states.d!.v).toBe(2)

      wrapper.unmount()
      source.d = 4
      vi.advanceTimersByTime(1000)
      expect(api.states.d!.v).toBe(2) // timer stopped with the component
    })
  })

  describe('MapStationOverlays', () => {
    const fetchMock = vi.fn(async () => [])
    beforeEach(() => {
      vi.useFakeTimers()
      vi.stubGlobal('$fetch', fetchMock)
      vi.stubGlobal('useState', (_key: string, init?: () => any) => ({ value: init ? init() : undefined }))
    })
    afterEach(() => {
      vi.unstubAllGlobals()
      vi.useRealTimers()
    })

    // 400x400 drawing in an 800x800 box: scale 2. A sits at (200, 200), B at (600, 600).
    const entities = [
      { handle: 'A', name: 'Station A', position: { x: 100, y: 100 } },
      { handle: 'B', name: 'Station B', position: { x: 300, y: 300 } }
    ]
    const baseProps = { entities, viewBox: { x: 0, y: 0, width: 400, height: 400 }, containerWidth: 800, containerHeight: 800, stationTickets: [] }

    const mountOverlays = async (props: Record<string, unknown>) => {
      const wrapper = mount(MapStationOverlays, { props: { ...baseProps, ...props } })
      await vi.dynamicImportSettled()
      await nextTick()
      return wrapper
    }
    const tones = (wrapper: ReturnType<typeof mount>) =>
      Object.fromEntries(wrapper.findAll('[data-pin-handle]').map(p => [p.attributes('data-pin-handle'), p.attributes('data-pin-tone')]))

    it('draws one colour-coded pin per station standing on the station, filtered by mode', async () => {
      const stationKpis = reactive({ A: kpi('A', { operationalStatus: 'FAULTED' }), B: kpi('B') })
      const all = await mountOverlays({ stationKpis, filterMode: 'all' })
      expect(tones(all)).toEqual({ A: 'critical', B: 'running' })
      // Tip on the top edge of A's glyph: anchor y 200 - 10 x scale 2 = 180.
      const pinA = all.find('[data-pin-handle="A"]')
      expect(pinA.attributes('style')).toContain('left: 193px')
      expect(pinA.attributes('style')).toContain('top: 162px')
      expect(pinA.attributes('title')).toBe('A · Faulted')

      const alerts = await mountOverlays({ stationKpis, filterMode: 'alerts_only' })
      expect(tones(alerts)).toEqual({ A: 'critical' })
      const hover = await mountOverlays({ stationKpis, filterMode: 'hover_only', hoveredHandle: 'B' })
      expect(tones(hover)).toEqual({ B: 'running' })
    })

    it('updates pins on their staggered turn, not on every data change', async () => {
      const stationKpis = reactive({ A: kpi('A'), B: kpi('B') })
      const wrapper = await mountOverlays({ stationKpis, filterMode: 'all' })
      stationKpis.A = kpi('A', { operationalStatus: 'IDLE' })
      stationKpis.B = kpi('B', { operationalStatus: 'MAINTENANCE' })
      await nextTick()
      expect(tones(wrapper)).toEqual({ A: 'running', B: 'running' })

      vi.advanceTimersByTime(100) // A's slot
      await nextTick()
      expect(tones(wrapper)).toEqual({ A: 'idle', B: 'running' })
      vi.advanceTimersByTime(100) // B's slot
      await nextTick()
      expect(tones(wrapper)).toEqual({ A: 'idle', B: 'maintenance' })
    })

    it('applies a hub status change or ticket event at once', async () => {
      const stationKpis = reactive({ A: kpi('A'), B: kpi('B') })
      const tickets = reactive<any[]>([])
      const wrapper = await mountOverlays({ stationKpis, stationTickets: tickets, filterMode: 'all' })
      const fire = (ev: any) => { for (const listener of [...hubListeners]) listener({ timestamp: '', ...ev }) }

      // The KPI store has committed the fault; the hub message makes the pin follow immediately.
      stationKpis.B = kpi('B', { operationalStatus: 'FAULTED' })
      fire({ type: 'StationTelemetryReceived', stationName: 'B', summary: { handle: 'B', operationalStatus: 'FAULTED' } })
      vi.advanceTimersByTime(1)
      await nextTick()
      expect(tones(wrapper).B).toBe('critical')

      // A routine sample with the same status does not jump the queue.
      stationKpis.A = kpi('A', { oee: 50 })
      fire({ type: 'StationTelemetryReceived', stationName: 'A', summary: { handle: 'A', operationalStatus: 'RUNNING', oee: 50 } })
      vi.advanceTimersByTime(1)
      await nextTick()
      expect(tones(wrapper).A).toBe('running')

      // A ticket event re-evaluates the pins right away (SLA).
      tickets.push({ id: 't1', title: 'Leak', status: 'Open', priority: 'Critical', stationId: 'A', createdAt: '', updatedAt: '', comments: [] })
      fire({ type: 'TicketCreated', ticketId: 't1' })
      vi.advanceTimersByTime(1)
      await nextTick()
      expect(wrapper.find('[data-pin-handle="A"]').attributes('title')).toBe('A · 1 critical ticket · 1 open ticket')
    })

    it('replaces the pin of an opened station with its card and toggles through the pin', async () => {
      const stationKpis = reactive({ A: kpi('A'), B: kpi('B') })
      const wrapper = await mountOverlays({ stationKpis, filterMode: 'all', expandedHandles: ['A'] })
      expect(tones(wrapper)).toEqual({ B: 'running' })
      expect(wrapper.find('[data-overlay-handle="A"] .expanded-card').exists()).toBe(true)

      await wrapper.find('[data-pin-handle="B"]').trigger('click')
      expect(wrapper.emitted('toggle-expand')).toEqual([['B']])
    })
  })
})
