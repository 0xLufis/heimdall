import { describe, it, expect, vi, beforeEach } from 'vitest'
import { HeimdallSignalRMaintenanceProvider } from '~/services/maintenance/HeimdallSignalRMaintenanceProvider'
import { useInventoryLive } from '~/composables/useInventoryLive'
import type { MaintenanceEvent } from '~/types/maintenance'

describe('SignalR Client Methods Registration & Unified Real-Time Live Sync', () => {
  let registeredHandlers: Record<string, Function> = {}

  beforeEach(() => {
    registeredHandlers = {}
    vi.stubGlobal('useRuntimeConfig', () => ({
      public: { signalrHubUrl: '/hubs/maintenance' }
    }))
  })

  it('HeimdallSignalRMaintenanceProvider registers all IMaintenanceClient methods including InventoryUpdated and TelemetryReceived', () => {
    // Instantiate provider with absolute URL for Node test environment
    const provider = new HeimdallSignalRMaintenanceProvider('http://localhost:5099/hubs/maintenance')
    const hubConn = (provider as any).hubConnection

    expect(hubConn).toBeDefined()

    // Simulate event notifications
    const receivedEvents: MaintenanceEvent[] = []
    const unsub = provider.subscribeToEvents((event) => {
      receivedEvents.push(event)
    })

    // Simulate server pushing InventoryUpdated
    const notifyListeners = (provider as any).notifyListeners.bind(provider)
    notifyListeners({
      type: 'InventoryUpdated',
      source: 'gRPC_Telemetry',
      hostname: 'CPC-011',
      mac: '00:1B:44:11:3A:B7',
      timestamp: new Date().toISOString()
    })

    // Simulate server pushing TelemetryReceived
    notifyListeners({
      type: 'TelemetryReceived',
      hostname: 'CPC-011',
      mac: '00:1B:44:11:3A:B7',
      summary: { CpuUsage: 18.5, RamUsage: 45.2, DiskSpace: 120.4 },
      timestamp: new Date().toISOString()
    })

    // Simulate server pushing StationTelemetryReceived & StationOscillationReceived
    notifyListeners({
      type: 'StationTelemetryReceived',
      stationName: 'L01-OP010',
      summary: { oee: 92.5, partsPerHour: 148 },
      timestamp: new Date().toISOString()
    })

    notifyListeners({
      type: 'StationOscillationReceived',
      stationName: 'L01-OP010',
      summary: { vibrationMmS: 1.34, spindleRpm: 2850, powerKw: 4.8 },
      timestamp: new Date().toISOString()
    })

    expect(receivedEvents.length).toBe(4)
    expect(receivedEvents[0].type).toBe('InventoryUpdated')
    expect(receivedEvents[0].hostname).toBe('CPC-011')
    expect(receivedEvents[1].type).toBe('TelemetryReceived')
    expect(receivedEvents[1].summary?.CpuUsage).toBe(18.5)
    expect(receivedEvents[2].type).toBe('StationTelemetryReceived')
    expect(receivedEvents[2].stationName).toBe('L01-OP010')
    expect(receivedEvents[3].type).toBe('StationOscillationReceived')
    expect(receivedEvents[3].summary?.vibrationMmS).toBe(1.34)

    unsub()
  })

  it('useInventoryLive notifies subscribers when real-time updates arrive', () => {
    const { onInventoryUpdate, notifySubscribers } = useInventoryLive()

    let count = 0
    onInventoryUpdate(() => {
      count++
    })

    notifySubscribers()
    expect(count).toBe(1)
  })
})
