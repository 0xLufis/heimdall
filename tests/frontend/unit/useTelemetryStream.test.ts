import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { ref, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { useTelemetryStream } from '../../../src/frontend/web/app/composables/useTelemetryStream'
import { CircularBuffer } from '../../../src/frontend/web/app/utils/CircularBuffer'
import LiveTelemetryGrid from '../../../src/frontend/web/app/components/telemetry/LiveTelemetryGrid.vue'
import type { MaintenanceEvent } from '../../../src/frontend/web/app/types/maintenance'

const oscillation = (stationId: string, summary: Record<string, unknown>): MaintenanceEvent => ({
  type: 'StationOscillationReceived',
  stationName: stationId,
  summary: { stationId, ...summary },
  timestamp: new Date().toISOString()
})

describe('useTelemetryStream', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-11-10T12:00:00Z'))
  })
  afterEach(() => {
    vi.useRealTimers()
  })

  it('starts empty: unreported values are null (never 0) and the state is OFFLINE', () => {
    const stream = useTelemetryStream({ bufferCapacity: 60 })
    expect(stream.connectionState.value).toBe('disconnected')
    expect(stream.signalState.value).toBe('idle')
    expect(stream.lastHeartbeat.value).toBeNull()
    expect(stream.cycleTimeBuffer.value.capacity).toBe(60)
    expect(stream.cycleTimeBuffer.value.length).toBe(0)
    expect(stream.latestValues.value.cycleTimeMs).toBeNull()
    expect(stream.latestValues.value.vibrationMmS).toBeNull()
    expect(stream.latestPlcState.value).toBe('OFFLINE')
  })

  it('accepts samples of the target station only (by machine id or floor-plan handle)', () => {
    const stream = useTelemetryStream({ target: { stationId: 'machine-a', handle: 'L02-OP060' } })

    stream.handleEvent(oscillation('machine-b', { handle: 'L01-OP010', vibrationMmS: 9.9, temperatureC: 80 }))
    expect(stream.sampleCount.value).toBe(0)
    expect(stream.latestValues.value.vibrationMmS).toBeNull()

    stream.handleEvent(oscillation('MACHINE-A', { vibrationMmS: 1.4, spindleRpm: 2950, cycleTimeMs: 24000, operationalStatus: 'RUNNING' }))
    // Published under the handle only (station id differs in casing / form)
    stream.handleEvent({ type: 'StationOscillationReceived', stationName: 'other-id', summary: { handle: 'l02-op060', vibrationMmS: 1.6 }, timestamp: '' })

    expect(stream.sampleCount.value).toBe(2)
    expect(stream.latestValues.value.vibrationMmS).toBe(1.6)
    expect(stream.latestValues.value.spindleRpm).toBe(2950)
    expect(stream.latestPlcState.value).toBe('RUNNING')
    expect(Array.from(stream.vibrationBuffer.value.toArray()).map(v => Number(v.toFixed(1)))).toEqual([1.4, 1.6])
  })

  it('keeps stations without a vibration sensor: only reported quantities get values and trend points', () => {
    const stream = useTelemetryStream({ target: 'laser-cell' })

    stream.handleEvent(oscillation('laser-cell', { powerKw: 5.71, temperatureC: 53.4, vibrationMmS: null }))
    stream.handleEvent(oscillation('laser-cell', { powerKw: 5.69 }))

    expect(stream.latestValues.value.powerKw).toBe(5.69)
    expect(stream.latestValues.value.spindleTempC).toBe(53.4)
    expect(stream.latestValues.value.vibrationMmS).toBeNull()
    expect(stream.latestValues.value.spindleRpm).toBeNull()
    expect(stream.powerBuffer.value.length).toBe(2)
    expect(stream.temperatureBuffer.value.length).toBe(1)
    expect(stream.vibrationBuffer.value.length).toBe(0)
  })

  it('does not mistake a reported 0 for "missing"', () => {
    const stream = useTelemetryStream({ target: 'conveyor' })
    stream.handleEvent(oscillation('conveyor', { spindleRpm: 0, vibrationMmS: 0 }))
    expect(stream.latestValues.value.spindleRpm).toBe(0)
    expect(stream.spindleRpmBuffer.value.length).toBe(1)
  })

  it('clears values and trends when the target station changes', async () => {
    const target = ref<string | null>('station-1')
    const stream = useTelemetryStream({ target })

    stream.handleEvent(oscillation('station-1', { vibrationMmS: 2.2, operationalStatus: 'WARNING' }))
    expect(stream.sampleCount.value).toBe(1)

    target.value = 'station-2'
    await nextTick()

    expect(stream.sampleCount.value).toBe(0)
    expect(stream.latestValues.value.vibrationMmS).toBeNull()
    expect(stream.latestPlcState.value).toBe('OFFLINE')
    expect(stream.vibrationBuffer.value.length).toBe(0)
    expect(stream.signalState.value).toBe('waiting')

    // A late sample of the previous station is ignored
    stream.handleEvent(oscillation('station-1', { vibrationMmS: 3.3 }))
    expect(stream.sampleCount.value).toBe(0)
  })

  it('takes the operational state from station KPI events of the target', () => {
    const stream = useTelemetryStream({ target: 'station-1' })
    stream.handleEvent({ type: 'StationTelemetryReceived', stationName: 'station-9', summary: { stationId: 'station-9', operationalStatus: 'FAULTED' }, timestamp: '' })
    expect(stream.latestPlcState.value).toBe('OFFLINE')
    stream.handleEvent({ type: 'StationTelemetryReceived', stationName: 'station-1', summary: { stationId: 'station-1', operationalStatus: 'Maintenance' }, timestamp: '' })
    expect(stream.latestPlcState.value).toBe('MAINTENANCE')
  })

  it('seeds the last known snapshot without adding trend points, and live samples take over', () => {
    const stream = useTelemetryStream({ target: 'station-1' })
    stream.seed({ vibrationMmS: 1.1, temperatureC: 40, operationalStatus: 'IDLE', lastTelemetryAt: '2026-11-10T11:00:00Z' })

    expect(stream.latestValues.value.vibrationMmS).toBe(1.1)
    expect(stream.latestPlcState.value).toBe('IDLE')
    expect(stream.vibrationBuffer.value.length).toBe(0)
    expect(stream.signalState.value).toBe('stale') // an hour old

    stream.handleEvent(oscillation('station-1', { vibrationMmS: 1.3 }))
    expect(stream.signalState.value).toBe('live')

    // Once live data exists a (re-fetched) snapshot must not overwrite it
    stream.seed({ vibrationMmS: 0.2 })
    expect(stream.latestValues.value.vibrationMmS).toBe(1.3)
  })

  it('exposes connect and disconnect handlers', async () => {
    const stream = useTelemetryStream()
    await stream.disconnect()
    expect(stream.isSubscribed.value).toBe(false)
    expect(stream.connectionState.value).toBe('disconnected')
  })
})

