import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import FleetStationsExplorer from '../../../src/frontend/web/app/components/analytics/FleetStationsExplorer.vue'
import {
  DEFAULT_VIEW, FLEET_COLUMNS, NO_DATA_MARK, UNASSIGNED_KEY, VIEW_STORAGE_KEY,
  activeFilterCount, computeVirtualWindow, dimensionOptions, filterStations, formatAge, formatDurationMinutes,
  getColumn, groupStations, loadViewState, needsAttention, normalizeFleetStation, normalizeFleetStations,
  orderGroups, rollUp, rollupCellText, sanitizeViewState, saveViewState, sortStations, stateOptions,
  stationCellText, stationContextData, stationMachineLink, stationTicketsLink,
  type FleetStation
} from '../../../src/frontend/web/app/utils/fleetAnalytics'

const ctx = { generatedAtMs: Date.parse('2026-11-11T10:00:00Z') }

/** A backend row; only what a test sets differs from "a station that never reported". */
const raw = (id: string, overrides: Record<string, unknown> = {}) => ({
  stationId: id,
  handle: id.toUpperCase(),
  name: `Station ${id}`,
  machineName: id,
  liveState: 'NO_DATA',
  operationalStatus: 'UNKNOWN',
  lifecycleStatus: 'active',
  activeTicketCount: 0,
  criticalTicketCount: 0,
  hasSlaBreach: false,
  incidentCount: 0,
  resolvedIncidentCount: 0,
  controllerCount: 0,
  controllersOnline: 0,
  ...overrides
})
const station = (id: string, overrides: Record<string, unknown> = {}): FleetStation =>
  normalizeFleetStation(raw(id, overrides))!

const fleet = (): FleetStation[] => [
  station('a', { name: 'L01-OP010', lineName: 'Line 01', machineType: 'RobotCell', organizationId: 'org-1', organizationName: 'Plant One',
    liveState: 'RUNNING', oee: 90, availability: 95, partsPerHour: 100, targetPartsPerHour: 200, vibrationMmS: 1.2,
    totalPartsProduced: 1000, scrapParts: 10, controllerCount: 2, controllersOnline: 2, telemetryAgeSeconds: 3 }),
  station('b', { name: 'L01-OP020', lineName: 'Line 01', machineType: 'ServoPress', organizationId: 'org-1', organizationName: 'Plant One',
    liveState: 'FAULTED', oee: 70, partsPerHour: 50, targetPartsPerHour: 100, vibrationMmS: 3.4,
    totalPartsProduced: 500, scrapParts: 40, activeTicketCount: 2, criticalTicketCount: 1, hasSlaBreach: true,
    incidentCount: 3, resolvedIncidentCount: 1, mttrMinutes: 60, downtimeMinutes: 30, controllerCount: 1, controllersOnline: 0 }),
  station('c', { name: 'L02-OP010', lineId: 'G-2', lineName: 'Line 02', machineType: 'RobotCell', organizationId: 'org-2',
    liveState: 'STALE', oee: 50, telemetryAgeSeconds: 900, incidentCount: 1, resolvedIncidentCount: 3, mttrMinutes: 20,
    oldestOpenTicketAt: '2026-11-10T10:00:00Z' }),
  station('d', { name: 'L10-OP010' })
]

describe('fleet analytics: normalization', () => {
  it('keeps unreported values null instead of turning them into zeros', () => {
    const s = station('x', { oee: null, partsPerHour: '', temperatureC: 'n/a', vibrationMmS: '1.5', liveState: 'bogus' })
    expect(s.oee).toBeNull()
    expect(s.partsPerHour).toBeNull()
    expect(s.temperatureC).toBeNull()
    expect(s.vibrationMmS).toBe(1.5)
    expect(s.liveState).toBe('NO_DATA')
    expect(s.lineName).toBeNull()
    expect(s.activeTicketCount).toBe(0)
  })

  it('rejects rows without an id and responses that are not a station list', () => {
    expect(normalizeFleetStation({ name: 'no id' })).toBeNull()
    expect(() => normalizeFleetStations({ statusCode: 502, message: 'down' })).toThrow()
    const payload = normalizeFleetStations({ generatedAt: '2026-11-11T10:00:00Z', windowDays: 7, telemetryStaleAfterSeconds: 60, criticalSlaHours: 12, stations: [raw('a'), { nope: true }] })
    expect(payload.stations).toHaveLength(1)
    expect(payload.meta).toMatchObject({ windowDays: 7, telemetryStaleAfterSeconds: 60, criticalSlaHours: 12 })
  })
})

