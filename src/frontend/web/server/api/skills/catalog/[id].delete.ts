import { defineEventHandler, getRouterParam, createError } from 'h3'
import { componentsAndDelegationsStore } from '../../../utils/componentsAndDelegationsStore'

export default defineEventHandler((event) => {
  const id = getRouterParam(event, 'id')
  if (!id) {
    throw createError({ statusCode: 400, message: 'Skill ID is required.' })
  }
  const deleted = componentsAndDelegationsStore.deleteSkillDefinition(id)
  return { success: deleted }
})
