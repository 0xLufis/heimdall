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
  createdBy?: string
  createdAt: string
  updatedAt: string
  lastExecutedAt?: string
  lastExecutionSuccess?: boolean
  lastExecutionError?: string
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
  description?: string
  targetZone: LuaUiTargetZone
  extensionType: LuaUiExtensionType
  icon: string
  color?: string
  handlerFunctionName?: string
  conditionExpression?: string
  properties?: Record<string, any>
}

export interface LuaExecutionResult {
  success: boolean
  returnValue?: any
  output: string
  error?: string
  duration?: string
  logs?: string[]
}