describe('fleet analytics: roll-ups', () => {
  it('aggregates each metric the way its column says and ignores stations without a value', () => {
    const r = rollUp(fleet(), ctx)

    expect(r.stationCount).toBe(4)
    expect(r.states).toMatchObject({ RUNNING: 1, FAULTED: 1, STALE: 1, NO_DATA: 1 })
    expect(r.reporting).toBe(2)
    expect(r.attention).toBe(2)                                   // faulted + stale

    expect(r.metrics.oee).toEqual({ value: 70, n: 3 })             // mean of 90, 70, 50 — the silent station is not a 0
    expect(r.metrics.availability).toEqual({ value: 95, n: 1 })
    expect(r.metrics.partsPerHour).toEqual({ value: 150, n: 2 })   // sum
    expect(r.metrics.vibration).toEqual({ value: 3.4, n: 2 })      // highest
    expect(r.metrics.attainment).toMatchObject({ value: 50, n: 2, numerator: 150, denominator: 300 })   // ratio of sums
    expect(r.metrics.scrapRate!.value).toBeCloseTo((50 / 1500) * 100, 6)
    expect(r.metrics.openTickets).toEqual({ value: 2, n: 4 })      // counts are stored data: every station contributes
    expect(r.metrics.slaBreaches!.value).toBe(1)
    expect(r.metrics.incidents!.value).toBe(4)
    expect(r.metrics.mttr).toMatchObject({ value: 30, n: 2 })      // (60×1 + 20×3) / 4 tickets, not the mean of 60 and 20
    expect(r.metrics.downtime).toEqual({ value: 30, n: 1 })
    // stations whose bound PCs are all online / stations with a PC (a: 2 of 2 online, b: 0 of 1)
    expect(r.metrics.controllers).toMatchObject({ value: 50, n: 2, numerator: 1, denominator: 2 })
    expect(r.metrics.oldestOpenTicket!.value).toBe(24)             // hours before the response time
    expect(r.metrics.temperature).toEqual({ value: null, n: 0 })   // nobody reports it
  })

  it('renders a roll-up without contributors as the no-data mark and a fraction as counts', () => {
    const r = rollUp(fleet(), ctx)
    expect(rollupCellText(r.metrics.temperature, getColumn('temperature')!)).toBe(NO_DATA_MARK)
    expect(rollupCellText(r.metrics.controllers, getColumn('controllers')!)).toBe('1/2')
    expect(rollupCellText(r.metrics.mttr, getColumn('mttr')!)).toBe('30 min')
    expect(rollupCellText(rollUp([], ctx).metrics.oee, getColumn('oee')!)).toBe(NO_DATA_MARK)
  })

  it('formats station cells, with "—" for anything the station does not report', () => {
    const [a, , , d] = fleet()
    expect(stationCellText(a!, getColumn('oee')!, ctx)).toBe('90.0')
    expect(stationCellText(a!, getColumn('attainment')!, ctx)).toBe('50')
    expect(stationCellText(a!, getColumn('controllers')!, ctx)).toBe('2/2')
    expect(stationCellText(a!, getColumn('state')!, ctx)).toBe('Running')
    expect(stationCellText(d!, getColumn('oee')!, ctx)).toBe(NO_DATA_MARK)
    expect(stationCellText(d!, getColumn('controllers')!, ctx)).toBe(NO_DATA_MARK)
    expect(stationCellText(d!, getColumn('line')!, ctx)).toBe(NO_DATA_MARK)
    expect(stationCellText(d!, getColumn('openTickets')!, ctx)).toBe('0')
    expect(formatDurationMinutes(1626)).toBe('27.1 h')
    expect(formatDurationMinutes(null)).toBe(NO_DATA_MARK)
    expect(formatAge(4)).toBe('4 s')
    expect(formatAge(900)).toBe('15 min')
  })
})

