import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import SimulatorControlModal from '~/components/dashboard/SimulatorControlModal.vue'
import simulatorHandler from '~~/server/api/simulator/[...action]'

const onlineStatus = () => ({
  active_fleet: 6,
  active_stations: 2,
  total_dispatched: 120,
  total_errors: 0,
  fault_rate: 0.02,
  speed: 1,
  dataset: 'demo',
  transport: { protocol: 'mqtt5', broker: '127.0.0.1:1883', connected: true, error: null },
  lines: ['L01', 'L02'],
  drift_profiles: ['bearing_wear', 'cycle_slowdown', 'laser_drift', 'thermal'],
  scenarios: ['demo_story', 'clear'],
  nodes: [{ hostname: 'nv-ipc-l2-laser', profile: 'Beckhoff TwinCAT 3 IPC' }],
  stations: [
    {
      id: 'NV-L2-OP060', name: 'Rotor Laser Welding Cell', handle: 'L02-OP060', line: 'L02', machineType: 'LaserWelder',
      controller: 'HW-NV-IPC-L2-LASER', state: 'WARNING', reason: 'power low', scenario: 'drift:laser_drift', driftProgress: 0.4,
      values: { power_kw: 5.81, temperature_c: 49.2, vibration_mms: 0.61 }
    },
    {
      id: 'NV-L1-OP010', name: 'Conveyor', handle: 'L01-OP010', line: 'L01', machineType: 'ConveyorTransfer',
      controller: 'HW-NV-IPC-L1-MAIN', state: 'RUNNING', reason: 'auto', scenario: null, driftProgress: null,
      values: { power_kw: 2.2, temperature_c: 38, vibration_mms: 0.9 }
    }
  ]
})

const offlineStatus = () => ({
  sim_offline: true, error: 'fetch failed', active_fleet: 0, total_dispatched: 0, total_errors: 0, fault_rate: 0,
  nodes: [], stations: [], lines: []
})

