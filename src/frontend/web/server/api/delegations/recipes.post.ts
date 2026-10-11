import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.name || !body.discipline) {
    throw createError({ statusCode: 400, message: 'name and discipline are required.' })
  }
  return componentsAndDelegationsStore.createDelegationRecipe(body)
})
