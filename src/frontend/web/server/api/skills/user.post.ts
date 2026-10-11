import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.userId || !body.skillId) {
    throw createError({ statusCode: 400, message: 'userId and skillId are required.' })
  }
  return componentsAndDelegationsStore.assignSkillToUser(body)
})
