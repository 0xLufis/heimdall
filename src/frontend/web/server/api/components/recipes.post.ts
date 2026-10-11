import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  if (!body.recipeCode || !body.name) {
    throw createError({ statusCode: 400, message: 'recipeCode and name are required.' })
  }
  return componentsAndDelegationsStore.createRecipe(body)
})