describe('fleet analytics: grouping, sorting, filtering', () => {
  it('groups by a dimension with a roll-up per group and the unassigned stations last', () => {
    const groups = groupStations(fleet(), 'line', ctx)
    expect(groups.map(g => [g.label, g.rows.length, g.isUnassigned])).toEqual([
      ['Line 01', 2, false], ['Line 02', 1, false], ['No line assigned', 1, true]
    ])
    expect(groups[0]!.rollup.metrics.oee).toEqual({ value: 80, n: 2 })
    expect(groups[2]!.key).toBe(UNASSIGNED_KEY)
    expect(groups[2]!.rollup.metrics.oee!.value).toBeNull()

    expect(groupStations(fleet(), 'machineType', ctx).map(g => g.label)).toEqual(['RobotCell', 'ServoPress', 'Type not set'])
    expect(groupStations(fleet(), 'organization', ctx).map(g => g.label)).toEqual(['org-2', 'Plant One', 'No organization'])
    expect(groupStations(fleet(), 'liveState', ctx).map(g => g.label)).toEqual(['Faulted', 'Stale', 'Running', 'No data'])
    expect(groupStations(fleet(), 'subLine', ctx)).toHaveLength(1)        // nobody has a sub-line: one unassigned group
    const all = groupStations(fleet(), 'none', ctx)
    expect(all).toHaveLength(1)
    expect(all[0]!.rows).toHaveLength(4)
  })

  it('sorts numerically with stations lacking the value last in both directions', () => {
    const names = (rows: FleetStation[]) => rows.map(r => r.stationId)
    expect(names(sortStations(fleet(), { key: 'oee', direction: 'desc' }, ctx))).toEqual(['a', 'b', 'c', 'd'])
    expect(names(sortStations(fleet(), { key: 'oee', direction: 'asc' }, ctx))).toEqual(['c', 'b', 'a', 'd'])
    expect(names(sortStations(fleet(), { key: 'state', direction: 'asc' }, ctx))).toEqual(['b', 'c', 'a', 'd'])
    // natural order: L02 before L10
    expect(names(sortStations(fleet(), { key: 'name', direction: 'desc' }, ctx))).toEqual(['d', 'c', 'b', 'a'])
  })

  it('orders groups by label or by the roll-up of the sorted column', () => {
    const groups = groupStations(fleet(), 'line', ctx)
    expect(orderGroups(groups, 'metric', { key: 'oee', direction: 'asc' }).map(g => g.label)).toEqual(['Line 02', 'Line 01', 'No line assigned'])
    expect(orderGroups(groups, 'metric', { key: 'name', direction: 'desc' }).map(g => g.label)).toEqual(['Line 01', 'Line 02', 'No line assigned'])
    expect(orderGroups(groups, 'label', { key: 'oee', direction: 'asc' }).map(g => g.label)).toEqual(['Line 01', 'Line 02', 'No line assigned'])
  })

  it('filters by search, line, organization, type, state and attention', () => {
    const rows = fleet()
    const ids = (f: Partial<Parameters<typeof filterStations>[1]>) =>
      filterStations(rows, { search: '', lines: [], organizations: [], machineTypes: [], states: [], attentionOnly: false, ...f }).map(r => r.stationId)

    expect(ids({})).toEqual(['a', 'b', 'c', 'd'])
    expect(ids({ search: 'op020' })).toEqual(['b'])
    expect(ids({ search: 'servo' })).toEqual(['b'])
    expect(ids({ lines: ['name:line 01'] })).toEqual(['a', 'b'])
    expect(ids({ lines: ['id:g-2', UNASSIGNED_KEY] })).toEqual(['c', 'd'])
    expect(ids({ organizations: ['id:org-2'] })).toEqual(['c'])
    expect(ids({ machineTypes: ['name:robotcell'], states: ['RUNNING'] })).toEqual(['a'])
    expect(ids({ attentionOnly: true })).toEqual(['b', 'c'])
    expect(needsAttention(rows[3]!)).toBe(false)

    expect(dimensionOptions(rows, 'line').map(o => [o.key, o.count])).toEqual([['name:line 01', 2], ['id:g-2', 1], [UNASSIGNED_KEY, 1]])
    expect(stateOptions(rows).map(o => o.key)).toEqual(['FAULTED', 'STALE', 'RUNNING', 'NO_DATA'])
    expect(activeFilterCount({ search: ' x ', lines: ['a'], organizations: [], machineTypes: [], states: [], attentionOnly: true })).toBe(3)
  })

  it('builds deep links and the context-menu binding from the machine id', () => {
    const s = station('m-1', { name: 'L01 Press', machineName: 'OP10', customIdentifier: 'L01-OP010', pinnedObjectHandle: '9C1D' })
    expect(stationMachineLink(s)).toEqual({ path: '/dashboard/machines', query: { search: 'L01-OP010', tree: 'm-1' } })
    expect(stationTicketsLink(s)).toEqual({ path: '/dashboard/tickets', query: { stationId: 'm-1' } })
    expect(stationContextData(s)).toEqual({ entityType: 'machine', entityId: 'm-1', entityName: 'L01 Press', machineId: 'm-1', machineName: 'OP10', handle: '9C1D' })
    expect(stationContextData(station('m-2')).handle).toBeUndefined()
  })
})

