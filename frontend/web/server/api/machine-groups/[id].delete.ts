import { deleteGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing group id' })
  const success = deleteGroup(id)
  if (!success) throw createError({ statusCode: 404, message: 'Group not found' })
  setResponseStatus(event, 204)
  return null
})
