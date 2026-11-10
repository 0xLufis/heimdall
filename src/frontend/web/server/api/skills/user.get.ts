import { defineEventHandler, getQuery } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler((event) => {
  const query = getQuery(event)
  const userId = query.userId as string | undefined
  return componentsAndDelegationsStore.getPersonnelSkills(userId)
})