describe('fleet analytics: persisted view', () => {
  it('drops unknown keys and falls back to defaults', () => {
    const view = sanitizeViewState({
      groupBy: 'machineType', groupOrder: 'metric', columns: ['oee', 'nope', 'oee', 'mttr'], sort: { key: 'mttr', direction: 'desc' },
      filters: { search: 'x', lines: ['id:1', 5], states: ['RUNNING', 'EXPLODED'], attentionOnly: true },
      windowDays: 7, collapsed: { machineType: ['name:robotcell'], bogus: ['x'] }, density: 'compact', autoRefreshSeconds: 10
    })
    expect(view).toMatchObject({
      groupBy: 'machineType', groupOrder: 'metric', columns: ['oee', 'mttr'], sort: { key: 'mttr', direction: 'desc' },
      windowDays: 7, density: 'compact', autoRefreshSeconds: 10, collapsed: { machineType: ['name:robotcell'] }
    })
    expect(view.filters).toEqual({ search: 'x', lines: ['id:1'], organizations: [], machineTypes: [], states: ['RUNNING'], attentionOnly: true })

    const junk = sanitizeViewState({ groupBy: 'galaxy', sort: { key: 'nope' }, windowDays: 13, autoRefreshSeconds: 1, columns: 'oee' })
    expect(junk).toEqual(DEFAULT_VIEW)
    expect(sanitizeViewState('garbage')).toEqual(DEFAULT_VIEW)
    expect(DEFAULT_VIEW.columns.every(key => FLEET_COLUMNS.some(c => c.key === key))).toBe(true)
  })

  it('survives storage that is missing, broken or throws', () => {
    const store = new Map<string, string>()
    const storage = { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => { store.set(k, v) } }
    const view = { ...sanitizeViewState(null), groupBy: 'organization' as const, windowDays: 90 }

    expect(saveViewState(view, storage)).toBe(true)
    expect(loadViewState(storage)).toMatchObject({ groupBy: 'organization', windowDays: 90 })

    store.set(VIEW_STORAGE_KEY, '{not json')
    expect(loadViewState(storage)).toEqual(DEFAULT_VIEW)
    expect(loadViewState(null)).toEqual(DEFAULT_VIEW)
    const throwing = { getItem: () => { throw new Error('blocked') }, setItem: () => { throw new Error('quota') } }
    expect(loadViewState(throwing)).toEqual(DEFAULT_VIEW)
    expect(saveViewState(view, throwing)).toBe(false)
    expect(saveViewState(view, null)).toBe(false)
  })
})

