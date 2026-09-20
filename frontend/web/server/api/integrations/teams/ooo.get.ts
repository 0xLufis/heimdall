import { getTeamsOooStatuses } from '../../../utils/technicianRulesStore'
import { featureFlags } from '../../../utils/featureFlags'

export default defineEventHandler(() => {
  return {
    devMode: featureFlags.enableDevFeatures,
    statuses: getTeamsOooStatuses()
  }
})
