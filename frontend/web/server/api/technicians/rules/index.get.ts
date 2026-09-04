import { getAllRules } from '../../../utils/technicianRulesStore'

export default defineEventHandler(() => {
  return getAllRules()
})
