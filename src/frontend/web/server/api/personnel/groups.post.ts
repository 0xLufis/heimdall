import { defineEventHandler, readBody } from 'h3'
import { componentsAndDelegationsStore, type PersonnelGroupEntry } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(async (event) => {
  const body = await readBody<Omit<PersonnelGroupEntry, 'id' | 'createdAt'>>(event)
  if (!body || !body.name) {
    throw createError({ statusCode: 400, message: 'Group name is required.' })
  }
  return componentsAndDelegationsStore.createPersonnelGroup(body)
})
