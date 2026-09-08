/**
 * Declarative Industrial Telemetry Types
 * Aligned with Heimdall Agent Recipe Models and Fleet Master Policies
 */

export type DataCategory = 
  | 'Scalar' 
  | 'List' 
  | 'Map' 
  | 'NestedObject' 
  | 'Metric' 
  | 'DeviceState'

export type RecipeSourceType = 
  | 'SystemCim' 
  | 'SystemProcess' 
  | 'SystemDisk' 
  | 'SystemFileSystem' 
  | 'BeckhoffAds' 
  | 'BeckhoffEtherCat' 
  | 'OpcUaSubscription' 
  | 'ModbusTcp' 
  | 'TcpSocket'

export type PollingStrategyType = 
  | 'Periodic' 
  | 'Cron' 
  | 'ChangeOfValue' 
  | 'OnDemand'

export type DeadbandType = 
  | 'None' 
  | 'Absolute' 
  | 'Percentage' 
  | 'StateChangeOnly'

export type EgressPriority = 
  | 'P0_CriticalAlarm' 
  | 'P1_HighOperational' 
  | 'P2_MediumMetrics' 
  | 'P3_LowInventory'

export interface DeadbandRule {
  deadbandType: DeadbandType
  deadbandValue?: number
}

export interface PollingSchedule {
  strategy: PollingStrategyType
  intervalMs?: number
  cronExpression?: string
}

export interface DataPointDefinition {
  pointId: string
  name: string
  description?: string
  sourceType: RecipeSourceType
  dataCategory: DataCategory
  egressPriority: EgressPriority
  schedule: PollingSchedule
  deadband: DeadbandRule
  connectionParameter?: string
  pathOrSymbol?: string
  expectedType?: string
}

export interface TargetSelector {
  osPlatform: 'All' | 'Linux' | 'Windows'
  controllerRoles: string[]
  tags: Record<string, string>
}

export interface RecipeSecurity {
  keyId: string
  algorithm: string
  signPayload: boolean
}

export interface TelemetryTemplate {
  recipeId: string
  version: string
  name: string
  description?: string
  targetSelector: TargetSelector
  security: RecipeSecurity
  dataPoints: DataPointDefinition[]
  createdAt?: string
  updatedAt?: string
  isBuiltin?: boolean
}

export interface OuTagRecipeRule {
  id: string
  name: string
  description?: string
  priority: number // Evaluation order (1 = highest priority)
  enabled: boolean
  targetTemplateId: string // Recipe ID to assign
  ouPatterns: string[] // e.g. ["*Line-A*", "OU=Robotics*"] or empty for all
  ouMatchMode: 'ANY' | 'ALL'
  tags: string[] // e.g. ["Beckhoff", "IPC", "Controller"]
  tagMatchMode: 'ALL' | 'ANY' // ALL = must contain all tags; ANY = at least one
  createdAt?: string
  updatedAt?: string
}

export interface FleetAgentPolicy {
  configSchemaVersion: string
  backendUrl: string
  authType: 'NoAuth' | 'HeimdallCert' | 'UserCert'
  clientCertificatePath?: string
  serverPublicKey?: string
  enforceHardwareBinding: boolean
  spoolEncryptionMode: 'AES_256_GCM' | 'DPAPI' | 'Plaintext'
  telemetryPayloadEncryption: boolean
  allowRemoteExecution: boolean
  allowUnsignedCommands: boolean
  piiScrubberStrictLevel: 'Strict' | 'Standard' | 'Disabled'
  maxNetworkEgressBytesPerSec: number
  deltaEvaluationAlgorithm: 'xxHash64' | 'SHA256' | 'None'
  deadbandTolerancePercentage: number
  maxSpoolDiskMb: number
  heartbeatIntervalSeconds: number
  assignedTemplateId?: string
  ouTagRules?: OuTagRecipeRule[]
}

export interface HostTelemetryState {
  hostname: string
  macAddress: string
  machineIdentifier?: string
  isOnline: boolean
  lastSeen?: string
  cpuUsagePercent?: number
  ramUsagePercent?: number
  freeDiskGb?: number
  assignedTemplateName?: string
  syncStatus: 'Synced' | 'Pending' | 'Not Assigned'
  adOuPath?: string
  detectedTags?: string[]
  matchedRuleId?: string
  matchedRuleName?: string
  assignmentMode?: 'rule' | 'manual' | 'default'
}
