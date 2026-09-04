import type { IMaintenanceService } from '~/types/maintenance'
import { HeimdallSignalRMaintenanceProvider } from './HeimdallSignalRMaintenanceProvider'
import { RestFallbackMaintenanceProvider } from './RestFallbackMaintenanceProvider'
import { OfflineQueueMaintenanceProvider } from './OfflineQueueMaintenanceProvider'
import { ExternalEnterpriseMaintenanceAdapter } from './ExternalEnterpriseMaintenanceAdapter'

export * from './IMaintenanceService'
export * from './HeimdallSignalRMaintenanceProvider'
export * from './RestFallbackMaintenanceProvider'
export * from './OfflineQueueMaintenanceProvider'
export * from './ExternalEnterpriseMaintenanceAdapter'

let defaultMaintenanceService: IMaintenanceService | null = null

export function getMaintenanceService(): IMaintenanceService {
  if (!defaultMaintenanceService) {
    if (typeof window !== 'undefined') {
      const baseProvider = new HeimdallSignalRMaintenanceProvider()
      defaultMaintenanceService = new OfflineQueueMaintenanceProvider(baseProvider)
    } else {
      defaultMaintenanceService = new RestFallbackMaintenanceProvider()
    }
  }
  return defaultMaintenanceService
}

export function setMaintenanceService(service: IMaintenanceService) {
  defaultMaintenanceService = service
}
