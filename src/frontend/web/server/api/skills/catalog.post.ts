import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.code || !body.name) {
    throw createError({ statusCode: 400, message: 'code and name are required.' })
  }
  return componentsAndDelegationsStore.createSkillDefinition(body)
})
