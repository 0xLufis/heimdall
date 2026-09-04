import { deleteOuRule } from '../../../utils/pkiStore'
export default defineEventHandler((event) => {
  const id = getRouterParam(event, 'id')
  if (id) deleteOuRule(id)
  return { message: 'Rule deleted successfully' }
})
