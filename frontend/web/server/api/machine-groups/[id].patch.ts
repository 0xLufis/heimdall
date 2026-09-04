import { updateGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const id = getRouterParam(event, 'id')
  if (!id) throw createError({ statusCode: 400, message: 'Missing group id' })
  const body = await readBody(event)
  const updated = updateGroup(id, body)
  if (!updated) throw createError({ statusCode: 404, message: 'Group not found' })
  return updated
})