describe('fleet analytics: virtual window', () => {
  const options = { rowHeight: 40, headerHeight: 44, overscan: 2 }

  it('renders only the rows that intersect the viewport and keeps every section at full height', () => {
    const sections = [{ rowCount: 100, collapsed: false }, { rowCount: 50, collapsed: false }]
    const top = computeVirtualWindow(sections, { ...options, scrollTop: 0, viewportHeight: 400 })
    expect(top.totalHeight).toBe(44 + 100 * 40 + 44 + 50 * 40)
    expect(top.sections[0]).toMatchObject({ top: 0, height: 4044, start: 0, end: 11, padTop: 0, padBottom: 89 * 40 })
    expect(top.sections[1]).toMatchObject({ top: 4044, start: 0, end: 0, padBottom: 50 * 40 })

    const middle = computeVirtualWindow(sections, { ...options, scrollTop: 2000, viewportHeight: 400 })
    expect(middle.sections[0]).toMatchObject({ start: 46, end: 61 })
    expect(middle.sections[0]!.padTop + (61 - 46) * 40 + middle.sections[0]!.padBottom).toBe(100 * 40)

    const second = computeVirtualWindow(sections, { ...options, scrollTop: 4100, viewportHeight: 400 })
    expect(second.sections[0]).toMatchObject({ start: 99, end: 100 })     // overscan above the viewport
    expect(second.sections[1]!.start).toBe(0)
    expect(second.sections[1]!.end).toBeGreaterThan(8)
  })

  it('gives collapsed sections only their header and tolerates an unmeasured viewport', () => {
    const w = computeVirtualWindow([{ rowCount: 500, collapsed: true }, { rowCount: 3, collapsed: false }], { ...options, scrollTop: 0, viewportHeight: 0 })
    expect(w.sections[0]).toMatchObject({ height: 44, start: 0, end: 0, padTop: 0, padBottom: 0 })
    expect(w.sections[1]).toMatchObject({ top: 44, start: 0, end: 3 })
    expect(w.totalHeight).toBe(44 + 44 + 3 * 40)
    // 5 000 rows never render more than the window plus overscan.
    const big = computeVirtualWindow([{ rowCount: 5000, collapsed: false }], { ...options, scrollTop: 100_000, viewportHeight: 600 })
    expect(big.sections[0]!.end - big.sections[0]!.start).toBeLessThanOrEqual(22)
  })
})

