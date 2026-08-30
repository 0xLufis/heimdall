import type { 
  ProductionStation, 
  IndustrialController, 
  BaseInventoryItem, 
  AgentEventItem 
} from './domain'

export interface DashboardStats {
  totalUsers: string
  activeClients: string
  pendingAlerts: string
  avgUptime: string
}

export interface RecentClientItem {
  id: string
  hostname: string
  os: string
  lastSeen: string
}

export interface DashboardResponse {
  stats: DashboardStats
  recentClients: RecentClientItem[]
  securityEvents: AgentEventItem[]
}

export interface SearchResultDto {
  id: string
  name: string
  displayName?: string
  itemType: string
  typeLabel?: string
  manufacturerName?: string | null
}

export interface SearchKeysGroup {
  group: string
  keys: string[]
}

export interface PaginatedResult<T> {
  items: T[]
  totalCount: number
  page?: number
  pageSize?: number
}

export interface ClientPcUpdateDto {
  id: string
  name: string
  hostname?: string
  macAddress: string
  pinnedObjectHandle?: string | null
  controlledMachineIds?: string[]
}

export interface MachineUpdateDto {
  id: string
  name: string
  customIdentifier: string
  pinnedObjectHandle?: string | null
  organizationId?: string
  controllerIds?: string[]
}
