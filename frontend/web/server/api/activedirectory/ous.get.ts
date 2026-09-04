import { getActiveDirectoryOus } from '../../utils/activeDirectoryStore'
export default defineEventHandler(() => getActiveDirectoryOus())