describe('FleetStationsExplorer', () => {
  const response = () => ({
    generatedAt: '2026-11-11T10:00:00Z', windowDays: 30, windowStart: '2026-10-12T10:00:00Z',
    telemetryStaleAfterSeconds: 60, controllerOnlineWindowSeconds: 300, criticalSlaHours: 12,
    stations: [
      raw('a', { name: 'L01-OP010', lineName: 'Line 01', machineType: 'RobotCell', liveState: 'RUNNING', oee: 90, controllerCount: 1, controllersOnline: 1 }),
      raw('b', { name: 'L01-OP020', lineName: 'Line 01', machineType: 'ServoPress', liveState: 'FAULTED', oee: 70, activeTicketCount: 2, criticalTicketCount: 1 }),
      raw('c', { name: 'L02-OP010', lineName: 'Line 02', machineType: 'RobotCell' })
    ]
  })
  let stored: Map<string, string>
  let fetchMock: ReturnType<typeof vi.fn>

  const mountExplorer = async () => {
    const wrapper = mount(FleetStationsExplorer, {
      global: {
        stubs: { NuxtLink: { template: '<a><slot /></a>' }, Popover: { template: '<div><slot /></div>' }, PopoverTrigger: { template: '<div><slot /></div>' }, PopoverContent: { template: '<div><slot /></div>' } },
        directives: { ctx: {} }
      }
    })
    await flushPromises()
    return wrapper
  }

  beforeEach(() => {
    stored = new Map()
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => stored.get(k) ?? null,
      setItem: (k: string, v: string) => { stored.set(k, v) },
      removeItem: (k: string) => { stored.delete(k) },
      clear: () => stored.clear()
    })
    fetchMock = vi.fn(async () => response())
    vi.stubGlobal('$fetch', fetchMock)
  })
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows grouped stations with roll-ups from the loaded rows and explicit "No data" where nothing is reported', async () => {
    const wrapper = await mountExplorer()

    expect(fetchMock).toHaveBeenCalledWith('/api/analytics/fleet-stations', { query: { windowDays: 30 } })
    expect(wrapper.findAll('[data-testid="fleet-row"]')).toHaveLength(3)
    const groups = wrapper.findAll('section')
    expect(groups).toHaveLength(2)
    expect(groups[0]!.text()).toContain('Line 01')
    expect(groups[0]!.text()).toContain('80.0')                       // mean OEE of 90 and 70
    expect(groups[1]!.text()).toContain('Line 02')
    expect(groups[1]!.text()).toContain('0/1 reporting')

    expect(wrapper.find('[data-testid="fleet-tile-stations"]').text()).toContain('3')
    expect(wrapper.find('[data-testid="fleet-tile-oee"]').text()).toContain('80.0')
    expect(wrapper.find('[data-testid="fleet-tile-oee"]').text()).toContain('2 of 3 stations that report OEE')
    expect(wrapper.find('[data-testid="fleet-tile-attainment"]').text()).toContain('No data')
    expect(wrapper.find('[data-testid="fleet-tile-tickets"]').text()).toContain('2')
    expect(wrapper.find('[data-testid="fleet-tile-controllers"]').text()).toContain('1/1')
    expect(wrapper.find('[data-testid="fleet-footer"]').text()).toContain('3 of 3 stations in 2 groups')
  })

  it('filters, regroups and collapses, and stores the view for the next visit', async () => {
    const wrapper = await mountExplorer()

    await wrapper.find('[data-testid="fleet-state-FAULTED"]').trigger('click')
    expect(wrapper.findAll('[data-testid="fleet-row"]')).toHaveLength(1)
    expect(wrapper.find('[data-testid="fleet-tile-stations"]').text()).toContain('filtered, of 3')
    await wrapper.find('[data-testid="fleet-clear-filters"]').trigger('click')

    await wrapper.find('[data-testid="fleet-group-by"]').setValue('machineType')
    expect(wrapper.findAll('section').map(s => s.find('button').text().replace(/\s+/g, ''))).toEqual(['RobotCell2', 'ServoPress1'])

    await wrapper.findAll('section')[0]!.find('button').trigger('click')       // collapse RobotCell
    expect(wrapper.findAll('[data-testid="fleet-row"]')).toHaveLength(1)

    await wrapper.find('[data-testid="fleet-col-oee"]').trigger('click')
    await wrapper.find('[data-testid="fleet-window"]').setValue('7')
    await flushPromises()
    expect(fetchMock).toHaveBeenLastCalledWith('/api/analytics/fleet-stations', { query: { windowDays: 7 } })

    const saved = JSON.parse(stored.get(VIEW_STORAGE_KEY)!)
    expect(saved).toMatchObject({ groupBy: 'machineType', windowDays: 7, sort: { key: 'oee', direction: 'desc' }, collapsed: { machineType: ['name:robotcell'] } })
    wrapper.unmount()

    const again = await mountExplorer()
    expect((again.find('[data-testid="fleet-group-by"]').element as HTMLSelectElement).value).toBe('machineType')
    expect(again.findAll('[data-testid="fleet-row"]')).toHaveLength(1)
  })

  it('says so when the data cannot be loaded or no station matches', async () => {
    fetchMock.mockRejectedValueOnce(Object.assign(new Error('boom'), { statusMessage: 'The Heimdall backend is currently unavailable.' }))
    const failed = await mountExplorer()
    expect(failed.find('[data-testid="fleet-error"]').text()).toContain('currently unavailable')
    failed.unmount()

    const wrapper = await mountExplorer()
    await wrapper.find('[data-testid="fleet-search"]').setValue('no such station')
    expect(wrapper.find('[data-testid="fleet-no-match"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="fleet-tile-oee"]').text()).toContain('No data')
  })
})