describe('CircularBuffer.version', () => {
  it('changes on every push and clear so renderers can skip unchanged frames', () => {
    const buffer = new CircularBuffer(4)
    const v0 = buffer.version
    buffer.push(1)
    const v1 = buffer.version
    buffer.clear()
    expect(v1).toBeGreaterThan(v0)
    expect(buffer.version).toBeGreaterThan(v1)
  })
})

describe('LiveTelemetryGrid', () => {
  const stubs = { Badge: { template: '<span><slot /></span>' } }

  it('renders the sparkline canvases (template ref is bound) and labels unreported metrics', async () => {
    const stream = useTelemetryStream({ target: 'station-1' })
    stream.handleEvent(oscillation('station-1', { vibrationMmS: 3.1, cycleTimeMs: 26400, operationalStatus: 'WARNING' }))

    const wrapper = mount(LiveTelemetryGrid, {
      props: { stream, stationName: 'Rotor Laser Welding Cell', targetCycleTimeSeconds: 24 },
      global: { stubs }
    })
    await nextTick()

    expect(wrapper.findAll('canvas')).toHaveLength(6)
    const text = wrapper.text()
    expect(text).toContain('Rotor Laser Welding Cell')
    expect(text).toContain('WARNING')
    expect(text).toContain('3.10')
    expect(text).toContain('26400')
    expect(text).toContain('+10% vs target')
    // Power, temperature, spindle speed and frequency were never reported
    expect(text.match(/Not reported by this station/g)).toHaveLength(4)
    expect(text).not.toContain('NaN')
    wrapper.unmount()
  })

  it('shows that it is waiting instead of a connected badge when nothing was received', () => {
    const stream = useTelemetryStream({ target: 'station-1' })
    const wrapper = mount(LiveTelemetryGrid, { props: { stream, stationName: 'Station 1' }, global: { stubs } })
    expect(wrapper.text()).toContain('WAITING FOR FIRST SAMPLE')
    expect(wrapper.text()).toContain('NO STATE REPORTED')
    wrapper.unmount()
  })
})
