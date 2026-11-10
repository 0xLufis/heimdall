import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useTelemetryStream } from '../../../src/frontend/web/app/composables/useTelemetryStream'

describe('useTelemetryStream Suite', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('initializes in disconnected state with empty ring buffers', () => {
    const stream = useTelemetryStream({ bufferCapacity: 60 })
    expect(stream.connectionState.value).toBe('disconnected')
    expect(stream.lastHeartbeat.value).toBeNull()
    expect(stream.cycleTimeBuffer.value.capacity).toBe(60)
    expect(stream.cycleTimeBuffer.value.length).toBe(0)
    expect(stream.latestValues.value.cycleTimeMs).toBe(0)
    expect(stream.latestPlcState.value).toBe('OFFLINE')
  })

  it('exposes connect and disconnect handlers', async () => {
    const stream = useTelemetryStream()
    expect(typeof stream.connect).toBe('function')
    expect(typeof stream.disconnect).toBe('function')
    await stream.disconnect()
    expect(stream.connectionState.value).toBe('disconnected')
  })
})
