import { defineEventHandler, getRouterParam } from 'h3'
import { componentsAndDelegationsStore } from '../../../utils/componentsAndDelegationsStore'

export default defineEventHandler((event) => {
  const componentId = getRouterParam(event, 'id')
  if (!componentId) return []
  return componentsAndDelegationsStore.getPartHistory(componentId)
})
