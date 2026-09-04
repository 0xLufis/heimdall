import { getAllGroups } from '../utils/machineGroupsStore'

export default defineEventHandler(() => {
  return getAllGroups()
})
