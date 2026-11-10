import { ref, shallowRef, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { HubConnection, HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'
import { CircularBuffer } from '~/utils/CircularBuffer'

export type StreamConnectionState = 'connected' | 'connecting' | 'reconnecting' | 'disconnected'

export interface TelemetryStreamOptions {
  bufferCapacity?: number
  hubUrl?: string
  targetHostname?: string
  targetMachineId?: string
}

export function useTelemetryStream(options: TelemetryStreamOptions = {}) {
  const capacity = options.bufferCapacity || 120
  const connectionState = ref<StreamConnectionState>('disconnected')
  const lastHeartbeat = ref<Date | null>(null)
  const connectionLatencyMs = ref<number>(0)
  const latestPlcState = ref<string>('OFFLINE')

  // Zero-allocation ring buffers for 60Hz streaming
  const cycleTimeBuffer = shallowRef(new CircularBuffer(capacity))
  const vibrationBuffer = shallowRef(new CircularBuffer(capacity))
  const temperatureBuffer = shallowRef(new CircularBuffer(capacity))
  const spindleRpmBuffer = shallowRef(new CircularBuffer(capacity))

  // Reactive snapshot map of current metric values
  const latestValues = ref<Record<string, number | string | boolean>>({
    cycleTimeMs: 0,
    vibrationMmS: 0,
    spindleTempC: 0,
    spindleRpm: 0,
    plcState: 'OFFLINE'
  })

  let hubConnection: HubConnection | null = null
  let isConnecting = false

  const resolveHubUrl = (): string => {
    if (options.hubUrl) return options.hubUrl
    if (typeof window !== 'undefined') {
      try {
        const config = useRuntimeConfig?.()
        const configuredUrl = config?.public?.signalrHubUrl as string | undefined
        if (configuredUrl) return configuredUrl
        if (window.location.port === '3000') {
          return `${window.location.protocol}//${window.location.hostname}:5099/hubs/maintenance`
        }
      } catch {
        // Fallback
      }
    }
    return '/hubs/maintenance'
  }

  const connect = async () => {
    if (typeof window === 'undefined' || hubConnection || isConnecting) return

    try {
      isConnecting = true
      connectionState.value = 'connecting'
      const url = resolveHubUrl()

      hubConnection = new HubConnectionBuilder()
        .withUrl(url, {
          transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
          withCredentials: true
        })
        .withAutomaticReconnect([0, 1000, 3000, 5000, 15000])
        .configureLogging(LogLevel.Warning)
        .build()

      hubConnection.onreconnecting(() => {
        connectionState.value = 'reconnecting'
      })

      hubConnection.onreconnected(() => {
        connectionState.value = 'connected'
      })

      hubConnection.onclose(() => {
        connectionState.value = 'disconnected'
      })

      // Primary live telemetry event from backend
      hubConnection.on('TelemetryReceived', (hostname: string, macAddress: string, summary: any) => {
        // Filter by target hostname if specified
        if (options.targetHostname && hostname !== options.targetHostname) return

        const now = new Date()
        lastHeartbeat.value = now

        if (summary) {
          const cycle = Number(summary.cycleTimeMs ?? summary.tactTimeMs ?? 0)
          const vib = Number(summary.vibrationMmS ?? summary.vibrationRms ?? 0)
          const temp = Number(summary.spindleTempC ?? summary.temperatureC ?? 0)
          const rpm = Number(summary.spindleRpm ?? summary.speedRpm ?? 0)
          const state = String(summary.plcState ?? summary.operationalState ?? 'RUN')

          if (cycle > 0) cycleTimeBuffer.value.push(cycle)
          if (vib > 0) vibrationBuffer.value.push(vib)
          if (temp > 0) temperatureBuffer.value.push(temp)
          if (rpm > 0) spindleRpmBuffer.value.push(rpm)

          latestPlcState.value = state
          latestValues.value = {
            cycleTimeMs: cycle,
            vibrationMmS: vib,
            spindleTempC: temp,
            spindleRpm: rpm,
            plcState: state
          }
        }
      })

      // Secondary PLC memory dump event
      hubConnection.on('PlcMemoryReceived', (hostname: string, machineId: string, amsNetId: string, symbol: string, data: any) => {
        if (options.targetMachineId && machineId !== options.targetMachineId) return
        lastHeartbeat.value = new Date()
        if (data && typeof data === 'object') {
          if (data.value !== undefined) {
            latestValues.value[symbol] = data.value
          }
        }
      })

      await hubConnection.start()
      connectionState.value = 'connected'
    } catch (err) {
      console.warn('[useTelemetryStream] SignalR connection failed, waiting for reconnect:', err)
      connectionState.value = 'disconnected'
    } finally {
      isConnecting = false
    }
  }

  const disconnect = async () => {
    if (hubConnection) {
      try {
        await hubConnection.stop()
      } catch {}
      hubConnection = null
    }
    connectionState.value = 'disconnected'
  }

  if (getCurrentInstance()) {
    onMounted(() => {
      connect()
    })

    onUnmounted(() => {
      disconnect()
    })
  }

  return {
    connectionState,
    lastHeartbeat,
    connectionLatencyMs,
    latestPlcState,
    latestValues,
    cycleTimeBuffer,
    vibrationBuffer,
    temperatureBuffer,
    spindleRpmBuffer,
    connect,
    disconnect
  }
}
