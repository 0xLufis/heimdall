import { z } from 'zod'

export enum QualityCode {
  QUALITY_GOOD = 0,
  QUALITY_UNCERTAIN = 1,
  QUALITY_BAD = 2,
  QUALITY_COMM_FAILURE = 3,
  QUALITY_STALE = 4,
  QUALITY_OUT_OF_RANGE = 5
}

export enum DataTypeClassifier {
  TYPE_UNSPECIFIED = 0,
  TYPE_BOOL = 1,
  TYPE_INT8 = 2,
  TYPE_UINT8 = 3,
  TYPE_INT16 = 4,
  TYPE_UINT16 = 5,
  TYPE_INT32 = 6,
  TYPE_UINT32 = 7,
  TYPE_INT64 = 8,
  TYPE_UINT64 = 9,
  TYPE_FLOAT32 = 10,
  TYPE_FLOAT64 = 11,
  TYPE_STRING = 12,
  TYPE_DATETIME = 13,
  TYPE_DURATION = 14,
  TYPE_BYTES = 15,
  TYPE_STRUCT = 16,
  TYPE_ARRAY = 17,
  TYPE_DEVICE_STATE = 18
}

export interface TypeDescriptor {
  classifier: DataTypeClassifier
  originalPlcType: string
  unit?: string
  minRange?: number
  maxRange?: number
  isArray: boolean
  arrayDimensions: number[]
  description?: string
}

export interface DeviceStateValue {
  stateCode: number
  stateLabel: string
  rawBitmask: number
  flagStates: Record<string, boolean>
}

export type TelemetryPrimitiveValue =
  | boolean
  | number
  | string
  | bigint
  | Date
  | DeviceStateValue
  | Record<string, any>
  | any[]

export interface TelemetryDataPoint {
  pointId: string
  canonicalKey: string
  timestamp: string // ISO 8601
  quality: QualityCode
  value: TelemetryPrimitiveValue
  typeDescriptor: TypeDescriptor
  isDelta: boolean
}

// Zod Schemas for Runtime Type Validation & Sanitization
export const DeviceStateValueSchema = z.object({
  stateCode: z.number().int(),
  stateLabel: z.string(),
  rawBitmask: z.number(),
  flagStates: z.record(z.string(), z.boolean())
})

export const TypeDescriptorSchema = z.object({
  classifier: z.nativeEnum(DataTypeClassifier),
  originalPlcType: z.string(),
  unit: z.string().optional(),
  minRange: z.number().optional(),
  maxRange: z.number().optional(),
  isArray: z.boolean().default(false),
  arrayDimensions: z.array(z.number()).default([]),
  description: z.string().optional()
})

export const TelemetryDataPointSchema = z.object({
  pointId: z.string(),
  canonicalKey: z.string(),
  timestamp: z.string(),
  quality: z.nativeEnum(QualityCode),
  value: z.any(),
  typeDescriptor: TypeDescriptorSchema,
  isDelta: z.boolean().default(false)
})

// Strongly-Typed Station 1 Telemetry Record matching ST_Station1TelemetryData
export interface Station1TelemetryRecord {
  sequenceId: number
  dcTimestampNs: number
  stationName: string
  stationState: number
  spindleVelocity: number
  bearingTempC: number
  motorCurrentA: number
  activeJobCount: number
  eStopActive: boolean
  safetyGateOpen: boolean
}

export const Station1TelemetrySchema = z.object({
  sequenceId: z.number(),
  dcTimestampNs: z.number(),
  stationName: z.string(),
  stationState: z.number().int(),
  spindleVelocity: z.number(),
  bearingTempC: z.number(),
  motorCurrentA: z.number(),
  activeJobCount: z.number().int(),
  eStopActive: z.boolean(),
  safetyGateOpen: z.boolean()
})
