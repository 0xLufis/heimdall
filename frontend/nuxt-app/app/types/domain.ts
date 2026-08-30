export type ControllerRole = 'Primary' | 'Secondary' | 'Safety' | 'Motion' | 'Vision' | 'Gateway' | 'Autonomous'

export type ControllerType = 
  | 'IPC' 
  | 'PLC' 
  | 'SoftPLC' 
  | 'RobotController' 
  | 'Dispenser' 
  | 'VisionController' 
  | 'AutonomousDevice'

export type HardwareCategory = 
  | 'NIC' 
  | 'CPU' 
  | 'RAM' 
  | 'Storage' 
  | 'Servo' 
  | 'FieldbusCoupler' 
  | 'DispenserHead' 
  | 'Sensor' 
  | 'Motor' 
  | 'Camera' 
  | 'Valve' 
  | 'General'

export type SoftwareType = 
  | 'OS' 
  | 'Driver' 
  | 'SoftPLC_Project' 
  | 'MES_Connector' 
  | 'OPC_UA_NodeSet' 
  | 'Patch' 
  | 'License'

export type InterconnectProtocol = 
  | 'EtherCAT' 
  | 'PROFINET' 
  | 'OPC_UA' 
  | 'ModbusTCP' 
  | 'EtherNet_IP' 
  | 'Serial' 
  | 'Ethernet'

export interface ResponsibleTeam {
  id: string
  name: string
  description?: string
}

export interface Manufacturer {
  id: string
  name: string
  website?: string
  supportContact?: string
}

export interface Supplier {
  id: string
  name: string
  website?: string
  contactPerson?: string
  email?: string
}

export interface DiskSpaceInfo {
  totalFreeGB: number
  osDriveFreeGB: number
  drives?: Record<string, number>
}

export interface BeckhoffDriverInfo {
  adapterName: string
  driverVersion: string
  serviceName: string
  pciDeviceId: string
  isRealtimeDriverBound: boolean
}

export interface ControllerTelemetry {
  cpuUsagePercent?: number
  ramUsagePercent?: number
  diskSpace?: DiskSpaceInfo
  beckhoffRT?: BeckhoffDriverInfo
  ipAddress?: string
  osVersion?: string
  lastOnline?: string
  isOnline: boolean
}

export interface StationControllerLink {
  id: string
  stationId: string
  controllerId: string
  hostname?: string
  name?: string
  ipAddress?: string
  role?: ControllerRole | string
  isPrimary?: boolean
  createdAt?: string
}

export interface ProductionStation {
  id: string
  name: string
  displayName?: string
  customIdentifier: string
  pinnedObjectHandle?: string | null
  organizationId?: string
  isOnline?: boolean
  alertCount?: number
  primaryControllerId?: string
  primaryControllerName?: string
  controllers?: StationControllerLink[]
  responsibleTeams?: ResponsibleTeam[]
  hardwareComponents?: HardwareComponent[]
  children?: BaseInventoryItem[]
}

export interface IndustrialController {
  id: string
  name: string
  displayName?: string
  hostname: string
  macAddress: string
  ipAddress?: string
  machineIdentifier?: string
  controllerType?: ControllerType
  pinnedObjectHandle?: string | null
  lastOnline?: string | null
  lastSeen?: string | null
  organizationId?: string
  controlledMachines?: Array<{ id: string; name: string; customIdentifier?: string; pinnedObjectHandle?: string }>
  machines?: Array<{ id: string; name: string; customIdentifier?: string; pinnedObjectHandle?: string }>
  responsibleTeams?: ResponsibleTeam[]
  inventoryItems?: BaseInventoryItem[]
  telemetry?: ControllerTelemetry
  freeDiskSpace?: DiskSpaceInfo
  systemMetadata?: Record<string, any>
}

export interface BaseInventoryItem {
  id: string
  name: string
  displayName?: string
  itemType: string
  organizationId?: string
  costInHUF?: number
  purchaseDate?: string
  serialNumber?: string
  manufacturerId?: string
  manufacturer?: Manufacturer
  supplierId?: string
  supplier?: Supplier
  clientPcId?: string
  clientPc?: IndustrialController
  machineId?: string
  machine?: ProductionStation
  parentId?: string
  parent?: BaseInventoryItem
  children?: BaseInventoryItem[]
  responsibleTeams?: ResponsibleTeam[]
  metadata?: Record<string, any>
  topLevelFlags?: {
    type?: 'hardware' | 'software' | 'peripherals'
  }
}

export interface HardwareComponent extends BaseInventoryItem {
  revision?: string
  modelNumber?: string
  category?: HardwareCategory
  firmware?: SoftwareAsset[]
}

export interface SoftwareAsset extends BaseInventoryItem {
  version?: string
  licenseKey?: string
  softwareType?: SoftwareType
  copiaRepoUrl?: string
  copiaCommitSha?: string
}

export interface EquipmentInterconnect {
  id: string
  sourceEquipmentId: string
  sourceEquipmentName?: string
  targetEquipmentId: string
  targetEquipmentName?: string
  interconnectType: string
  protocol: InterconnectProtocol | string
  channelInfo?: string
  portOrAddress?: string
  status: 'Active' | 'Inactive' | 'Degraded'
  metadata?: Record<string, any>
}

export interface FloorPlan {
  id: string
  name: string
  svgContent: string
  anchors: Array<{
    handle: string
    name: string
    x?: number
    y?: number
  }>
  createdAt: string
}

export interface AgentEventItem {
  id?: string
  title: string
  description: string
  time: string
  source?: string
  level?: string
  severity: 'low' | 'medium' | 'high' | 'critical'
}
