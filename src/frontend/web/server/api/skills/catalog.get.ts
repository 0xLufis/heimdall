import { defineEventHandler } from 'h3'
import { componentsAndDelegationsStore } from '../../utils/componentsAndDelegationsStore'

export default defineEventHandler(() => {
  return componentsAndDelegationsStore.getSkillsCatalog()
})
