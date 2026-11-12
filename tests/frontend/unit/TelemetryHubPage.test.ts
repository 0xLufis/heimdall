import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { reactive, ref } from 'vue'

// Mounting imports the page's whole module graph. That first, cold import takes several seconds
// when the full suite runs in parallel, longer than the default 5 s test timeout.
vi.setConfig({ testTimeout: 20_000 })

const route = reactive<{ query: Record<string, any> }>({ query: {} })
const replace = vi.fn((to: any) => { route.query = { ...(to?.query || {}) } })
vi.mock('vue-router', () => ({
  useRoute: () => route,
  useRouter: () => ({ replace, push: replace })
}))

const NOW = Date.parse('2026-11-10T12:00:00Z')
const iso = (msAgo: number) => new Date(NOW - msAgo).toISOString()

const kpi = (over: Record<string, unknown>) => ({
  stationId: 'st-1', handle: 'L01-OP010', name: 'Station', lineId: 'l1', lineName: 'Line 1',
  oee: null, availability: null, performance: null, quality: null, yield: null,
  partsPerHour: null, targetPartsPerHour: null, cycleTimeSeconds: null, targetCycleTimeSeconds: null,
  totalPartsProduced: null, scrapParts: null, operationalStatus: 'UNKNOWN',
  activeTicketCount: 0, criticalTicketCount: 0, hasSlaBreach: false, lastTelemetryAt: null,
  ...over
})

const stationKpis = [
  kpi({ stationId: 'st-conveyor', handle: 'L01-OP010', name: 'Infeed Conveyor', oee: 91.2, operationalStatus: 'RUNNING', lastTelemetryAt: iso(20_000) }),
  kpi({
    stationId: 'st-laser', handle: 'L02-OP060', name: 'Rotor Laser Welding Cell', lineName: 'Line 2',
    oee: 61.5, availability: 80, performance: 85, quality: 90.4, partsPerHour: 96, targetPartsPerHour: 150,
    cycleTimeSeconds: 26.4, targetCycleTimeSeconds: 24, totalPartsProduced: 1840, scrapParts: 37, yield: 97.99,
    powerKw: 5.71, temperatureC: 53.4, operationalStatus: 'WARNING',
    activeTicketCount: 2, criticalTicketCount: 1, hasSlaBreach: true, lastTelemetryAt: iso(5_000)
  }),
  kpi({ stationId: 'st-silent', handle: 'L03-OP020', name: 'Housing CNC', lineName: 'Line 3' })
]

const clientPcs = [
  { id: 'ipc-laser', hostname: 'nv-ipc-l2-laser', name: 'NV-IPC-L2-LASER', macAddress: '02:00:00:00:00:02', lastOnline: iso(30_000), machines: [{ id: 'st-laser', name: 'NV-L2-OP060' }], systemMetadata: { IPAddress: '10.42.2.16' }, resourceAverages: { cpuUsageAverage: 78.4, ramUsageAverage: 71.3 } },
  { id: 'ipc-l1', hostname: 'nv-ipc-l1-main', name: 'NV-IPC-L1-MAIN', macAddress: '02:00:00:00:00:01', lastOnline: iso(30_000), machines: [{ id: 'st-conveyor', name: 'NV-L1-OP010' }] }
]

const fetchMock = vi.fn(async (url: string) => {
  if (url.startsWith('/api/telemetry/station-kpis')) return stationKpis
  if (url.startsWith('/api/proxy/ClientPc')) return clientPcs
  return []
})

const stubs = {
  NuxtLink: { props: ['to'], template: '<a :data-to="JSON.stringify(to)"><slot /></a>' },
  AlertRulesManager: { template: '<div />' },
  SimulatorControlModal: { props: ['open'], template: '<div data-sim :data-open="String(open)" />' },
  Badge: { template: '<span><slot /></span>' }
}