describe('Simulator control modal', () => {
  let fetchMock: ReturnType<typeof vi.fn>
  let simStatus: any

  beforeEach(() => {
    simStatus = onlineStatus()
    fetchMock = vi.fn(async (url: string, opts?: any) => {
      if (url === '/api/simulator/status') return simStatus
      if (url === '/api/tickets/settings') return { settings: { autoAssignTickets: false, devTicketGenEnabled: true } }
      if (url === '/api/tickets') return { tickets: [] }
      if (url.startsWith('/api/simulator/')) return { success: true, message: `ok ${url} ${JSON.stringify(opts?.body)}` }
      return {}
    })
    vi.stubGlobal('$fetch', fetchMock)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  const mountModal = async () => {
    const wrapper = mount(SimulatorControlModal, { props: { open: true } })
    await flushPromises()
    return wrapper
  }

  const simCalls = () => fetchMock.mock.calls.filter(c => c[1]?.method === 'POST' && String(c[0]).startsWith('/api/simulator/'))

  it('shows the real fleet numbers, station states and active scenarios', async () => {
    const wrapper = await mountModal()
    expect(wrapper.find('[data-testid="sim-offline"]').exists()).toBe(false)
    expect(wrapper.text()).toContain('Streaming Active')
    expect(wrapper.text()).toContain('demo dataset')
    expect(wrapper.text()).toContain('WARNING 1')
    expect(wrapper.text()).toContain('RUNNING 1')
    expect(wrapper.text()).toContain('drift:laser_drift')
    expect(wrapper.text()).toContain('5.81 kW')
    expect(wrapper.find('select[aria-label="Station"]').findAll('option')).toHaveLength(3)
  })

  it('every station scenario button sends its command for the selected station', async () => {
    const wrapper = await mountModal()
    await wrapper.find('select[aria-label="Station"]').setValue('NV-L2-OP060')

    await wrapper.find('[data-testid="sim-station-fault"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-station-maintenance"]').trigger('click')
    await flushPromises()
    await wrapper.find('select[aria-label="Drift profile"]').setValue('bearing_wear')
    await wrapper.find('input[aria-label="Drift duration in minutes"]').setValue('5')
    await wrapper.find('[data-testid="sim-station-drift"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-station-recover"]').trigger('click')
    await flushPromises()

    expect(simCalls().map(c => [c[0], c[1].body])).toEqual([
      ['/api/simulator/station/fault', { station: 'NV-L2-OP060' }],
      ['/api/simulator/station/maintenance', { station: 'NV-L2-OP060' }],
      ['/api/simulator/station/drift', { station: 'NV-L2-OP060', profile: 'bearing_wear', duration_s: 300 }],
      ['/api/simulator/recover', { station: 'NV-L2-OP060' }]
    ])
    expect(wrapper.find('[role="status"]').text()).toContain('/api/simulator/recover')
  })

  it('line, preset, speed and IPC controls send their commands', async () => {
    const wrapper = await mountModal()
    await wrapper.find('select[aria-label="Line"]').setValue('L02')
    await wrapper.find('[data-testid="sim-line-stop"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-line-recover"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-scenario-demo"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-recover-all"]').trigger('click')
    await flushPromises()
    const speed10 = wrapper.findAll('[aria-label="Simulation speed"] button').find(b => b.text() === '10x')!
    await speed10.trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-inject-fault"]').trigger('click')
    await flushPromises()
    await wrapper.find('[data-testid="sim-clear-faults"]').trigger('click')
    await flushPromises()

    expect(simCalls().map(c => [c[0], c[1].body])).toEqual([
      ['/api/simulator/line/stop', { line: 'L02' }],
      ['/api/simulator/recover', { line: 'L02' }],
      ['/api/simulator/scenario', { name: 'demo_story' }],
      ['/api/simulator/scenario', { name: 'clear' }],
      ['/api/simulator/speed', { speed: 10 }],
      ['/api/simulator/fault', { hostname: undefined }],
      ['/api/simulator/clear-faults', {}]
    ])
  })

  it('station actions are disabled until a station is selected', async () => {
    const wrapper = await mountModal()
    for (const id of ['sim-station-fault', 'sim-station-maintenance', 'sim-station-drift', 'sim-station-recover', 'sim-line-stop']) {
      expect(wrapper.find(`[data-testid="${id}"]`).attributes('disabled')).toBeDefined()
    }
    await wrapper.find('[data-testid="sim-station-fault"]').trigger('click')
    await flushPromises()
    expect(simCalls()).toHaveLength(0)
  })

  it('degrades clearly when the simulator is offline: banner, no invented numbers, controls disabled', async () => {
    simStatus = offlineStatus()
    const wrapper = await mountModal()
    expect(wrapper.find('[data-testid="sim-offline"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('Fleet simulator offline')
    expect(wrapper.text()).not.toContain('Streaming Active')
    expect(wrapper.text()).not.toContain('400+')
    for (const id of ['sim-inject-fault', 'sim-clear-faults', 'sim-scenario-demo', 'sim-recover-all', 'sim-station-fault']) {
      expect(wrapper.find(`[data-testid="${id}"]`).attributes('disabled')).toBeDefined()
    }
  })

  it('treats a failing status request as offline and reports failed commands as errors', async () => {
    fetchMock.mockImplementation(async (url: string, opts?: any) => {
      if (url === '/api/simulator/status') throw Object.assign(new Error('403'), { data: { statusMessage: 'Simulation features are disabled' } })
      return {}
    })
    const offline = await mountModal()
    expect(offline.find('[data-testid="sim-offline"]').text()).toContain('Simulation features are disabled')
    offline.unmount()

    fetchMock.mockImplementation(async (url: string, opts?: any) => {
      if (url === '/api/simulator/status') return simStatus
      if (opts?.method === 'POST') throw Object.assign(new Error('boom'), { data: { statusMessage: "Unknown station 'X'" } })
      return {}
    })
    const wrapper = await mountModal()
    await wrapper.find('[data-testid="sim-scenario-demo"]').trigger('click')
    await flushPromises()
    expect(wrapper.find('[role="status"]').text()).toContain("Unknown station 'X'")
    expect(wrapper.find('[role="status"]').classes().join(' ')).toContain('rose')
  })

  it('warns when the simulator runs but cannot reach the MQTT broker', async () => {
    simStatus.transport = { protocol: 'mqtt5', broker: '127.0.0.1:1883', connected: false, error: 'Connection refused' }
    const wrapper = await mountModal()
    expect(wrapper.find('[data-testid="sim-broker-down"]').text()).toContain('Connection refused')
    expect(wrapper.text()).toContain('Broker unreachable')
  })
})

describe('Simulator BFF proxy', () => {
  const originalEnv = { ...process.env }
  let fetchMock: ReturnType<typeof vi.fn>

  const event = (action: string, method = 'GET') => ({
    context: { params: { action } },
    method,
    node: { req: { method, headers: {} } }
  }) as any

  beforeEach(() => {
    process.env = { ...originalEnv, NODE_ENV: 'development', HEIMDALL_ENABLE_DEV: 'true', HEIMDALL_ENABLE_SIMULATION: 'true' }
    delete process.env.SIMULATOR_HOST
    fetchMock = vi.fn()
    vi.stubGlobal('$fetch', fetchMock)
  })

  afterEach(() => {
    process.env = { ...originalEnv }
    vi.unstubAllGlobals()
  })

  it('forwards status to a locally running simulator first', async () => {
    fetchMock.mockResolvedValue({ active_fleet: 6 })
    await expect(simulatorHandler(event('status'))).resolves.toEqual({ active_fleet: 6 })
    expect(fetchMock.mock.calls[0][0]).toBe('http://127.0.0.1:5055/api/status')
  })

  it('falls back to the compose service and honours SIMULATOR_HOST', async () => {
    fetchMock.mockRejectedValueOnce(new Error('ECONNREFUSED')).mockResolvedValueOnce({ active_fleet: 56 })
    await expect(simulatorHandler(event('status'))).resolves.toEqual({ active_fleet: 56 })
    expect(fetchMock.mock.calls.map(c => c[0])).toEqual(['http://127.0.0.1:5055/api/status', 'http://simulator:5055/api/status'])

    fetchMock.mockReset().mockResolvedValue({ stations: [] })
    process.env.SIMULATOR_HOST = 'http://sim.internal:5055/'
    await simulatorHandler(event('stations'))
    expect(fetchMock.mock.calls.map(c => c[0])).toEqual(['http://sim.internal:5055/api/stations'])
  })

  it('reports an offline simulator explicitly instead of inventing fleet numbers', async () => {
    fetchMock.mockRejectedValue(new Error('ECONNREFUSED'))
    const res: any = await simulatorHandler(event('status'))
    expect(res).toMatchObject({ sim_offline: true, active_fleet: 0, total_dispatched: 0, nodes: [], stations: [] })
  })

  it('fails commands with 503 when the simulator is offline', async () => {
    fetchMock.mockRejectedValue(new Error('ECONNREFUSED'))
    await expect(simulatorHandler(event('station/fault', 'POST'))).rejects.toMatchObject({ statusCode: 503 })
  })

  it('passes simulator 4xx verdicts through', async () => {
    fetchMock.mockRejectedValue(Object.assign(new Error('404'), { response: { status: 404 }, data: { message: "Unknown station 'X'" } }))
    await expect(simulatorHandler(event('station/fault', 'POST'))).rejects.toMatchObject({
      statusCode: 404,
      statusMessage: "Unknown station 'X'"
    })
    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('only forwards known actions with the right method', async () => {
    for (const [action, method] of [['../admin', 'GET'], ['status/../x', 'GET'], ['station/fault', 'GET'], ['status', 'POST'], ['shutdown', 'POST']]) {
      await expect(simulatorHandler(event(action, method))).rejects.toMatchObject({ statusCode: 404 })
    }
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
