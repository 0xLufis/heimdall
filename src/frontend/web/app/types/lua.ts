export type LuaScriptTriggerType =
  | 'Manual'
  | 'TelemetryChange'
  | 'MachineStateChange'
  | 'SlaPreventiveCheck'
  | 'UiExtension'
  | 'Scheduled'

export interface LuaScriptRecord {
  id: string
  name: string
  description: string
  triggerType: LuaScriptTriggerType
  code: string
  isActive: boolean
  createdBy?: string | null
  createdAt: string
  updatedAt: string
  lastExecutedAt?: string | null
  lastExecutionSuccess?: boolean | null
  lastExecutionError?: string | null
  metadata?: Record<string, string>
}

export type LuaUiTargetZone =
  | 'MachineDetails'
  | 'TicketDetails'
  | 'LiveTelemetry'
  | 'DashboardOverview'
  | 'SidebarMenu'
  | 'GeneralWorkbench'

export type LuaUiExtensionType =
  | 'ActionButton'
  | 'Badge'
  | 'MetricCard'
  | 'BannerAlert'

export interface LuaUiExtension {
  id: string
  title: string
  description?: string | null
  targetZone: LuaUiTargetZone
  extensionType: LuaUiExtensionType
  icon: string
  color?: string | null
  handlerFunctionName?: string | null
  properties?: Record<string, unknown>
}

export interface LuaExecutionResult {
  success: boolean
  returnValue?: unknown
  output: string
  error?: string | null
  /** .NET TimeSpan serialized as "hh:mm:ss.fffffff". */
  duration?: string
  logs?: string[]
}
