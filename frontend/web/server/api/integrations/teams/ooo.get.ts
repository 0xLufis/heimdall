import { getTeamsOooStatuses } from '../../../utils/technicianRulesStore'

export default defineEventHandler(() => {
  return {
    devMode: true,
    statuses: getTeamsOooStatuses()
  }
})
