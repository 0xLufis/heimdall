import { createGroup } from '../../utils/machineGroupsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.name) {
    throw createError({ statusCode: 400, message: 'Group name is required' })
  }
  const created = createGroup(body)
  setResponseStatus(event, 201)
  return created
})
