import { describe, it, expect } from 'vitest'
import {
  QualityCode,
  DataTypeClassifier,
  TelemetryDataPointSchema,
  Station1TelemetrySchema,
  type Station1TelemetryRecord
} from '~/types/telemetry.types'

describe('Telemetry Types and Zod Schema Validation', () => {
  it('validates complete TelemetryDataPoint with valid metadata', () => {
    const validPoint = {
      pointId: 'spindle_temp',
      canonicalKey: 'Beckhoff.Ads:192.168.1.100.1.1:851:MAIN.Station1.Telemetry._data',
      timestamp: '2026-08-31T00:00:00.000Z',
      quality: QualityCode.QUALITY_GOOD,
      value: 65.4,
      typeDescriptor: {
        classifier: DataTypeClassifier.TYPE_FLOAT64,
        originalPlcType: 'LREAL',
        unit: '°C',
        minRange: 0,
        maxRange: 120,
        isArray: false,
        arrayDimensions: []
      },
      isDelta: false
    }

    const result = TelemetryDataPointSchema.safeParse(validPoint)
    expect(result.success).toBe(true)
  })

  it('rejects invalid TelemetryDataPoint with wrong QualityCode', () => {
    const invalidPoint = {
      pointId: 'spindle_temp',
      canonicalKey: 'test',
      timestamp: 'invalid-date',
      quality: 999, // Invalid enum value
      value: 123,
      typeDescriptor: {
        classifier: DataTypeClassifier.TYPE_INT32,
        originalPlcType: 'DINT'
      }
    }

    const result = TelemetryDataPointSchema.safeParse(invalidPoint)
    expect(result.success).toBe(false)
  })

  it('validates strongly-typed Station1TelemetryRecord schema matching PLC _data', () => {
    const stationData: Station1TelemetryRecord = {
      sequenceId: 1045,
      dcTimestampNs: 1772412345000000,
      stationName: 'OP10_Assembly',
      stationState: 1,
      spindleVelocity: 3000.5,
      bearingTempC: 42.8,
      motorCurrentA: 8.4,
      activeJobCount: 3,
      eStopActive: false,
      safetyGateOpen: false
    }

    const result = Station1TelemetrySchema.safeParse(stationData)
    expect(result.success).toBe(true)
  })

  it('rejects malformed Station1TelemetryRecord missing required fields', () => {
    const malformedData = {
      stationName: 'OP10_Assembly',
      spindleVelocity: 'not-a-number' // Should be number
    }

    const result = Station1TelemetrySchema.safeParse(malformedData)
    expect(result.success).toBe(false)
  })
})
