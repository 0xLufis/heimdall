import { commitImportedHosts } from '../../utils/activeDirectoryStore'
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return commitImportedHosts(body.hosts || [])
})
