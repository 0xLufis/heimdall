import { defineEventHandler } from 'h3'
import { getAzureSsoConfig } from '../../../utils/auth'

export default defineEventHandler((event) => {
  const config = getAzureSsoConfig()
  const isDev = process.env.NODE_ENV !== 'production'

  return {
    enabled: config.isConfigured || isDev,
    provider: 'microsoft',
    providerName: 'Microsoft Entra ID (Azure SSO)',
    tenantId: config.tenantId,
    isConfigured: config.isConfigured,
    allowMockSimulation: isDev
  }
})
