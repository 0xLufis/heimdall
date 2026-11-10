import { previewAdImport } from '../../utils/activeDirectoryStore'
export default defineEventHandler(async (event) => {
  const body = await readBody(event)
  return previewAdImport(body)
})