describe('Telemetry hub page (/dashboard/telemetry)', () => {
  const nuxtStates = new Map<string, any>()

  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date', 'setInterval', 'clearInterval'] })
    vi.setSystemTime(NOW)
    nuxtStates.clear()
    route.query = {}
    replace.mockClear()
    fetchMock.mockClear()
    vi.stubGlobal('definePageMeta', vi.fn())
    vi.stubGlobal('$fetch', fetchMock)
    vi.stubGlobal('useRuntimeConfig', () => ({ public: { enableDevFeatures: true } }))
    vi.stubGlobal('useState', (key: string, init?: () => any) => {
      if (!nuxtStates.has(key)) nuxtStates.set(key, ref(init ? init() : undefined))
      return nuxtStates.get(key)
    })
  })

  const mountPage = async () => {
    const Page = (await import('../../../src/frontend/web/app/pages/dashboard/telemetry/index.vue')).default
    const wrapper = mount(Page, { global: { stubs } })
    await flushPromises()
    await flushPromises()
    return wrapper
  }

  it('lists stations from the backend, summarises the fleet and never shows a fabricated reading', async () => {
    const wrapper = await mountPage()
    const text = wrapper.text()

    expect(fetchMock).toHaveBeenCalledWith('/api/telemetry/station-kpis')
    expect(text).toContain('Infeed Conveyor')
    expect(text).toContain('Rotor Laser Welding Cell')
    expect(text).toContain('Housing CNC')
    // 2 of 3 stations reported in the last 5 minutes; one needs attention
    expect(text).toMatch(/Reporting\s*2\s*\/ 3/)
    expect(text).toMatch(/Needs Attention\s*1/)
    expect(text).toContain('no telemetry')
    expect(text).not.toContain('NaN')
    wrapper.unmount()
  })

  it('opens the station named in ?station= (by handle) and shows its own KPIs, tickets and controller', async () => {
    route.query = { station: 'l02-op060' }
    const wrapper = await mountPage()
    const text = wrapper.text()

    expect(wrapper.find('h2').text()).toBe('Rotor Laser Welding Cell')
    expect(text).toContain('SLA breached')
    expect(text).toContain('1 critical')
    expect(text).toContain('61.5')
    expect(text).toContain('1,840')
    // Last known dynamics are seeded from the snapshot; vibration was never reported by this cell
    expect(text).toContain('5.71')
    expect(text).toContain('53.4')
    expect(text).toContain('Not reported by this station')
    // Only the controller bound to this station is listed
    expect(text).toContain('nv-ipc-l2-laser')
    expect(text).not.toContain('nv-ipc-l1-main')

    const links = wrapper.findAll('a').map(a => a.attributes('data-to') || '')
    expect(links.some(l => l.includes('/dashboard/machines') && l.includes('st-laser'))).toBe(true)
    expect(links.some(l => l.includes('/dashboard/clients') && l.includes('ipc-laser'))).toBe(true)
    expect(links.some(l => l.includes('/dashboard/tickets/new') && l.includes('st-laser'))).toBe(true)
    wrapper.unmount()
  })

  it('resolves ?controllerId= to the station that controller runs', async () => {
    route.query = { controllerId: 'ipc-l1' }
    const wrapper = await mountPage()
    expect(wrapper.find('h2').text()).toBe('Infeed Conveyor')
    wrapper.unmount()
  })

  it('selecting a station in the directory switches the detail view and updates the URL', async () => {
    const wrapper = await mountPage()
    const row = wrapper.findAll('button').find(b => b.text().includes('Housing CNC'))
    expect(row).toBeDefined()
    await row!.trigger('click')
    await flushPromises()

    expect(wrapper.find('h2').text()).toBe('Housing CNC')
    expect(replace).toHaveBeenCalledWith(expect.objectContaining({ query: expect.objectContaining({ station: 'st-silent' }) }))
    expect(wrapper.text()).toContain('No telemetry has been received from this station')
    wrapper.unmount()
  })

  it('fleet tiles filter the directory', async () => {
    const wrapper = await mountPage()
    const attention = wrapper.findAll('button').find(b => b.text().includes('Needs Attention'))
    await attention!.trigger('click')

    const rows = wrapper.findAll('button[aria-current], button.w-full').map(b => b.text())
    expect(rows.some(r => r.includes('Rotor Laser Welding Cell'))).toBe(true)
    expect(rows.some(r => r.includes('Infeed Conveyor'))).toBe(false)
    wrapper.unmount()
  })

  it('surfaces a backend failure instead of an empty or invented fleet', async () => {
    fetchMock.mockImplementation(async (url: string) => {
      if (url.startsWith('/api/telemetry/station-kpis')) throw Object.assign(new Error('Bad Gateway'), { data: { message: 'The Heimdall backend is currently unavailable.' } })
      return []
    })
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('Station telemetry could not be loaded')
    expect(wrapper.text()).toContain('currently unavailable')
    wrapper.unmount()
  })
})
