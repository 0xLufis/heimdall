import { getAllAbsences } from '../../utils/technicianRulesStore'

export default defineEventHandler(() => {
  return getAllAbsences()
})
