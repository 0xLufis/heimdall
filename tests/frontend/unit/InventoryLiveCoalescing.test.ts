import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'

// Hub events: capture the listener useInventoryLive registers, like the real hub fans out.
const hubListeners = new Set<(ev: any) => void>()
vi.mock('~/services/maintenance', () => ({
  getMaintenanceService: () => ({
    subscribeToEvents: (listener: (ev: any) => void) => {
      hubListeners.add(listener)
      return () => hubListeners.delete(listener)
    }
  })
}))

import { useInventoryLive, INVENTORY_REFRESH_COALESCE_MS } from '../../../src/frontend/web/app/composables/useInventoryLive'

const emit = (type: string) => hubListeners.forEach(l => l({ type, timestamp: new Date().toISOString() }))

describe('useInventoryLive refresh coalescing', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => {
    vi.useRealTimers()
    hubListeners.clear()
  })

  it('refreshes once at once and once more at the end of a burst of InventoryUpdated events', () => {
    const refresh = vi.fn()
    const Host = defineComponent({
      setup() {
        useInventoryLive().onInventoryUpdate(refresh)
        return () => h('div')
      }
    })
    const wrapper = mount(Host)

    // A heartbeat burst: 20 updates in 2 s (one every 100 ms).
    for (let i = 0; i < 20; i++) {
      emit('InventoryUpdated')
      vi.advanceTimersByTime(100)
    }
    expect(refresh).toHaveBeenCalledTimes(1) // the leading refresh only

    vi.advanceTimersByTime(INVENTORY_REFRESH_COALESCE_MS)
    expect(refresh).toHaveBeenCalledTimes(2) // one trailing refresh covers the rest of the burst

    vi.advanceTimersByTime(10 * INVENTORY_REFRESH_COALESCE_MS)
    expect(refresh).toHaveBeenCalledTimes(2) // quiet hub, no further refreshes

    // Telemetry traffic never triggers an inventory refetch.
    emit('TelemetryReceived')
    emit('StationTelemetryReceived')
    vi.advanceTimersByTime(INVENTORY_REFRESH_COALESCE_MS)
    expect(refresh).toHaveBeenCalledTimes(2)

    wrapper.unmount()
  })

  it('drops a pending refresh when the last consumer unmounts', () => {
    const refresh = vi.fn()
    const Host = defineComponent({
      setup() {
        useInventoryLive().onInventoryUpdate(refresh)
        return () => h('div')
      }
    })
    const wrapper = mount(Host)
    emit('InventoryUpdated')
    vi.advanceTimersByTime(0)
    emit('InventoryUpdated') // schedules a trailing refresh
    const calls = refresh.mock.calls.length
    wrapper.unmount()
    vi.advanceTimersByTime(2 * INVENTORY_REFRESH_COALESCE_MS)
    expect(refresh).toHaveBeenCalledTimes(calls)
  })
})
